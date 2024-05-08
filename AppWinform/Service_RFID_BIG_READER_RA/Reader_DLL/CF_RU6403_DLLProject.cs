using Newtonsoft.Json;
using Service_RFID_BIG_READER.Database;
using Service_RFID_BIG_READER.DTO;
using Service_RFID_BIG_READER.Reader;
using Service_RFID_BIG_READER.Util;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    public class CF_RU6403_DLL_DEFINE
    {

        private RFIDCallBack elegateRFIDCallBack;
        private Dictionary<string, TagInfo> _tagInfoNgDic = new Dictionary<string, TagInfo>();
        private Dictionary<string, TagInfo> _tagInfoXeDic = new Dictionary<string, TagInfo>();

        private byte port = 0;
        private byte fComAdr = 0xff; //ComAdr hiện đang hoạt động
        private byte fBaud;
        private byte[] fPassWord = new byte[4];
        private int fCmdRet = 30;// Giá trị trả về của tất cả các lệnh đã thực hiện
        private int fErrorCode;
        private int frmComportIndex;
        private TagInfo tagInfo;
        private byte TIDFlag = 0;
        private byte tidLen = 0;
        private byte tidAddr = 0;
        private byte fastFlag = 0;
        private byte target = 0;
        private byte scantime = 0;
        private byte Qvalue = 4;
        private byte session = 0;
        private int cardNum = 0;

        public CF_RU6403_DLL_DEFINE()
        {
            tagInfo = new TagInfo();
            this.TIDFlag = Convert.ToByte(ConfigurationManager.AppSettings["TIDFlag"]);
            if (TIDFlag == 1)
            {
                tidAddr = 2;
                tidLen = 4;
            }
            elegateRFIDCallBack = new RFIDCallBack(GetUid);
        }

        private void WritePower()
        {
            byte WritePower = Convert.ToByte(ConfigurationManager.AppSettings["power"]);
            WritePower = Convert.ToByte(WritePower | 0x80); // enable

            fCmdRet = CF_RU6403_DLLProject.WriteRfPower(ref fComAdr, WritePower, frmComportIndex);
            if (fCmdRet != 0)
            {
                string strLog = "Set power failed： ";
                Service1.WriteLog(strLog);
            }
        }

        private void WriteBeep()
        {
            byte beepEn = Convert.ToByte(ConfigurationManager.AppSettings["beepEn"]);
            fCmdRet = CF_RU6403_DLLProject.SetBeepNotification(ref fComAdr, beepEn, frmComportIndex);
            if (fCmdRet != 0)
            {
                string strLog = "Set beep failed";
                Service1.WriteLog(strLog);
            }
        }

        private void WriteAntenna()
        {
            byte ANT = 0;
            ANT = Convert.ToByte(ConfigurationManager.AppSettings["antenna"]);
            fCmdRet = CF_RU6403_DLLProject.SetAntennaMultiplexing(ref fComAdr, ANT, frmComportIndex);
            if (fCmdRet != 0)
            {
                string strLog = "Antenna config failed";
                Service1.WriteLog(strLog);
            }
        }

        private void GetInformation()
        {
            byte TrType = 0;
            byte[] VersionInfo = new byte[2];
            byte ReaderType = 0;
            byte ScanTime = 0;
            byte dmaxfre = 0;
            byte dminfre = 0;
            byte powerdBm = 0;
            byte Ant = 0;
            byte BeepEn = 0;
            byte OutputRep = 0;
            byte CheckAnt = 0;
            fCmdRet = CF_RU6403_DLLProject.GetReaderInformation(ref fComAdr, VersionInfo, ref ReaderType, ref TrType, ref dmaxfre, ref dminfre, ref powerdBm, ref ScanTime, ref Ant, ref BeepEn, ref OutputRep, ref CheckAnt, frmComportIndex);
            if (fCmdRet != 0)
            {
                string strLog = "Get Reader Information failed";
                Service1.WriteLog(strLog);
            }
        }
        public bool Connect(string port_com)
        {
            try
            {
                //Lấy số port
                int FrmPortIndex = 0;
                string temp;
                temp = port_com;
                temp = temp.Trim();
                port = Convert.ToByte(temp.Substring(3, temp.Length - 3));

                fBaud = Convert.ToByte(ConfigurationManager.AppSettings["baundRate"]);
                if (fBaud > 2)
                    fBaud = Convert.ToByte(fBaud + 2);

                CF_RU6403_DLLProject.CloseComPort();
                fCmdRet = CF_RU6403_DLLProject.OpenComPort(port, ref fComAdr, fBaud, ref FrmPortIndex);
                if (fCmdRet != 0)
                    return false;

                frmComportIndex = FrmPortIndex;
                GetInformation();
                if (FrmPortIndex > 0)
                    CF_RU6403_DLLProject.InitRFIDCallBack(elegateRFIDCallBack, true, FrmPortIndex);
                WriteAntenna();
                WritePower();
                WriteBeep();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task<bool> CheckTime(DateTime dateTime, string keyCondition, string valueCondition)
        {
            DateTime now = DateTime.Now;
            double diffInSeconds = (now - dateTime).TotalSeconds;
            //Kiểm tra xem thẻ đó đã được free chưa (thời gian cập nhật lần cuối)
            if (diffInSeconds < 5)
            {
                await SqliteDataAccess.UpdateByKey(
                      "TagInfo",
                      "lastUpdate",
                      DateTime.UtcNow.ToString(SqliteDataAccess._timeFormat),
                      keyCondition, valueCondition
                      );
                return false;
            }
            return true;
        }

        public async void GetUid(IntPtr p, Int32 nEvt)
        {
            RFIDTag ce = (RFIDTag)Marshal.PtrToStructure(p, typeof(RFIDTag));
            int Antnum = ce.ANT;
            string str_ant = Convert.ToString(Antnum, 2).PadLeft(4, '0');
            string epclen = Convert.ToString(ce.LEN, 16);
            if (epclen.Length == 1) epclen = "0" + epclen;
            // string para = str_ant + "," + epclen + ce.UID + "," + ce.RSSI.ToString() + " ";
            // System.Diagnostics.Debug.WriteLine(para);

            DTOTagInfo dto = await SqliteDataAccess.FindOrByMulKey(new string[2] { "tidNg", "tidXe" }, ce.UID);
            if (dto == null)
            {
                System.Diagnostics.Debug.WriteLine(ce.UID + " " + str_ant + " " + DateTime.Now);
                tagInfo = new TagInfo("epc", ce.UID, "password", "crc", "40", "ant", false);
                if (ConfigMapping.ARR_ANTENNA_OUT.IndexOf((byte)Antnum) != -1)
                {
                    Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_NOTFOUND_TAG);
                }
                else
                    Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_NOTFOUND_TAG);
            }
            else
            {
                string topic = TopicPub.IN_MESSAGE;
                bool isIn = false;
                if (ConfigMapping.ARR_ANTENNA_OUT.IndexOf((byte)Antnum) != -1)
                {
                    topic = TopicPub.OUT_MESSAGE;
                    isIn = true;
                }
                bool isNg = false;
                if (ce.UID == dto.tidNg)
                {
                    // Nếu thẻ người chưa vào
                    if ((dto.isInNg == isIn) && await CheckTime(dto.lastUpdate, "tidNg", ce.UID))
                    {
                        isNg = true;
                        tagInfo = new TagInfo(dto.epcNg, dto.tidNg, dto.passNg, "crc", ce.RSSI.ToString(), "ant", topic, isNg);
                        if (!_tagInfoNgDic.ContainsKey(tagInfo.tid))
                            _tagInfoNgDic.Add(tagInfo.tid, tagInfo);
                    }
                }
                else if (ce.UID == dto.tidXe)
                {
                    // Nếu thẻ xe chưa vào
                    if (dto.isInXe == isIn && await CheckTime(dto.lastUpdate, "tidXe", ce.UID))
                    {
                        isNg = false;
                        tagInfo = new TagInfo(dto.epcXe, dto.tidXe, dto.passXe, "crc", ce.RSSI.ToString(), "ant", topic, isNg);
                        if (!_tagInfoXeDic.ContainsKey(tagInfo.tid))
                            _tagInfoXeDic.Add(tagInfo.tid, tagInfo);
                        //if (EncPass(tagInfo.password, tagInfo.epc))
                        //Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), topic);
                    }
                }
            }

            //Service1.PublishMessage(para, TopicPub.IN_NOTFOUND_TAG);
        }

        public static byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }
        public bool EncPass(string accessPassWord, string epc)
        {
            if (ConfigMapping.ENC_PASS == 0)
                return true;
            byte WordPtr = 0x02, ENum;
            byte Num = 0x02;
            byte Mem = 0; //C_Reserve
            byte[] CardData = new byte[320];
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[100];
            ENum = Convert.ToByte(epc.Length / 4);
            byte[] EPC = new byte[ENum * 2];
            EPC = HexStringToByteArray(epc);
            fPassWord = HexStringToByteArray(accessPassWord);

            for (int p = 0; p < 10; p++)
            {
                fCmdRet = CF_RU6403_DLLProject.ReadData_G2(ref fComAdr, EPC, ENum, Mem, WordPtr, Num, fPassWord, MaskMem, MaskAdr, MaskLen, MaskData, CardData, ref fErrorCode, frmComportIndex);
                if (fCmdRet == 0)
                {
                    return true;
                }
            }
            if (fCmdRet != 0)
            {
                return false;
            }
            else
            {
                /* byte[] daw = new byte[Num * 2];
                 Array.Copy(CardData, daw, Num * 2);
                 text_WriteData.Text = ByteArrayToHexString(daw);*/
                return true;
            }
        }

        public void Inventory(byte inAnt)
        {
            TagInfo tagInfo = new TagInfo();
            byte Ant = 0;
            int TagNum = 0;
            int Totallen = 0;
            byte[] EPC = new byte[50000];
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[100];
            byte MaskFlag = 0;
            cardNum = 0;
            try
            {
                fCmdRet = CF_RU6403_DLLProject.Inventory_G2(ref fComAdr, Qvalue, session, MaskMem, MaskAdr, MaskLen, MaskData, MaskFlag, tidAddr, tidLen, TIDFlag, target, inAnt, scantime, fastFlag, EPC, ref Ant, ref Totallen, ref TagNum, frmComportIndex);
                if (fCmdRet == 0x30)
                {
                    cardNum = 0;
                }
                if ((fCmdRet == 1) || (fCmdRet == 2) || (fCmdRet == 0xFB) || (fCmdRet == 0x26))
                {
                    if (_tagInfoNgDic.Count > 0)
                    {
                        tagInfo = _tagInfoNgDic.First().Value;
                        if (EncPass(tagInfo.password, tagInfo.epc))
                        {
                            Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), tagInfo.topic);
                            _tagInfoNgDic.Remove(tagInfo.tid);
                        }
                    }
                    if (_tagInfoXeDic.Count > 0)
                    {
                        tagInfo = _tagInfoXeDic.First().Value;
                        if (EncPass(tagInfo.password, tagInfo.epc))
                        {
                            Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), tagInfo.topic);
                            _tagInfoXeDic.Remove(tagInfo.tid);
                        }
                    }
                    /* IntPtr ptrWnd = IntPtr.Zero;
                 string para = tagrate.ToString() + "," + totalTagnum.ToString();
                 // SendMessage(ptrWnd, WM_SENDTAGSTAT, IntPtr.Zero, para);*/
                    return;
                }
                Service1._isReaderDisconnect = false;
            }
            catch (Exception)
            {
                Service1._isReaderDisconnect = false;
                return;
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RFIDTag
    {
        public byte PacketParam;
        public byte LEN;
        public string UID;
        public byte RSSI;
        public byte ANT;
        public Int32 Handles;
    }

    public delegate void RFIDCallBack(IntPtr p, Int32 nEvt);

    public static class CF_RU6403_DLLProject

    {
        private const string DLLNAME = @"Reader_DLL\CF_RU6403_DLLProject_x86.dll";

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        internal static extern void InitRFIDCallBack(RFIDCallBack t, bool uidBack, int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenNetPort(int Port,
                                             string IPaddr,
                                             ref byte ComAddr,
                                             ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseNetPort(int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenComPort(int Port,
                                                 ref byte ComAddr,
                                                 byte Baud,
                                                 ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseComPort();

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int AutoOpenComPort(ref int Port,
                                                 ref byte ComAddr,
                                                 byte Baud,
                                                 ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseSpecComPort(int Port);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int OpenUSBPort(ref byte ComAddr,
                                                 ref int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CloseUSBPort(int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetReaderInformation(ref byte ComAdr,              //读写器地址		
                                                      byte[] VersionInfo,           //软件版本
                                                      ref byte ReaderType,              //读写器型号
                                                      ref byte TrType,      //支持的协议
                                                      ref byte dmaxfre,           //当前读写器使用的最高频率
                                                      ref byte dminfre,           //当前读写器使用的最低频率
                                                      ref byte powerdBm,             //读写器的输出功率
                                                      ref byte ScanTime,
                                                      ref byte Ant,
                                                      ref byte BeepEn,
                                                      ref byte OutputRep,
                                                      ref byte CheckAnt,
                                                      int FrmHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRegion(ref byte ComAdr,
                                           byte dmaxfre,
                                           byte dminfre,
                                           int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetAddress(ref byte ComAdr,
                                             byte ComAdrData,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetInventoryScanTime(ref byte ComAdr,
                                               byte ScanTime,
                                               int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetBaudRate(ref byte ComAdr,
                                           byte baud,
                                           int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRfPower(ref byte ComAdr,
                                             byte powerDbm,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int BuzzerAndLEDControl(ref byte ComAdr,
                                                     byte AvtiveTime,
                                                     byte SilentTime,
                                                     byte Times,
                                                     int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetWorkMode(ref byte ComAdr,
                                             byte Read_mode,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetAntennaMultiplexing(ref byte ComAdr,
                                            byte Ant,
                                            int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetBeepNotification(ref byte ComAdr,
                                         byte BeepEn,
                                         int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetReal_timeClock(ref byte ComAdr,
                                          byte[] paramer,
                                          int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetTime(ref byte ComAdr,
                                          byte[] paramer,
                                          int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetRelay(ref byte ComAdr,
                                          byte RelayTime,
                                          int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetGPIO(ref byte ComAdr,
                                         byte OutputPin,
                                         int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetGPIOStatus(ref byte ComAdr,
                                         ref byte OutputPin,
                                         int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetNotificationPulseOutput(ref byte ComAdr,
                                              byte OutputRep,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetSystemParameter(ref byte ComAdr,
                                                      ref byte Read_mode,
                                                      ref byte Accuracy,
                                                      ref byte RepCondition,
                                                      ref byte RepPauseTime,
                                                      ref byte ReadPauseTim,
                                                      ref byte TagProtocol,
                                                      ref byte MaskMem,
                                                      byte[] MaskAdr,
                                                      ref byte MaskLen,
                                                      byte[] MaskData,
                                                      ref byte TriggerTime,
                                                      ref byte AdrTID,
                                                      ref byte LenTID,
                                                      int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetEASSensitivity(ref byte ComAdr,
                                             byte Accuracy,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetTriggerTime(ref byte ComAdr,
                                             byte TriggerTime,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetTIDParameter(ref byte ComAdr,
                                             byte AdrTID,
                                             byte LenTID,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetMask(ref byte ComAdr,
                                         byte MaskMem,
                                         byte[] MaskAdr,
                                         byte MaskLen,
                                         byte[] MaskData,
                                         int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetResponsePamametersofAuto_runningMode(ref byte ComAdr,
                                                 byte RepCondition,
                                                 byte RepPauseTime,
                                                 int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetInventoryInterval(ref byte ComAdr,
                                                  byte ReadPauseTim,
                                                  int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SelectTagType(ref byte ComAdr,
                                                byte Protocol,
                                                int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetCommType(ref byte ComAdr,
                                                byte CommType,
                                                int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetTagBufferInfo(ref byte ComAdr,
                                                   byte[] Data,
                                                   ref int dataLength,
                                                   int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ClearTagBuffer(ref byte ComAdr,
                                             int frmComPortindex);




        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadActiveModeData(byte[] ScanModeData,
                                                    ref int ValidDatalength,
                                                    int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Inventory_G2(ref byte ComAdr,
                                              byte QValue,
                                              byte Session,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              byte MaskFlag,
                                              byte AdrTID,
                                              byte LenTID,
                                              byte TIDFlag,
                                              byte Target,
                                              byte InAnt,
                                              byte Scantime,
                                              byte FastFlag,
                                              byte[] pEPCList,
                                              ref byte Ant,
                                              ref int Totallen,
                                              ref int cardNum,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int InventoryMix_G2(ref byte ComAdr,
                                              byte QValue,
                                              byte Session,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              byte MaskFlag,
                                              byte ReadMem,
                                              byte[] ReadAdr,
                                              byte ReadLen,
                                              byte[] Psd,
                                              byte Target,
                                              byte InAnt,
                                              byte Scantime,
                                              byte FastFlag,
                                              byte[] pEPCList,
                                              ref byte Ant,
                                              ref int Totallen,
                                              ref int cardNum,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadData_G2(ref byte ComAdr,
                                             byte[] EPC,
                                             byte ENum,
                                             byte Mem,
                                             byte WordPtr,
                                             byte Num,
                                             byte[] Password,
                                             byte MaskMem,
                                             byte[] MaskAdr,
                                             byte MaskLen,
                                             byte[] MaskData,
                                             byte[] Data,
                                             ref int errorcode,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteData_G2(ref byte ComAdr,
                                              byte[] EPC,
                                              byte WNum,
                                              byte ENum,
                                              byte Mem,
                                              byte WordPtr,
                                              byte[] Wdt,
                                              byte[] Password,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              ref int errorcode,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteEPC_G2(ref byte ComAdr,
                                             byte[] Password,
                                             byte[] WriteEPC,
                                             byte ENum,
                                             ref int errorcode,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int KillTag_G2(ref byte ComAdr,
                                                byte[] EPC,
                                                byte ENum,
                                                byte[] Password,
                                                byte MaskMem,
                                                byte[] MaskAdr,
                                                byte MaskLen,
                                                byte[] MaskData,
                                                ref int errorcode,
                                                int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Lock_G2(ref byte ComAdr,
                                                   byte[] EPC,
                                                   byte ENum,
                                                   byte select,
                                                   byte setprotect,
                                                   byte[] Password,
                                                   byte MaskMem,
                                                   byte[] MaskAdr,
                                                   byte MaskLen,
                                                   byte[] MaskData,
                                                   ref int errorcode,
                                                   int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int BlockErase_G2(ref byte ComAdr,
                                              byte[] EPC,
                                              byte ENum,
                                              byte Mem,
                                              byte WordPtr,
                                              byte Num,
                                              byte[] Password,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              ref int errorcode,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPrivacyWithoutEPC_G2(ref byte ComAdr,
                                                          byte[] Password,
                                                          ref int errorcode,
                                                          int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetPrivacyByEPC_G2(ref byte ComAdr,
                                                  byte[] EPC,
                                                  byte ENum,
                                                  byte[] Password,
                                                  byte MaskMem,
                                                  byte[] MaskAdr,
                                                  byte MaskLen,
                                                  byte[] MaskData,
                                                  ref int errorcode,
                                                  int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ResetPrivacy_G2(ref byte ComAdr,
                                                      byte[] Password,
                                                      ref int errorcode,
                                                      int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckPrivacy_G2(ref byte ComAdr,
                                                      ref byte readpro,
                                                      ref int errorcode,
                                                      int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int EASConfigure_G2(ref byte ComAdr,
                                                  byte[] EPC,
                                                  byte ENum,
                                                  byte[] Password,
                                                  byte EAS,
                                                  byte MaskMem,
                                                  byte[] MaskAdr,
                                                  byte MaskLen,
                                                  byte[] MaskData,
                                                  ref int errorcode,
                                                  int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int EASAlarm_G2(ref byte ComAdr,
                                                  ref int errorcode,
                                                  int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int BlockLock_G2(ref byte ComAdr,
                                                  byte[] EPC,
                                                  byte ENum,
                                                  byte[] Password,
                                                  byte WrdPointer,
                                                  byte MaskMem,
                                                  byte[] MaskAdr,
                                                  byte MaskLen,
                                                  byte[] MaskData,
                                                  ref int errorcode,
                                                  int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int BlockWrite_G2(ref byte ComAdr,
                                              byte[] EPC,
                                              byte WNum,
                                              byte ENum,
                                              byte Mem,
                                              byte WordPtr,
                                              byte[] Wdt,
                                              byte[] Password,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              ref int errorcode,
                                              int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ChangeATMode(ref byte ConAddr,
                                               byte ATMode,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int TransparentCMD(ref byte ConAddr,
                                               byte timeout,
                                               byte cmdlen,
                                               byte[] cmddata,
                                               ref byte recvLen,
                                               byte[] recvdata,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetSeriaNo(ref byte ConAddr,
                                               byte[] SeriaNo,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetCheckAnt(ref byte ComAdr,
                                             byte CheckAnt,
                                             int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int InventorySingle_6B(ref byte ConAddr,
                                                  ref byte ant,
                                                  byte[] ID_6B,
                                                  int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int InventoryMultiple_6B(ref byte ConAddr,
                                               byte Condition,
                                               byte StartAddress,
                                               byte mask,
                                               byte[] ConditionContent,
                                               ref byte ant,
                                               byte[] ID_6B,
                                               ref int cardNum,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadData_6B(ref byte ConAddr,
                                               byte[] ID_6B,
                                               byte StartAddress,
                                               byte Num,
                                               byte[] Data,
                                               ref int errorcode,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteData_6B(ref byte ConAddr,
                                               byte[] ID_6B,
                                               byte StartAddress,
                                               byte[] Writedata,
                                               byte Writedatalen,
                                               ref int writtenbyte,
                                               ref int errorcode,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int Lock_6B(ref byte ConAddr,
                                               byte[] ID_6B,
                                               byte Address,
                                               ref int errorcode,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int CheckLock_6B(ref byte ConAddr,
                                               byte[] ID_6B,
                                               byte Address,
                                               ref byte ReLockState,
                                               ref int errorcode,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetQS(ref byte ConAddr,
                                               byte Qvalue,
                                               byte Session,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetQS(ref byte ConAddr,
                                       ref byte Qvalue,
                                       ref byte Session,
                                       int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetFlashRom(ref byte ConAddr,
                                       int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetModuleVersion(ref byte ConAddr,
                                               byte[] Version,
                                               int PortHandle);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ExtReadData_G2(ref byte ComAdr,
                                             byte[] EPC,
                                             byte ENum,
                                             byte Mem,
                                             byte[] WordPtr,
                                             byte Num,
                                             byte[] Password,
                                             byte MaskMem,
                                             byte[] MaskAdr,
                                             byte MaskLen,
                                             byte[] MaskData,
                                             byte[] Data,
                                             ref int errorcode,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ExtWriteData_G2(ref byte ComAdr,
                                              byte[] EPC,
                                              byte WNum,
                                              byte ENum,
                                              byte Mem,
                                              byte[] WordPtr,
                                              byte[] Wdt,
                                              byte[] Password,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              ref int errorcode,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int InventoryBuffer_G2(ref byte ComAdr,
                                              byte QValue,
                                              byte Session,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              byte MaskFlag,
                                              byte AdrTID,
                                              byte LenTID,
                                              byte TIDFlag,
                                              byte Target,
                                              byte InAnt,
                                              byte Scantime,
                                              byte FastFlag,
                                              ref int BufferCount,
                                              ref int TagNum,
                                              int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetSaveLen(ref byte ComAdr,
                                              byte SaveLen,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetSaveLen(ref byte ComAdr,
                                            ref byte SaveLen,
                                            int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadBuffer_G2(ref byte ComAdr,
                                              ref int Totallen,
                                              ref int cardNum,
                                              byte[] pEPCList,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ClearBuffer_G2(ref byte ComAdr,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetBufferCnt_G2(ref byte ComAdr,
                                               ref int Count,
                                              int frmComPortindex);


        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetReadMode(ref byte ComAdr,
                                             byte ReadMode,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetReadParameter(ref byte ComAdr,
                                              byte[] Parameter,
                                              byte MaskMem,
                                              byte[] MaskAdr,
                                              byte MaskLen,
                                              byte[] MaskData,
                                              byte MaskFlag,
                                              byte AdrTID,
                                              byte LenTID,
                                              byte TIDFlag,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetReadParameter(ref byte ComAdr,
                                             byte[] Parameter,
                                              int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int WriteRfPower(ref byte ComAdr,
                                             byte powerDbm,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int ReadRfPower(ref byte ComAdr,
                                             ref byte powerDbm,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int RetryTimes(ref byte ComAdr,
                                             ref byte Times,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int SetDRM(ref byte ComAdr,
                                             byte DRM,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetDRM(ref byte ComAdr,
                                             ref byte DRM,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int GetReaderTemperature(ref byte ComAdr,
                                             ref byte PlusMinus,
                                             ref byte Temperature,
                                             int frmComPortindex);

        [DllImport(DLLNAME, CallingConvention = CallingConvention.StdCall)]
        public static extern int MeasureReturnLoss(ref byte ComAdr,
                                             byte[] TestFreq,
                                             byte Ant,
                                             ref byte ReturnLoss,
                                             int frmComPortindex);

    }
}
