#region Assembly UHFReader18CSharp, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
// D:\workspaces\Electron HW-VX6346K HW-VX6330K\Demo\Csharp\Demo v2.9\UHFReader18CSharp.dll
// Decompiled with ICSharpCode.Decompiler 7.1.0.6543
#endregion

using Newtonsoft.Json;
using Service_RFID_BIG_READER.Database;
using Service_RFID_BIG_READER.DTO;
using Service_RFID_BIG_READER.Reader;
using Service_RFID_BIG_READER.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.Eventing.Reader;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    public class ZTX_G20_DLL_DEFINE
    {
        Int16 port = 0;
        private byte fComAdr = 0xff; //ComAdr hiện đang hoạt động
        private byte fBaud;
        private byte[] fPassWord = new byte[4];
        private string isFull = "1";
        private int fCmdRet = 30;// Giá trị trả về của tất cả các lệnh đã thực hiện
        private int fErrorCode;
        private byte maskadr = 0x00;
        private byte maskLen = 0x00;
        private byte maskFlag = 0x00;
        private byte AdrTID = 0x00;
        private byte LenTID = 0x00;
        private byte TIDFlag;
        private string fInventoryEPCList; //Lưu trữ danh sách truy vấn (nếu dữ liệu đọc không thay đổi thì sẽ không được làm mới)
        private int frmComPortIndex;
        TagInfo tagInfo;
        public ZTX_G20_DLL_DEFINE()
        {
            this.tagInfo = new TagInfo();
            this.TIDFlag = Convert.ToByte(ConfigurationManager.AppSettings["TIDFlag"]);
        }
        public bool Connect(string port_com)
        {
            try
            {
                int FrmPortIndex = 0;
                string temp;

                //Lấy số port
                temp = port_com;
                temp = temp.Trim();
                port = Convert.ToInt16(temp.Substring(3, temp.Length - 3));

                for (int i = 6; i >= 0; i--)
                {
                    fBaud = Convert.ToByte(i);
                    if (fBaud == 3)
                        continue;
                    ZTX_G20_DLLProject.CloseComPort();
                    Thread.Sleep(50);
                    fCmdRet = ZTX_G20_DLLProject.OpenComPort(port, ref fComAdr, fBaud, ref FrmPortIndex);
                    if (fCmdRet != 0)
                        return false;

                    if ((FrmPortIndex != -1) & (fCmdRet != 0X35) & (fCmdRet != 0X30))
                    {
                        frmComPortIndex = FrmPortIndex;
                        Setting();
                        if (TIDFlag == 0x01)
                        {
                            AdrTID = 0x02;
                            LenTID = 0x04;
                        }
                        else
                        {
                            AdrTID = 0x00;
                            LenTID = 0x00;
                        }
                        return true;
                    }
                    if ((FrmPortIndex == -1) && (fCmdRet == 0x30))
                    {
                        return false;
                    }
                    break;
                }
                return true;
            }
            catch
            {
                return false;
            }

        }
        private bool Setting()
        {
            fBaud = Convert.ToByte(ConfigurationManager.AppSettings["baundRate"]);
            if (fBaud > 2)
                fBaud = Convert.ToByte(fBaud + 2);
            fCmdRet = ZTX_G20_DLLProject.Writebaud(ref fComAdr, ref fBaud, frmComPortIndex);
            if (fCmdRet == 0)
                return true;
            else return false;
        }
        private string ByteArrayToHexString(byte[] data)
        {
            StringBuilder sb;
            try
            {
                sb = new StringBuilder(data.Length * 3); foreach (byte b in data)
                    sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
                return sb.ToString().ToUpper();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private byte[] HexStringToByteArray(string s)
        {
            byte[] buffer;
            try
            {
                s = s.Replace(" ", "");
                buffer = new byte[s.Length / 2];
                for (int i = 0; i < s.Length; i += 2)
                    buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
                return buffer;
            }
            catch (Exception)
            {
                return null;
            }
        }
        public async void Inventory()
        {
            try
            {
                int CardNum = 0;
                int Totallen = 0;
                int EPClen, m;
                byte[] EPC = new byte[5000];
                int CardIndex;
                string temps;
                string s, sEPC;
                bool isonlistview;

                ///
                /*  byte WordPtr, ENum = 0;
                  byte Num = 0;
                  byte Mem = 0;
                  byte EPClength = 0;*/

                ///
                fCmdRet = ZTX_G20_DLLProject.Inventory_G2(ref fComAdr, AdrTID, LenTID, TIDFlag, EPC, ref Totallen, ref CardNum, frmComPortIndex);
                if ((fCmdRet == 1) | (fCmdRet == 2) | (fCmdRet == 3) | (fCmdRet == 4) | (fCmdRet == 0xFB))//代表已查找结束，
                {
                    byte[] daw = new byte[Totallen];
                    Array.Copy(EPC, daw, Totallen);
                    temps = ByteArrayToHexString(daw);
                    m = 0;
                    if (CardNum != 0)
                    {
                        for (CardIndex = 0; CardIndex < CardNum; CardIndex++) // Danh sach the doc duoc
                        {
                            EPClen = daw[m];
                            sEPC = temps.Substring(m * 2 + 2, EPClen * 2);

                            DTOTagInfo dto = await SqliteDataAccess.FindOrByMulKey(new string[2] { "tidNg", "tidXe" }, sEPC);
                            if (dto == null)
                            {
                                tagInfo = new TagInfo("epc", sEPC, "password", "crc", "40", "ant", false);
                                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_NOTFOUND_TAG);
                            }
                            else
                            {
                                bool isNg = false;
                                if (sEPC == dto.tidNg)
                                {
                                    // Nếu thẻ người chưa vào
                                    if (!dto.isInNg)
                                    {
                                        isNg = true;
                                        tagInfo = new TagInfo(dto.epcNg, dto.tidNg, dto.passNg, "crc", "40", "ant", isNg);
                                        if (EncPass(tagInfo.password, tagInfo.epc))
                                            Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                                    }
                                }
                                else if (sEPC == dto.tidXe)
                                {
                                    // Nếu thẻ xe chưa vào
                                    if (!dto.isInXe)
                                    {
                                        isNg = false;
                                        tagInfo = new TagInfo(dto.epcXe, dto.tidXe, dto.passXe, "crc", "40", "ant", isNg);
                                        if (EncPass(tagInfo.password, tagInfo.epc))
                                            Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                                    }
                                }
                            }

                            m = m + EPClen + 1;
                            if (sEPC.Length != EPClen * 2)
                                return;
                            isonlistview = false;
                            if (!isonlistview)
                            {
                                s = sEPC;
                                s = (sEPC.Length / 2).ToString().PadLeft(2, '0');
                            }
                        }
                        return;
                    }
                }
                else Service1._isReaderDisconnect = false;
            }
            catch (Exception)
            {
            }
        }

        public bool EncPass(string accessPassWord, string epc)
        {
            if (ConfigMapping.ENC_PASS == 0)
                return true;
            byte WordPtr;
            byte Num;
            byte Mem;
            byte ENum;
            byte EPClength;
            byte[] CardData = new byte[320];
            EPClength = Convert.ToByte(epc.Length / 2);
            ENum = Convert.ToByte(epc.Length / 4);
            byte[] EPC_1tag = new byte[ENum];
            EPC_1tag = HexStringToByteArray(epc);
            WordPtr = Convert.ToByte("2", 16); // Địa chỉ bắt đầu đọc
            Num = Convert.ToByte("2"); // Độ dài của chuỗi EPC (tối đa là 12 byte)
            Mem = 0;  // Password checked
            fPassWord = HexStringToByteArray(accessPassWord);
            fCmdRet = ZTX_G20_DLLProject.ReadCard_G2(ref fComAdr, EPC_1tag, Mem, WordPtr, Num, fPassWord, maskadr, maskLen, maskFlag, CardData, EPClength, ref fErrorCode, frmComPortIndex);
            if (fCmdRet == 0)
            {
                return true;
            }
            return false;
        }

    }
    public static class ZTX_G20_DLLProject
    {
        private const string DLLNAME = @"Reader_DLL\ZTX_G20_DLLProject_x86.dll";

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenNetPort(int Port, string IPaddr, ref byte ComAddr, ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseNetPort(int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenComPort(int Port, ref byte ComAddr, byte Baud, ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseComPort();

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int AutoOpenComPort(ref int Port, ref byte ComAddr, byte Baud, ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseSpecComPort(int Port);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetReaderInformation(ref byte ConAddr, byte[] VersionInfo, ref byte ReaderType, byte[] TrType, ref byte dmaxfre, ref byte dminfre, ref byte powerdBm, ref byte ScanTime, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteComAdr(ref byte ConAddr, ref byte ComAdrData, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPowerDbm(ref byte ConAddr, byte powerDbm, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Writedfre(ref byte ConAddr, ref byte dmaxfre, ref byte dminfre, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Writebaud(ref byte ConAddr, ref byte baud, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteScanTime(ref byte ConAddr, ref byte ScanTime, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int InSelfTestMode(ref byte ConAddr, bool IsSelfTestMode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int RfOutput(ref byte ConAddr, byte onoff, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPWM(ref byte ConAddr, byte PWM, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadPWM(ref byte ConAddr, ref byte PWM, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPowerParameter(ref byte ConAddr, ref byte power, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Getpower(ref byte ConAddr, ref byte power, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckPowerParameter(ref byte ConAddr, ref int code, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetStartInformation(ref byte ConAddr, ref byte ADF7020E, ref byte FreE, ref byte addrE, ref byte scnE, ref byte xpwrE, ref byte pwmE, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SolidifyPWMandPowerlist(ref byte ConAddr, byte[] dBm_list, ref int code, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Inventory_G2(ref byte ConAddr, byte AdrTID, byte LenTID, byte TIDFlag, byte[] EPClenandEPC, ref int Totallen, ref int CardNum, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadCard_G2(ref byte ConAddr, byte[] EPC, byte Mem, byte WordPtr, byte Num, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte[] Data, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteCard_G2(ref byte ConAddr, byte[] EPC, byte Mem, byte WordPtr, byte Writedatalen, byte[] Writedata, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, int WrittenDataNum, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteBlock_G2(ref byte ConAddr, byte[] EPC, byte Mem, byte WordPtr, byte Writedatalen, byte[] Writedata, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, int WrittenDataNum, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int EraseCard_G2(ref byte ConAddr, byte[] EPC, byte Mem, byte WordPtr, byte Num, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetCardProtect_G2(ref byte ConAddr, byte[] EPC, byte select, byte setprotect, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int DestroyCard_G2(ref byte ConAddr, byte[] EPC, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteEPC_G2(ref byte ConAddr, byte[] Password, byte[] WriteEPC, byte WriteEPClen, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetReadProtect_G2(ref byte ConAddr, byte[] EPC, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetMultiReadProtect_G2(ref byte ConAddr, byte[] Password, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int RemoveReadProtect_G2(ref byte ConAddr, byte[] Password, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckReadProtected_G2(ref byte ConAddr, ref byte readpro, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetEASAlarm_G2(ref byte ConAddr, byte[] EPC, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte EAS, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckEASAlarm_G2(ref byte ConAddr, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int LockUserBlock_G2(ref byte ConAddr, byte[] EPC, byte[] Password, byte maskadr, byte maskLen, byte maskFlag, byte BlockNum, byte EPClength, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Inventory_6B(ref byte ConAddr, byte[] ID_6B, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int inventory2_6B(ref byte ConAddr, byte Condition, byte StartAddress, byte mask, byte[] ConditionContent, byte[] ID_6B, ref int Cardnum, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadCard_6B(ref byte ConAddr, byte[] ID_6B, byte StartAddress, byte Num, byte[] Data, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteCard_6B(ref byte ConAddr, byte[] ID_6B, byte StartAddress, byte[] Writedata, byte Writedatalen, ref int writtenbyte, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int LockByte_6B(ref byte ConAddr, byte[] ID_6B, byte Address, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckLock_6B(ref byte ConAddr, byte[] ID_6B, byte Address, ref byte ReLockState, ref int errorcode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWGParameter(ref byte ConAddr, byte Wg_mode, byte Wg_Data_Inteval, byte Wg_Pulse_Width, byte Wg_Pulse_Inteval, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWorkMode(ref byte ConAddr, byte[] Parameter, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetWorkModeParameter(ref byte ConAddr, byte[] Parameter, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadActiveModeData(byte[] ModeData, ref int Datalength, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetAccuracy(ref byte ConAddr, byte Accuracy, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetOffsetTime(ref byte ConAddr, byte OffsetTime, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetFhssMode(ref byte ConAddr, byte FhssMode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetFhssMode(ref byte ConAddr, ref byte FhssMode, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetTriggerTime(ref byte ConAddr, ref byte TriggerTime, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int BuzzerAndLEDControl(ref byte ConAddr, byte AvtiveTime, byte SilentTime, byte Times, int FrmHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRelay(ref byte ConAddr, byte RelayStatus, int PortHandle);
    }
}
#if false // Decompilation log
'9' items in cache
------------------
Resolve: 'mscorlib, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
Found single assembly: 'mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089'
WARN: Version mismatch. Expected: '2.0.0.0', Got: '4.0.0.0'
Load from: 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\mscorlib.dll'
#endif
