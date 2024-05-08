using Newtonsoft.Json;
using Service_RFID_BIG_READER.Database;
using Service_RFID_BIG_READER.DTO;
using Service_RFID_BIG_READER.Reader;
using Service_RFID_BIG_READER.Util;
using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    public class SYC_R16_DLL_DEFINE
    {
        string pc = string.Empty;
        string epc = string.Empty;
        string crc = string.Empty;
        string rssi = string.Empty;

        static SYC_R16_DLLProject.CallReceive aDataReceive = null;
        TagInfo tagInfo;

        public SYC_R16_DLL_DEFINE()
        {
            tagInfo = new TagInfo();
        }
        public bool Connect(string port)
        {
            int result;
            aDataReceive = DataReceive;
            result = SYC_R16_DLLProject.Connect((byte)Service1._connectType, port, aDataReceive);
            return (result == 0) ? true : false;
        }

        public int DataReceive(byte Type, byte Command, int LpRecSize, IntPtr LpRecByt)
        {
            byte[] RecByt = null;
            if (LpRecSize > 0)
            {
                RecByt = new byte[LpRecSize];
                System.Runtime.InteropServices.Marshal.Copy(LpRecByt, RecByt, 0, LpRecSize);
            }
            ProcReceive(Type, Command, RecByt);
            return 1;
        }
        async void ProcReceive(byte Type, byte Command, byte[] ParamData)
        {
            try
            {
                if (Type == 0x02 && Command == 0x22 && ParamData != null)
                {
                    int rssidBm = ParamData[0];
                    if (rssidBm > 127)
                    {
                        rssidBm = -((-rssidBm) & 0xFF);
                    }
                    rssidBm -= Convert.ToInt32("-27", 10);
                    rssidBm -= Convert.ToInt32("1", 10);
                    rssi = rssidBm.ToString();

                    int PCEPCLength = (ParamData[1] / 8) * 2;
                    pc = BytesToHexString(ParamData, " ", 1, 2);
                    epc = BytesToHexString(ParamData, " ", 3, PCEPCLength);
                    crc = BytesToHexString(ParamData, " ", 3 + PCEPCLength, 2);

                    DTOTagInfo dto = await SqliteDataAccess.FindOrByMulKey(new string[2] { "epcNg", "epcXe" }, epc);
                    if (dto == null)
                    {
                        tagInfo = new TagInfo(epc, "tid", "pasword", crc, rssi, "ant", false);
                        Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_NOTFOUND_TAG);
                    }
                    else
                    {
                        bool isNg = false;
                        if (epc == dto.epcNg)
                        {
                            // Nếu thẻ người đã vào
                            if (dto.isInNg)
                            {
                                isNg = true;
                                tagInfo = new TagInfo(dto.epcNg, dto.tidNg, dto.passNg, crc, rssi, "ant", isNg);
                                if (EncPass(tagInfo.password, tagInfo.epc))
                                    Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                            }
                        }
                        else if (epc == dto.epcXe)
                        {
                            // Nếu thẻ xe đã vào
                            if (dto.isInXe)
                            {
                                isNg = false;
                                tagInfo = new TagInfo(dto.epcXe, dto.tidXe, dto.passXe, crc, rssi, "ant", isNg);
                                if (EncPass(tagInfo.password, tagInfo.epc))
                                    Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                Service1.WriteLog("ERROR SYC_R16 ProcReceive");
            }
        }
        void SetSelect(string epc)
        {
            byte Target = (byte)0;
            byte Action = (byte)0;
            byte MemBank = 1;
            int Pointer = BytesToInt(HexStringToBytes("00000020"));
            byte Truncated = (byte)0x00;
            byte[] MaskByt = HexStringToBytes(epc);
            SYC_R16_DLLProject.SetSelectParam(Target, Action, MemBank, Pointer, Truncated, MaskByt, (byte)MaskByt.Length);
        }

        private bool EncPass(string accessPassWord, string epc)
        {
            try
            {
                if (ConfigMapping.ENC_PASS == 0)
                    return true;
                byte[] AccessPassword = HexStringToBytes(accessPassWord);
                byte MemBank = (byte)0; // CheckPass
                int StartIndex = BytesToShort(HexStringToBytes("0002"));
                int Length = BytesToShort(HexStringToBytes("0002"));
                byte[] PC = new byte[2], EPC = new byte[12], Data = new byte[1024];
                int Size = 0;
                int retDword;
                SetSelect(epc);
                retDword = SYC_R16_DLLProject.ReadData(AccessPassword, MemBank, StartIndex, Length, PC, EPC, Data, ref Size);
                Thread.Sleep(20);
                if (retDword != 0)
                {
                    return false;
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        ulong BytesToUlong(byte[] Bytes, int startIndex = 0, int length = 0)
        {
            if (length <= 0)
            {
                length = Bytes.Length - startIndex;
            }
            int NumSize = 8 > length ? length : 8;
            ulong RetUlong = 0;
            for (int i = 0; i < NumSize; i++)
            {
                RetUlong <<= 8;
                RetUlong |= Convert.ToUInt64((Bytes[i] & 0x00ff));
            }
            return RetUlong;
        }

        short BytesToShort(byte[] Bytes, int startIndex = 0, int length = 0)
        {
            return (short)BytesToUlong(Bytes, startIndex, length <= 0 || length > 2 ? 2 : length);
        }

        int BytesToInt(byte[] Bytes, int startIndex = 0, int length = 0)
        {
            return Convert.ToInt32(BytesToUlong(Bytes, startIndex, length <= 0 || length > 4 ? 4 : length));
        }

        string BytesToHexString(byte[] Bytes, string Separator, int startIndex = 0, int length = 0)
        {
            if (length <= 0)
            {
                length = Bytes.Length - startIndex;
            }
            return BitConverter.ToString(Bytes, startIndex, length).Replace("-", "");
        }
        byte[] HexStringToBytes(string HexString)
        {
            if (string.IsNullOrEmpty(HexString))
            {
                return null;
            }
            if (HexString.Length % 2 > 0)
            {
                HexString = "0" + HexString;
            }
            int ByteInt = HexString.Length / 2;
            byte[] ByteAr = new byte[ByteInt];
            for (int i = 0; i < ByteInt; i++)
            {
                ByteAr[i] = Convert.ToByte(HexString.Substring(i * 2, 2), 16);
            }
            return ByteAr;
        }


    }
    internal class SYC_R16_DLLProject
    {
        private const string DLL_Name = @"Reader_DLL\SYC_R16_DLLProject_x86.dll";

        public struct NET_DeviceInfo
        {
            /// <summary>
            /// MAC地址
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] MAC;
            /// <summary>
            /// IP地址
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] IP;
            /// <summary>
            /// 版本
            /// </summary>
            public byte VER;
            /// <summary>
            /// 设备名长度
            /// </summary>
            public byte LEN;
            /// <summary>
            /// 设备名
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
            public byte[] NAME;
        };
        public struct _DEVICEHW_CONFIG
        {
            /// <summary>
            /// 设备类型,具体见设备类型表
            /// </summary>
            public byte bDevType;
            /// <summary>
            /// 设备子类型
            /// </summary>
            public byte bAuxDevType;
            /// <summary>
            /// 设备序号
            /// </summary>
            public byte bIndex;
            /// <summary>
            /// 设备硬件版本号
            /// </summary>
            public byte bDevHardwareVer;
            /// <summary>
            /// 设备软件版本号
            /// </summary>
            public byte bDevSoftwareVer;
            /// <summary>
            /// 模块名
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
            public byte[] szModulename;
            /// <summary>
            /// 模块网络MAC地址
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] bDevMAC;
            /// <summary>
            /// 模块IP地址
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] bDevIP;
            /// <summary>
            /// 模块网关IP
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] bDevGWIP;
            /// <summary>
            /// 模块子网掩码
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] bDevIPMask;
            /// <summary>
            /// DHCP 使能，是否启用DHCP,1:启用，0：不启用
            /// </summary>
            public byte bDhcpEnable;
            /// <summary>
            /// 保留
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x1D)]
            public byte[] breserved;
        };

        public struct _DEVICEPORT_CONFIG
        {
            /// <summary>
            /// 端口序号
            /// </summary>
            public byte bIndex;
            /// <summary>
            /// 端口启用标志 1：启用后 ；0：不启用
            /// </summary>
            public byte bPortEn;
            /// <summary>
            /// 网络工作模式: 0: TCP SERVER;1: TCP CLENT; 2: UDP SERVER 3：UDP CLIENT;
            /// </summary>
            public byte bNetMode;
            /// <summary>
            /// TCP 客户端模式下随即本地端口号，1：随机 0: 不随机
            /// </summary>
            public byte bRandSportFlag;
            /// <summary>
            /// 网络通讯端口号
            /// </summary>
            public ushort wNetPort;
            /// <summary>
            /// 目的IP地址
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] bDesIP;
            /// <summary>
            /// 工作于TCP Server模式时，允许外部连接的端口号
            /// </summary>
            public ushort wDesPort;
            /// <summary>
            /// 串口波特率: 300---921600bps
            /// </summary>
            public uint dBaudRate;
            /// <summary>
            /// 串口数据位: 5---8位 
            /// </summary>
            public byte bDataSize;
            /// <summary>
            /// 串口停止位: 1表示1个停止位; 2表示2个停止位
            /// </summary>
            public byte bStopBits;
            /// <summary>
            /// 串口校验位: 0表示奇校验; 1表示偶校验; 2表示标志位(MARK,置1); 3表示空白位(SPACE,清0);
            /// </summary>
            public byte bParity;
            /// <summary>
            /// PHY断开，Socket动作，1：关闭Socket 2、不动作
            /// </summary>
            public byte bPHYChangeHandle;
            /// <summary>
            /// 串口RX数据打包长度，最大1024
            /// </summary>
            public uint dRxPktlength;
            /// <summary>
            /// 串口RX数据打包转发的最大等待时间,单位为: 10ms,0则表示关闭超时功能
            /// </summary>
            public uint dRxPktTimeout;
            /// <summary>
            /// 工作于TCP CLIENT时，连接TCP SERVER的最大重试次数
            /// </summary>
            public byte bReConnectCnt;
            /// <summary>
            /// 串口复位操作: 0表示不清空串口数据缓冲区; 1表示连接时清空串口数据缓冲区
            /// </summary>
            public byte bResetCtrl;
            /// <summary>
            /// 域名功能启用标志，1：启用 2：不启用
            /// </summary>
            public byte bDNSFlag;
            /// <summary>
            /// 域名
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] szDomainname;
            /// <summary>
            /// DNS 主机
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] bDNSHostIP;
            /// <summary>
            /// DNS 端口
            /// </summary>
            public ushort wDNSHostPort;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] breserved;
        };

        public struct _NET_DEVICE_CONFIG
        {
            /// <summary>
            /// 从硬件处获取的配置信息
            /// </summary>
            public _DEVICEHW_CONFIG HWCfg;
            /// <summary>
            /// 网络设备所包含的子设备的配置信息
            /// </summary>
            [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 2, ArraySubType = UnmanagedType.Struct)]
            public _DEVICEPORT_CONFIG[] PortCfg;
        };

        //struct转换为byte[]
        public static byte[] StructToBytes(object structObj)
        {
            int size = Marshal.SizeOf(structObj);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structObj, buffer, false);
                byte[] bytes = new byte[size];
                Marshal.Copy(buffer, bytes, 0, size);
                return bytes;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        //byte[]转换为struct
        public static object BytesToStruct(byte[] bytes, Type strcutType)
        {
            int size = Marshal.SizeOf(strcutType);
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(bytes, 0, buffer, size);
                return Marshal.PtrToStructure(buffer, strcutType);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public static int GetStructLength(Type strcutType)
        {
            return Marshal.SizeOf(strcutType);
        }

        /// <summary>回调函数</summary>
        public delegate int CallReceive(byte Type, byte Command, int LpRecSize, IntPtr LpRecByt);
        /// <summary>回调函数</summary>
        public delegate int Udp_Receive(string IP, int Port, int LpRecSize, IntPtr LpRecByt);
        /// <summary>回调函数</summary>
        public delegate int Svr_Receive(byte Type, string IP, int Port, int LpRecSize, IntPtr LpRecByt);

        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Connect(byte ConnType, string ConnChar, CallReceive rc);

        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Disconnect();
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetModuleInfo(ref byte InfoType, StringBuilder InfoData, ref int DataSize);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadSingle();
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadMulti(int PollCount);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int StopRead();
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetSelectParam(byte Target, byte Action, byte MemBank, int Pointer, byte Truncated, byte[] MaskData, byte MaskSize);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetSelectMode(byte Mode);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadData(byte[] AccessPassword, byte MemBank, int StartIndex, int Length, byte[] PC, byte[] EPC, byte[] Data, ref int Size);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteData(byte[] AccessPassword, byte MemBank, int StartIndex, byte[] Data, int Size, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRegion(byte Region);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetRfChannel(ref byte RfChannel);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRfChannel(byte RfChannel);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetFhss(bool Fhss);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetPower(ref int Power);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPower(int Power);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetCW(bool CW);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetQuery(ref byte DR, ref byte M, ref byte TRext, ref byte Sel, ref byte Session, ref byte Target, ref byte Q);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetQuery(byte DR, byte M, byte TRext, byte Sel, byte Session, byte Target, byte Q);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ImpinjMonzaQT(byte[] AccessPassword, byte RW, byte Persistence, byte Payload, byte[] PC, byte[] EPC, byte[] QTControl);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NxpChangeConfig(byte[] AccessPassword, byte[] Config, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NxpChangeEas(byte[] AccessPassword, byte Protect, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NxpReadProtect(byte[] AccessPassword, byte Protect, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NxpEasAlarm(byte[] EASAlarmCode);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int LockUnlock(byte[] AccessPassword, byte[] LD, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Kill(byte[] AccessPassword, byte[] PC, byte[] EPC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ScanJammer(ref byte CH_L, ref byte CH_H, byte[] JMR);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int ScanRSSI(ref byte CH_L, ref byte CH_H, byte[] JMR);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetModemPara(ref byte Mixer_G, ref byte IF_G, ref int Thrd);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetModemPara(byte Mixer_G, byte IF_G, int Thrd);

        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_Open(string IP);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_Close();
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_SearchForDevices(byte DeviceType, ref int Count, byte[] Data, ref int Length);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_FactoryReset(byte DeviceType, byte[] MAC);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_GetInfo(byte DeviceType, byte[] MAC, byte[] Data, ref int Length);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int NetCfg_SetInfo(byte DeviceType, byte[] LocalMAC, byte[] DevMAC, byte[] Data, int Length);

        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Svr_Startup(byte ConnType, string ConnChar, Svr_Receive rc);
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Svr_CleanUp();
        [DllImport(DLL_Name, CallingConvention = CallingConvention.StdCall)]
        public static extern int Svr_Send(string ConnChar, byte[] Data, int Length);
    }
}
