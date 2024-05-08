using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Reader;
using Service_RFID_BIG_READER.Reader;
using Service_RFID_BIG_READER.Reader.Rfid_Fonkan_E710;
using Service_RFID_BIG_READER.Util;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    internal class Fonkan_E710_DLLProject : InterfaceRfidAPI
    {
        private ReaderMethod reader;

        private ReaderSetting m_curSetting = new ReaderSetting();

        private Dictionary<string, TagInfo> _tagInfoNgDic = new Dictionary<string, TagInfo>();
        private Dictionary<string, TagInfo> _tagInfoXeDic = new Dictionary<string, TagInfo>();
        private Dictionary<string, string> _tagCheck = new Dictionary<string, string>(); // <string, string>
                                                                                         // <epc, atnNum_pass>

        private int m_InvExeTime = -1;
        private int m_InvCmdInterval = 0;
        private int tagbufferCount = 0
            ;
        private Options.ReaderAntTypeForFonkanE710 antType;

        private TagDB tagdb = null;
        private TagDB tagOpDb = null;
        private bool flagInventory = true;
        private bool flagReadTid = false;
        private bool isFastInv = true;
        private bool doingFastInv = false;
        private bool Inventorying = true;
        private bool isRealInv = false;
        private bool doingRealInv = false;
        private bool isBufferInv = false;
        private bool doingBufferInv = false;
        private bool needGetBuffer = false;

        private bool m_setOutputPower = false;
        private bool m_setWorkAnt = false;

        private bool invTargetB = true;

        private byte TIDFlag = 0;
        private byte power;
        byte btWorkAntenna = 0;

        bool useAntG1 = true;


        public Fonkan_E710_DLLProject()
        {
            reader = new ReaderMethod();
            tagdb = new TagDB();
            TIDFlag = Convert.ToByte(ConfigurationManager.AppSettings["TIDFlag"]);
            m_InvCmdInterval = Convert.ToInt32(ConfigurationManager.AppSettings["maxExecTime"]);
            antType = (Options.ReaderAntTypeForFonkanE710)Convert.ToInt16(ConfigurationManager.AppSettings["antenna"]);
            power = Convert.ToByte(ConfigurationManager.AppSettings["power"]);
            //The callback function
            reader.AnalyCallback = AnalyData;
            reader.SendCallback = SendData;
            reader.ReceiveCallback = RecvData;
            reader.ErrCallback = OnError;
        }

        private void SetBeeperMode()
        {
            byte btBeeperMode = Convert.ToByte(ConfigurationManager.AppSettings["beepEn"]);

            if (btBeeperMode == 1)
            {
                btBeeperMode = 0x02;
            }
            else btBeeperMode = 0x00;
            reader.SetBeeperMode(m_curSetting.btReadId, btBeeperMode);
            m_curSetting.btBeeperMode = btBeeperMode;
        }

        private void SetOutputPower()
        {
            try
            {
                if (antType == Options.ReaderAntTypeForFonkanE710.ANT_TYPE_16)
                {
                    m_setOutputPower = true;
                    cmdSwitchAntG1();
                }
                else if (antType == Options.ReaderAntTypeForFonkanE710.ANT_TYPE_8)
                {
                    byte[] OutputPower = new byte[8];
                    OutputPower[0] = power;
                    OutputPower[1] = power;
                    OutputPower[2] = power;
                    OutputPower[3] = power;
                    OutputPower[4] = power;
                    OutputPower[5] = power;
                    OutputPower[6] = power;
                    OutputPower[7] = power;
                    reader.SetOutputPower(m_curSetting.btReadId, OutputPower);
                }
                else if (antType == Options.ReaderAntTypeForFonkanE710.ANT_TYPE_4)
                {
                    byte[] OutputPower = new byte[4];
                    OutputPower[0] = power;
                    OutputPower[1] = power;
                    OutputPower[2] = power;
                    OutputPower[3] = power;
                    reader.SetOutputPower(m_curSetting.btReadId, OutputPower);
                }
                else
                {
                    byte[] OutputPower = new byte[1];
                    OutputPower[0] = power;
                    reader.SetOutputPower(m_curSetting.btReadId, OutputPower);
                }
            }
            catch (Exception ex)
            {
                Service1.WriteLog("SetOutputPower Erro " + ex.Message);
            }
        }

        private void cmdSwitchAntG1()
        {
            useAntG1 = true;
            tagdb.AntGroup = 0x00;
            cmdSwitchAntGroup(tagdb.AntGroup);
        }
        private void cmdSwitchAntG2()
        {
            useAntG1 = false;
            tagdb.AntGroup = 0x01;
            cmdSwitchAntGroup(tagdb.AntGroup);
        }

        private void cmdSwitchAntGroup(byte groupid)
        {
            int writeIndex = 0;
            byte[] rawData = new byte[256];
            rawData[writeIndex++] = 0xA0; // hdr

            rawData[writeIndex++] = 0x03; // len minLen = 3

            rawData[writeIndex++] = m_curSetting.btReadId; // addr

            rawData[writeIndex++] = (byte)CMD.cmd_set_antenna_group; // cmd

            rawData[writeIndex++] = groupid; // groupId G1=0x00, g2=0x01

            int msgLen = writeIndex + 1;
            rawData[1] = (byte)(msgLen - 2); // except hdr+len
                                             //Console.WriteLine("cmdSwitchAntGroup writeIndex={0}, msgLen={0}, len={2}", writeIndex, msgLen, rawData[1]);

            rawData[writeIndex] = ReaderUtils.CheckSum(rawData, 0, msgLen - 1); // check
            Array.Resize(ref rawData, msgLen);
            _ = reader.SendMessage(rawData);
        }

        public bool Connect(string port)
        {
            string strException = string.Empty;
            int nBaudrate = 115200;

            int nRet = reader.OpenCom(port, nBaudrate, out strException);
            if (nRet != 0)
                return false;
            SetBeeperMode();
            Thread.Sleep(1000);
            SetOutputPower();
            return true;
        }

        public bool Disconnect(Options.ConnectType connectType)
        {
            throw new NotImplementedException();
        }

        private void cmdFastInventorySend(bool antG1)
        {
            int writeIndex = 0;
            byte[] rawData = new byte[256];
            rawData[writeIndex++] = 0xA0; // hdr
            rawData[writeIndex++] = 0x03; // len minLen = 3
            rawData[writeIndex++] = m_curSetting.btReadId; // addr
            rawData[writeIndex++] = (byte)CMD.cmd_fast_switch_ant_inventory; // cmd

            // data
            if (antG1)
            {
                for (int i = 0; i < 8; i++)
                {
                    rawData[writeIndex++] = (byte)(i); // Vị trí antena VD: 0, 1, 2, 3, ,4
                    if (ConfigMapping.ARR_ANTENNA_IN.Contains((byte)i) || ConfigMapping.ARR_ANTENNA_OUT.Contains((byte)i))
                        rawData[writeIndex++] = 1; // Antena có bật hay không, 1 bật, 0 tắt
                    else
                        rawData[writeIndex++] = 0; // Antena có bật hay không, 1 bật, 0 tắt
                }
            }
            else
            {
                for (int i = 8; i < 16; i++)
                {
                    rawData[writeIndex++] = (byte)(i - 8); // Vị trí antena VD: 0, 1, 2, 3, ,4
                    if (ConfigMapping.ARR_ANTENNA_IN.Contains((byte)i) || ConfigMapping.ARR_ANTENNA_OUT.Contains((byte)i))
                        rawData[writeIndex++] = 1; // Antena có bật hay không, 1 bật, 0 tắt
                    else
                        rawData[writeIndex++] = 0; // Antena có bật hay không, 1 bật, 0 tắt
                }
            }
            //Console.WriteLine("antType8/16 end [G{0}]", useAntG1 ? "1" : "2");

            rawData[writeIndex++] = 0; // Interval, 0 ms

            rawData[writeIndex++] = 1; // Repeat
            int msgLen = writeIndex + 1;
            rawData[1] = (byte)(msgLen - 2); // except hdr+len
                                             //Console.WriteLine("FastInv writeIndex={0}, msgLen={0}, len={2}", writeIndex, msgLen, rawData[1]);

            rawData[writeIndex] = ReaderUtils.CheckSum(rawData, 0, msgLen - 1); // check
            Array.Resize(ref rawData, msgLen);
            _ = reader.SendMessage(rawData);
        }

        private bool checkFastInvAntG1Count()
        {
            for (int i = 0; i < 8; i++)
            {
                if (ConfigMapping.ARR_ANTENNA_IN.Contains((byte)i) || ConfigMapping.ARR_ANTENNA_OUT.Contains((byte)i))
                {
                    useAntG1 = true;
                    return true;
                }
            }
            useAntG1 = false;
            return false;
        }

        private bool checkFastInvAntG2Count()
        {
            for (int i = 8; i < 16; i++)
            {
                if (ConfigMapping.ARR_ANTENNA_IN.Contains((byte)i) || ConfigMapping.ARR_ANTENNA_OUT.Contains((byte)i))
                {
                    useAntG1 = false;
                    return true;
                }
            }
            useAntG1 = true;
            return false;
        }

        private void ReadTid()
        {
            lock (_tagCheck)
            {
                if (_tagCheck.Any())
                {
                    foreach (KeyValuePair<string, string> keyValuePair in _tagCheck)
                    {
                        string[] arS = keyValuePair.Value.Split('_');
                        SetTagOpWorkAnt((byte)(Convert.ToInt32(arS[0]) - 1));
                        Thread.Sleep(10);
                        byte[] btAryEpc = ReaderUtils.FromHex(keyValuePair.Key);
                        reader.SetAccessEpcMatch(m_curSetting.btReadId, 0x00, Convert.ToByte(btAryEpc.Length), btAryEpc);
                        Thread.Sleep(10);
                        try
                        {
                            byte btMemBank = 0x02; // TID bank
                            byte btWordAdd = 0x02; // Address
                            byte btWordCnt = 0x04; // Length
                            if (btWordCnt <= 0)
                            {
                                return;
                            }
                            byte[] accessPw = ReaderUtils.FromHex(arS[1]);

                            reader.ReadTag(m_curSetting.btReadId, btMemBank, btWordAdd, btWordCnt, accessPw);

                            _tagCheck.Remove(keyValuePair.Key);
                            if (!_tagCheck.Any()) return;
                        }
                        catch (Exception ex)
                        {
                            Service1.WriteLog(ex.Message);
                        }
                    }
                }
            }
        }

        public void Inventory()
        {
            if (m_setOutputPower) return;
            if (m_InvExeTime == 0)
                return;
            try
            {
               /* if (flagReadTid)
                {*/
                    string[] arS = "16_00001111".Split('_');
                   // SetTagOpWorkAnt((byte)(Convert.ToInt32(arS[0]) - 1));
                    byte[] btAryEpc = ReaderUtils.FromHex("0D742810BFFF4BD884A5D9A7");
                   // reader.SetAccessEpcMatch(m_curSetting.btReadId, 0x00, Convert.ToByte(btAryEpc.Length), btAryEpc);
                    try
                    {
                        byte btMemBank = 0x00; // TID bank
                        byte btWordAdd = 0x02; // Address
                        byte btWordCnt = 0x02; // Length
                        if (btWordCnt <= 0)
                        {
                            return;
                        }
                        byte[] accessPw = ReaderUtils.FromHex(arS[1]);

                        reader.ReadTag(m_curSetting.btReadId, btMemBank, btWordAdd, btWordCnt, accessPw);
                    }
                    catch (Exception ex)
                    {
                        Service1.WriteLog(ex.Message);
                    }
                    flagReadTid = false;
                 //   return;
                //}
                if (flagInventory)
                {
                    if (m_InvExeTime == -1) 
                    {
                        doingFastInv = true;
                        if (useAntG1)
                        {
                            if (checkFastInvAntG2Count())
                            {
                                cmdSwitchAntG2();
                            }
                            else
                            {
                                cmdFastInventorySend(useAntG1);
                            }
                        }
                        else //if (useAntG2)
                        {
                            if (checkFastInvAntG1Count())
                            {
                                cmdSwitchAntG1();
                            }
                            else
                            {
                                cmdFastInventorySend(useAntG1);
                            }
                        }
                        flagInventory = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Service1.WriteLog("E710 Inventory" + ex.Message);
            }
        }
        #region Callback func

        #region AnalyData

        private bool m_bDisplayLog = false;

        private void SendData(object sender, byte[] data)
        {
        }

        private void RecvData(object sender, TransportDataEventArgs e)
        {
        }

        private void OnError(object sender, ErrorReceivedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine(e.Err.Message);
        }

        private void AnalyData(object sender, MessageTran msgTran)
        {
            //if (m_bDisplayLog)
            //{
            //    string strLog = string.Format("{0}:{1}", FindResource("TransportRecv"), ReaderUtils.ToHex(msgTran.AryTranData, "", " "));
            //    //Console.WriteLine("<--  {0}", strLog);
            //    WriteLog(lrtxtDataTran, strLog, 2);
            //}
            if (msgTran.PacketType != 0xA0)
            {
                return;
            }
            switch (msgTran.Cmd)
            {
                #region 0x6x
                case 0x60:
                    ProcessReadGpioValue(msgTran);
                    break;
                case 0x61:
                    ProcessWriteGpioValue(msgTran);
                    break;
                case 0x62:
                    ProcessSetAntDetector(msgTran);
                    break;
                case 0x63:
                    ProcessGetAntDetector(msgTran);
                    break;
                case 0x66:
                    ProcessSetTempOutpower(msgTran);
                    break;
                case 0x67:
                    ProcessSetReaderIdentifier(msgTran);
                    break;
                case 0x68:
                    ProcessGetReaderIdentifier(msgTran);
                    break;
                case 0x69:
                    ProcessSetProfile(msgTran);
                    break;
                case 0x6A:
                    ProcessGetProfile(msgTran);
                    break;
                case 0x6c:
                    ProcessSetReaderAntGroup(msgTran);
                    break;
                case 0x6d:
                    ProcessGetReaderAntGroup(msgTran);
                    break;
                #endregion //0x6x

                #region 0x7x
                case 0x71:
                    ProcessSetUartBaudrate(msgTran);
                    break;
                case 0x72:
                    ProcessGetFirmwareVersion(msgTran);
                    break;
                case 0x73:
                    ProcessSetReadAddress(msgTran);
                    break;
                case 0x74:
                    ProcessSetWorkAntenna(msgTran);
                    break;
                case 0x75:
                    ProcessGetWorkAntenna(msgTran);
                    break;
                case 0x76:
                    ProcessSetOutputPower(msgTran);
                    break;
                case 0x97:
                case 0x77:
                    ProcessGetOutputPower(msgTran);
                    break;
                case 0x78:
                    ProcessSetFrequencyRegion(msgTran);
                    break;
                case 0x79:
                    ProcessGetFrequencyRegion(msgTran);
                    break;
                case 0x7A:
                    ProcessSetBeeperMode(msgTran);
                    break;
                case 0x7B:
                    ProcessGetReaderTemperature(msgTran);
                    break;
                case 0x7C:
                    ProcessSetDrmMode(msgTran);
                    break;
                case 0x7D:
                    ProcessGetDrmMode(msgTran);
                    break;
                case 0x7E:
                    ProcessGetImpedanceMatch(msgTran);
                    break;
                #endregion //0x7x

                #region 0x8x
                case 0x80:
                    ProcessInventory(msgTran);
                    break;
                case 0x81:
                    ProcessReadTag(msgTran);
                    break;
                case 0x82:
                case 0x94:
                    ProcessWriteTag(msgTran);
                    break;
                case 0x83:
                    ProcessLockTag(msgTran);
                    break;
                case 0x84:
                    ProcessKillTag(msgTran);
                    break;
                case 0x85:
                    ProcessSetAccessEpcMatch(msgTran);
                    break;
                case 0x86:
                    ProcessGetAccessEpcMatch(msgTran);
                    break;
                case 0x89:
                case 0x8B:
                    ProcessInventoryReal(msgTran);
                    break;
                case 0x8A:
                    ProcessFastSwitch(msgTran);
                    break;
                case 0x8C:
                case 0x8D:
                    ProcessSetMonzaStatus(msgTran);
                    break;
                case 0x8E:
                    ProcessGetMonzaStatus(msgTran);
                    break;
                #endregion //0x8x

                #region 0x9x
                case 0x90:
                    ProcessGetInventoryBuffer(msgTran);
                    break;
                case 0x91:
                    ProcessGetAndResetInventoryBuffer(msgTran);
                    break;
                case 0x92:
                    ProcessGetInventoryBufferTagCount(msgTran);
                    break;
                case 0x93:
                    ProcessResetInventoryBuffer(msgTran);
                    break;
                case 0x98:
                    ProcessTagMask(msgTran);
                    break;
                #endregion //0x9x

                #region 0xAx
                case 0xAA:
                    ProcessGetInternalVersion(msgTran);
                    break;
                #endregion //0xAx

                #region 0xBx
                case 0xb0:
                    ProcessInventoryISO18000(msgTran);
                    break;
                case 0xb1:
                    ProcessReadTagISO18000(msgTran);
                    break;
                case 0xb2:
                    ProcessWriteTagISO18000(msgTran);
                    break;
                case 0xb3:
                    ProcessLockTagISO18000(msgTran);
                    break;
                case 0xb4:
                    ProcessQueryISO18000(msgTran);
                    break;
                #endregion //0xBx
                default:
                    break;
            }
        }

        private void ProcessReadGpioValue(MessageTran msgTran) { }
        private void ProcessWriteGpioValue(MessageTran msgTran) { }
        private void ProcessSetAntDetector(MessageTran msgTran) { }
        private void ProcessGetAntDetector(MessageTran msgTran) { }
        private void ProcessSetTempOutpower(MessageTran msgTran) { }
        private void ProcessSetReaderIdentifier(MessageTran msgTran) { }
        private void ProcessGetReaderIdentifier(MessageTran msgTran) { }
        private void ProcessSetProfile(MessageTran msgTran) { }
        private void ProcessGetProfile(MessageTran msgTran) { }
        private void ProcessSetReaderAntGroup(MessageTran msgTran)
        {
            string strErrorCode;
            if (msgTran.AryData.Length == 1)
            {
                if (msgTran.AryData[0] == 0x10)
                {
                    m_curSetting.btReadId = msgTran.ReadId;

                    if (m_setWorkAnt)
                    {
                        m_setWorkAnt = false;
                        //btWorkAntenna = (byte)cmbWorkAnt.SelectedIndex;
                        if (btWorkAntenna >= 0x08)
                            btWorkAntenna = (byte)((btWorkAntenna & 0xFF) - 0x08);
                        reader.SetWorkAntenna(m_curSetting.btReadId, btWorkAntenna);
                        m_curSetting.btWorkAntenna = btWorkAntenna;
                        return;
                    }

                    if (m_setOutputPower)
                    {
                        if (tagdb.AntGroup == 0x00)
                        {
                            byte[] OutputPower = new byte[8];
                            OutputPower[0] = power;
                            OutputPower[1] = power;
                            OutputPower[2] = power;
                            OutputPower[3] = power;
                            OutputPower[4] = power;
                            OutputPower[5] = power;
                            OutputPower[6] = power;
                            OutputPower[7] = power;
                            reader.SetOutputPower(m_curSetting.btReadId, OutputPower);
                        }
                        else
                        {
                            byte[] OutputPower = new byte[8];
                            OutputPower[0] = power;
                            OutputPower[1] = power;
                            OutputPower[2] = power;
                            OutputPower[3] = power;
                            OutputPower[4] = power;
                            OutputPower[5] = power;
                            OutputPower[6] = power;
                            OutputPower[7] = power;

                            reader.SetOutputPower(m_curSetting.btReadId, OutputPower);
                        }
                    }
                }
                if (doingRealInv || doingBufferInv)
                {
                    reader.SetWorkAntenna(m_curSetting.btReadId, m_curSetting.btWorkAntenna);
                }
                else if (doingFastInv)
                {
                    cmdFastInventorySend(useAntG1);
                }
                return;
            }
            else
            {
                strErrorCode = ReaderUtils.FormatErrorCode(msgTran.AryData[0]);
            }

        }
        private void ProcessGetReaderAntGroup(MessageTran msgTran) { }
        private void ProcessSetUartBaudrate(MessageTran msgTran) { }
        private void ProcessGetFirmwareVersion(MessageTran msgTran)
        {
            string strErrorCode = string.Empty;

            if (msgTran.AryData.Length == 2)
            {
                m_curSetting.btMajor = msgTran.AryData[0];
                m_curSetting.btMinor = msgTran.AryData[1];
                m_curSetting.btReadId = msgTran.ReadId;

                RefreshReadSetting((CMD)msgTran.Cmd);
                //  cmdGetInternalVersion();

                return;
            }
        }
        private void ProcessSetReadAddress(MessageTran msgTran) { }
        private void ProcessSetWorkAntenna(MessageTran msgTran) { }
        private void ProcessGetWorkAntenna(MessageTran msgTran) { }

        private void SetTagOpWorkAnt(byte antNum)
        {
            m_setWorkAnt = true;
            btWorkAntenna = antNum;
            if (btWorkAntenna >= 0x08)
                tagdb.AntGroup = 0x01;
            else
                tagdb.AntGroup = 0x00;
            reader.SetReaderAntGroup(m_curSetting.btReadId, tagdb.AntGroup);
        }
        private void ProcessSetOutputPower(MessageTran msgTran)
        {
            string strErrorCode = string.Empty;

            if (msgTran.AryData.Length == 1)
            {
                if (msgTran.AryData[0] == 0x10)
                {
                    m_curSetting.btReadId = msgTran.ReadId;

                    if (m_setOutputPower)
                    {
                        if (antType == Options.ReaderAntTypeForFonkanE710.ANT_TYPE_16)
                        {
                            if (useAntG1)
                            {
                                cmdSwitchAntG2();
                            }
                            else
                            {
                                m_setOutputPower = false;
                                useAntG1 = true;
                                SetTagOpWorkAnt(0);
                            }
                        }
                        else
                        {
                            m_setOutputPower = false;
                        }
                    }
                    return;
                }
                else
                {
                    strErrorCode = ReaderUtils.FormatErrorCode(msgTran.AryData[0]);
                }
            }
        }
        private void ProcessGetOutputPower(MessageTran msgTran) { }
        private void ProcessSetFrequencyRegion(MessageTran msgTran) { }
        private void ProcessGetFrequencyRegion(MessageTran msgTran) { }
        private void ProcessSetBeeperMode(MessageTran msgTran)
        {

        }
        private void ProcessGetReaderTemperature(MessageTran msgTran) { }
        private void ProcessSetDrmMode(MessageTran msgTran) { }
        private void ProcessGetDrmMode(MessageTran msgTran) { }
        private void ProcessGetImpedanceMatch(MessageTran msgTran) { }
        private void ProcessInventory(MessageTran msgTran) { }
        private void ProcessReadTag(MessageTran msgTran)
        {
            lock (tagdb)
            {
                string strErrorCode = string.Empty;
                if (msgTran.AryData.Length == 1)
                {
                    strErrorCode = ReaderUtils.FormatErrorCode(msgTran.AryData[0]);
                    if(msgTran.AryData[0] == 0x37)
                    {
                        System.Diagnostics.Debug.WriteLine(strErrorCode);
                    }
                }
                else
                {
                    Tag tag = new Tag(msgTran.AryData, msgTran.Cmd, tagdb.AntGroup);
                    Task.Run(() => { 
                    System.Diagnostics.Debug.WriteLine($"{tag.Antenna} {tag.Rssi} {tag.EPC.Replace(" ", "")} {tag.Data.Replace(" ", "")}");
                    });
                }
                flagInventory = true;
            }
        }
        private void ProcessWriteTag(MessageTran msgTran) { }
        private void ProcessLockTag(MessageTran msgTran) { }
        private void ProcessKillTag(MessageTran msgTran) { }
        private void ProcessSetAccessEpcMatch(MessageTran msgTran) { }
        private void ProcessGetAccessEpcMatch(MessageTran msgTran) { }
        private void ProcessInventoryReal(MessageTran msgTran) { }
        private void ProcessFastSwitch(MessageTran msgTran)
        {
            string strErrorCode = string.Empty;

            if (msgTran.AryData.Length == 1)
            {
            }
            else if (msgTran.AryData.Length == 2)
            {
            }
            else if (msgTran.AryData.Length == 7)
            {
                flagReadTid = true;
            }
            else
            {
                if (doingFastInv)
                {
                    parseInvTag(false, msgTran.AryData, (byte)CMD.cmd_fast_switch_ant_inventory);

                }
            }
        }

        private void SetMaxMinRSSI(int nRSSI)
        {
            if (tagdb.MinRSSI == 0 && tagdb.MinRSSI == 0)
            {
                tagdb.MaxRSSI = nRSSI;
                tagdb.MinRSSI = nRSSI;
            }
            else
            {
                if (tagdb.MaxRSSI < nRSSI)
                {
                    tagdb.MaxRSSI = nRSSI;
                }

                if (tagdb.MinRSSI > nRSSI)
                {
                    tagdb.MinRSSI = nRSSI;
                }
            }
        }

        private void parseInvTag(bool readPhase, byte[] data, byte cmd)
        {
            lock (tagdb)
            {
                Tag tag = new Tag(data, readPhase, cmd, tagdb.AntGroup);
                string epc = tag.EPC.Replace(" ", "");
                Task.Run(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"{epc} {tag.Antenna}");
                });
                //SetMaxMinRSSI(Convert.ToInt32(tag.Rssi));

                /*if (_tagCheck.ContainsKey(epc))
                    return;
                string password = "00000000";
                _tagCheck.Add(epc, $"{tag.Antenna}_{password}");
                ReadTid();*/
                /* txtFastMaxRssi.Text = tagdb.MaxRSSI + "dBm";
                 txtFastMinRssi.Text = tagdb.MinRSSI + "dBm";*/
            }
        }
        private void ProcessSetMonzaStatus(MessageTran msgTran) { }
        private void ProcessGetMonzaStatus(MessageTran msgTran) { }
        private void ProcessGetInventoryBuffer(MessageTran msgTran) { }
        private void ProcessGetAndResetInventoryBuffer(MessageTran msgTran) { }
        private void ProcessGetInventoryBufferTagCount(MessageTran msgTran) { }
        private void ProcessResetInventoryBuffer(MessageTran msgTran) { }
        private void ProcessTagMask(MessageTran msgTran) { }
        private void ProcessGetInternalVersion(MessageTran msgTran) { }
        private void ProcessInventoryISO18000(MessageTran msgTran) { }
        private void ProcessReadTagISO18000(MessageTran msgTran) { }
        private void ProcessWriteTagISO18000(MessageTran msgTran) { }
        private void ProcessLockTagISO18000(MessageTran msgTran) { }
        private void ProcessQueryISO18000(MessageTran msgTran) { }
        #endregion

        #region RefreshReadSetting
        private void RefreshReadSetting(CMD btCmd)
        {

            switch (btCmd)
            {
                /* case CMD.cmd_get_rf_link_profile:
                     {
                         if (m_curSetting.btLinkProfile == 0xd0)
                         {
                             rdbProfile0.Checked = true;
                         }
                         else if (m_curSetting.btLinkProfile == 0xd1)
                         {
                             rdbProfile1.Checked = true;
                         }
                         else if (m_curSetting.btLinkProfile == 0xd2)
                         {
                             rdbProfile2.Checked = true;
                         }
                         else if (m_curSetting.btLinkProfile == 0xd3)
                         {
                             rdbProfile3.Checked = true;
                         }
                         else
                         {
                         }
                     }
                     break;
                 case CMD.cmd_get_reader_identifier:
                     {
                         htbGetIdentifier.Text = m_curSetting.btReaderIdentifier;
                     }
                     break;*/
                case CMD.cmd_get_firmware_version:
                    {
                        string s = m_curSetting.btMajor.ToString() + "." + m_curSetting.btMinor.ToString();
                    }
                    break;
                /* case CMD.cmd_get_work_antenna:
                     {
                         if (antType16.Checked && tagdb.AntGroup == 0x01)
                         {
                             cmbWorkAnt.SelectedIndex = m_curSetting.btWorkAntenna + 0x08;
                             cmbbxTagOpWorkAnt.SelectedIndex = m_curSetting.btWorkAntenna + 0x08;
                         }
                         else
                         {
                             if (!antType16.Checked && (m_curSetting.btWorkAntenna > 4 && !antType8.Checked))
                             {
                                 antType8.Checked = true;
                             }
                             cmbWorkAnt.SelectedIndex = m_curSetting.btWorkAntenna;
                             cmbbxTagOpWorkAnt.SelectedIndex = m_curSetting.btWorkAntenna;
                         }
                     }
                     break;
                 case CMD.cmd_get_output_power:
                     {
                         if (antType4.Checked)
                         {
                             if (m_curSetting.btOutputPower != 0 && m_curSetting.btOutputPowers == null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_2.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_3.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_4.Text = m_curSetting.btOutputPower.ToString();

                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }
                             else if (m_curSetting.btOutputPowers != null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPowers[0].ToString();
                                 tb_outputpower_2.Text = m_curSetting.btOutputPowers[1].ToString();
                                 tb_outputpower_3.Text = m_curSetting.btOutputPowers[2].ToString();
                                 tb_outputpower_4.Text = m_curSetting.btOutputPowers[3].ToString();

                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }

                         }

                         if (antType1.Checked)
                         {
                             if (m_curSetting.btOutputPower != 0 && m_curSetting.btOutputPowers == null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPower.ToString();
                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }
                             else if (m_curSetting.btOutputPowers != null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPowers[0].ToString();
                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }
                         }

                     }
                     break;
                 case CMD.cmd_get_output_power_eight:
                     {
                         if (antType8.Checked)
                         {

                             if (m_curSetting.btOutputPower != 0 && m_curSetting.btOutputPowers == null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_2.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_3.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_4.Text = m_curSetting.btOutputPower.ToString();


                                 tb_outputpower_5.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_6.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_7.Text = m_curSetting.btOutputPower.ToString();
                                 tb_outputpower_8.Text = m_curSetting.btOutputPower.ToString();

                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }
                             else if (m_curSetting.btOutputPowers != null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPowers[0].ToString();
                                 tb_outputpower_2.Text = m_curSetting.btOutputPowers[1].ToString();
                                 tb_outputpower_3.Text = m_curSetting.btOutputPowers[2].ToString();
                                 tb_outputpower_4.Text = m_curSetting.btOutputPowers[3].ToString();
                                 tb_outputpower_5.Text = m_curSetting.btOutputPowers[4].ToString();
                                 tb_outputpower_6.Text = m_curSetting.btOutputPowers[5].ToString();
                                 tb_outputpower_7.Text = m_curSetting.btOutputPowers[6].ToString();
                                 tb_outputpower_8.Text = m_curSetting.btOutputPowers[7].ToString();

                                 m_curSetting.btOutputPower = 0;
                                 m_curSetting.btOutputPowers = null;
                             }
                         }
                         else if (antType16.Checked)
                         {
                             if (m_curSetting.btOutputPowers != null)
                             {
                                 tb_outputpower_1.Text = m_curSetting.btOutputPowers[0].ToString();
                                 tb_outputpower_2.Text = m_curSetting.btOutputPowers[1].ToString();
                                 tb_outputpower_3.Text = m_curSetting.btOutputPowers[2].ToString();
                                 tb_outputpower_4.Text = m_curSetting.btOutputPowers[3].ToString();
                                 tb_outputpower_5.Text = m_curSetting.btOutputPowers[4].ToString();
                                 tb_outputpower_6.Text = m_curSetting.btOutputPowers[5].ToString();
                                 tb_outputpower_7.Text = m_curSetting.btOutputPowers[6].ToString();
                                 tb_outputpower_8.Text = m_curSetting.btOutputPowers[7].ToString();

                                 if (m_curSetting.btOutputPowers.Length >= 16)
                                 {
                                     tb_outputpower_9.Text = m_curSetting.btOutputPowers[8].ToString();
                                     tb_outputpower_10.Text = m_curSetting.btOutputPowers[9].ToString();
                                     tb_outputpower_11.Text = m_curSetting.btOutputPowers[10].ToString();
                                     tb_outputpower_12.Text = m_curSetting.btOutputPowers[11].ToString();
                                     tb_outputpower_13.Text = m_curSetting.btOutputPowers[12].ToString();
                                     tb_outputpower_14.Text = m_curSetting.btOutputPowers[13].ToString();
                                     tb_outputpower_15.Text = m_curSetting.btOutputPowers[14].ToString();
                                     tb_outputpower_16.Text = m_curSetting.btOutputPowers[15].ToString();
                                     m_curSetting.btOutputPowers = null;
                                 }
                             }
                         }
                     }
                     break;
                 case CMD.cmd_get_frequency_region:
                     {
                         switch (tagdb.CurRegion)
                         {
                             case 0x01:
                                 {
                                     cbUserDefineFreq.Checked = false;
                                     textStartFreq.Text = "";
                                     TextFreqInterval.Text = "";
                                     textFreqQuantity.Text = "";
                                     rdbRegionFcc.Checked = true;
                                     cmbFrequencyStart.SelectedIndex = Convert.ToInt32(tagdb.FrequencyStart) - 7;
                                     cmbFrequencyEnd.SelectedIndex = Convert.ToInt32(tagdb.FrequencyEnd) - 7;
                                 }
                                 break;
                             case 0x02:
                                 {
                                     cbUserDefineFreq.Checked = false;
                                     textStartFreq.Text = "";
                                     TextFreqInterval.Text = "";
                                     textFreqQuantity.Text = "";
                                     rdbRegionEtsi.Checked = true;
                                     cmbFrequencyStart.SelectedIndex = Convert.ToInt32(tagdb.FrequencyStart);
                                     cmbFrequencyEnd.SelectedIndex = Convert.ToInt32(tagdb.FrequencyEnd);
                                 }
                                 break;
                             case 0x03:
                                 {
                                     cbUserDefineFreq.Checked = false;
                                     textStartFreq.Text = "";
                                     TextFreqInterval.Text = "";
                                     textFreqQuantity.Text = "";
                                     rdbRegionChn.Checked = true;
                                     cmbFrequencyStart.SelectedIndex = Convert.ToInt32(tagdb.FrequencyStart) - 43;
                                     cmbFrequencyEnd.SelectedIndex = Convert.ToInt32(tagdb.FrequencyEnd) - 43;
                                 }
                                 break;
                             case 0x04:
                                 {
                                     cbUserDefineFreq.Checked = true;
                                     rdbRegionChn.Checked = false;
                                     rdbRegionEtsi.Checked = false;
                                     rdbRegionFcc.Checked = false;
                                     cmbFrequencyStart.SelectedIndex = -1;
                                     cmbFrequencyEnd.SelectedIndex = -1;
                                     textStartFreq.Text = string.Format("{0}", tagdb.FrequencyStart);
                                     TextFreqInterval.Text = string.Format("{0}", tagdb.FrequencyInterval * 10);
                                     textFreqQuantity.Text = string.Format("{0}", tagdb.FreqQuantity);
                                 }
                                 break;
                             default:
                                 break;
                         }
                     }
                     break;
                 case CMD.cmd_get_reader_temperature:
                     {
                         string strTemperature = string.Empty;
                         if (m_curSetting.btPlusMinus == 0x0)
                         {
                             strTemperature = "-" + m_curSetting.btTemperature.ToString() + "℃";
                         }
                         else
                         {
                             strTemperature = m_curSetting.btTemperature.ToString() + "℃";
                         }
                         txtReaderTemperature.Text = strTemperature;
                     }
                     break;
                 case CMD.cmd_get_drm_mode:
                     {
                         *//*
                         if (m_curSetting.btDrmMode == 0x00)
                         {
                             rdbDrmModeClose.Checked = true;
                         }
                         else
                         {
                             rdbDrmModeOpen.Checked = true;
                         }
                          * *//*
                     }
                     break;
                 case CMD.cmd_get_rf_port_return_loss:
                     {
                         textReturnLoss.Text = m_curSetting.btAntImpedance.ToString() + " dB";
                     }
                     break;
                 case CMD.cmd_get_impinj_fast_tid:
                     {
                         if (m_curSetting.btMonzaStatus == 0x8D)
                         {
                             rdbMonzaOn.Checked = true;
                         }
                         else
                         {
                             rdbMonzaOff.Checked = true;
                         }
                     }
                     break;
                 case CMD.cmd_read_gpio_value:
                     {
                         if (m_curSetting.btGpio1Value == 0x00)
                         {
                             rdbGpio1Low.Checked = true;
                         }
                         else
                         {
                             rdbGpio1High.Checked = true;
                         }

                         if (m_curSetting.btGpio2Value == 0x00)
                         {
                             rdbGpio2Low.Checked = true;
                         }
                         else
                         {
                             rdbGpio2High.Checked = true;
                         }
                     }
                     break;
                 case CMD.cmd_get_ant_connection_detector:
                     {
                         tbAntDectector.Text = m_curSetting.btAntDetector.ToString();
                     }
                     break;*/
                default:
                    break;
            }
        }
        #endregion

        #endregion


        public class ReaderUtils
        {
            private static bool useEnglish = true;
            ReaderUtils()
            {
            }
            public static byte[] StringArrayToByteArray(string[] strAryHex, int nLen)
            {
                if (strAryHex.Length < nLen)
                {
                    nLen = strAryHex.Length;
                }

                byte[] btAryHex = new byte[nLen];

                try
                {
                    int nIndex = 0;
                    foreach (string strTemp in strAryHex)
                    {
                        btAryHex[nIndex] = Convert.ToByte(strTemp, 16);
                        nIndex++;
                    }
                }
                catch (System.Exception ex)
                {

                }

                return btAryHex;
            }

            public static string ByteArrayToString(byte[] btAryHex, int nIndex, int nLen)
            {
                if (nIndex + nLen > btAryHex.Length)
                {
                    nLen = btAryHex.Length - nIndex;
                }

                string strResult = string.Empty;

                for (int nloop = nIndex; nloop < nIndex + nLen; nloop++)
                {
                    string strTemp = string.Format(" {0:X2}", btAryHex[nloop]);

                    strResult += strTemp;
                }

                return strResult;
            }

            /// <summary>
            /// Intercepts and converts a string to a specified length as an array of characters. Spaces are ignored
            /// </summary>
            /// <param name="strValue"></param>
            /// <param name="nLength"></param>
            /// <returns></returns>
            public static string[] StringToStringArray(string strValue, int nLength)
            {
                string[] strAryResult = null;

                if (!string.IsNullOrEmpty(strValue))
                {
                    System.Collections.ArrayList strListResult = new System.Collections.ArrayList();
                    string strTemp = string.Empty;
                    int nTemp = 0;

                    for (int nloop = 0; nloop < strValue.Length; nloop++)
                    {
                        if (strValue[nloop] == ' ')
                        {
                            continue;
                        }
                        else
                        {
                            nTemp++;

                            // Check whether the intercepted characters are between A~F and 0~9, or exit directly if not
                            System.Text.RegularExpressions.Regex reg = new System.Text.RegularExpressions.Regex(@"^(([A-F])*(\d)*)$");
                            if (!reg.IsMatch(strValue.Substring(nloop, 1)))
                            {
                                return strAryResult;
                            }

                            strTemp += strValue.Substring(nloop, 1);

                            // Determine whether the interception length has been reached
                            if ((nTemp == nLength) || (nloop == strValue.Length - 1 && !string.IsNullOrEmpty(strTemp)))
                            {
                                strListResult.Add(strTemp);
                                nTemp = 0;
                                strTemp = string.Empty;
                            }
                        }
                    }

                    if (strListResult.Count > 0)
                    {
                        strAryResult = new string[strListResult.Count];
                        strListResult.CopyTo(strAryResult);
                    }
                }

                return strAryResult;
            }
            public static string FormatCommException(CommExceptionCode code)
            {
                return (useEnglish ? FormatCommExceptionEN(code) : FormatCommExceptionCN(code));
            }

            public static string FormatCommExceptionEN(CommExceptionCode code)
            {
                string strErrorCode = "";
                switch (code)
                {
                    case CommExceptionCode.ConnectToServerFail:
                        strErrorCode = "Connect timeout，Failed to connect to the specified server";
                        break;
                    case CommExceptionCode.ConnectError:
                        strErrorCode = "Connect Error";
                        break;
                    case CommExceptionCode.DataRecvError:
                        strErrorCode = "Data Receive Error";
                        break;
                    case CommExceptionCode.DataSendError:
                        strErrorCode = "Data Send Error";
                        break;
                    case CommExceptionCode.ReconnectSuccess:
                        strErrorCode = "Reconnect Success";
                        break;
                    case CommExceptionCode.ReconnectFailed:
                        strErrorCode = "Reconnect Failed";
                        break;
                    case CommExceptionCode.TcpLogout:
                        strErrorCode = "Logout";
                        break;
                    case CommExceptionCode.NotTcpObj:
                        strErrorCode = "Not a tcp object";
                        break;
                    case CommExceptionCode.NotSerialObj:
                        strErrorCode = "Not a serial object";
                        break;
                    case CommExceptionCode.CommError:
                        strErrorCode = "Communication error";
                        break;
                }
                return strErrorCode;
            }

            public static string FormatCommExceptionCN(CommExceptionCode code)
            {
                string strErrorCode = "";
                switch (code)
                {
                    case CommExceptionCode.ConnectToServerFail:
                        strErrorCode = "连接超时，无法连接到指定的服务器";
                        break;
                    case CommExceptionCode.ConnectError:
                        strErrorCode = "连接异常";
                        break;
                    case CommExceptionCode.DataRecvError:
                        strErrorCode = "数据接收异常";
                        break;
                    case CommExceptionCode.DataSendError:
                        strErrorCode = "数据发送异常";
                        break;
                    case CommExceptionCode.ReconnectSuccess:
                        strErrorCode = "重连成功";
                        break;
                    case CommExceptionCode.ReconnectFailed:
                        strErrorCode = "重连失败";
                        break;
                    case CommExceptionCode.TcpLogout:
                        strErrorCode = "登出";
                        break;
                    case CommExceptionCode.NotTcpObj:
                        strErrorCode = "不是一个Tcp对象";
                        break;
                    case CommExceptionCode.NotSerialObj:
                        strErrorCode = "不是一个串口对象";
                        break;
                    case CommExceptionCode.CommError:
                        strErrorCode = "通讯异常";
                        break;
                }
                return strErrorCode;
            }

            public static string FormatErrorCode(byte btErrorCode)
            {
                return (useEnglish ? FormatErrorCodeEN(btErrorCode) : FormatErrorCodeCN(btErrorCode));
            }

            public static string FormatErrorCodeEN(byte btErrorCode)
            {
                string strErrorCode = "";
                switch (btErrorCode)
                {
                    case 0x10:
                        strErrorCode = "Command succeeded";
                        break;
                    case 0x11:
                        strErrorCode = "Command failed";
                        break;
                    case 0x20:
                        strErrorCode = "CPU reset error";
                        break;
                    case 0x21:
                        strErrorCode = "Turn on CW error";
                        break;
                    case 0x22:
                        strErrorCode = "Antenna is missing";
                        break;
                    case 0x23:
                        strErrorCode = "Write flash error";
                        break;
                    case 0x24:
                        strErrorCode = "Read flash error";
                        break;
                    case 0x25:
                        strErrorCode = "Set output power error";
                        break;
                    case 0x31:
                        strErrorCode = "Error occurred during inventory";
                        break;
                    case 0x32:
                        strErrorCode = "Error occurred during read";
                        break;
                    case 0x33:
                        strErrorCode = "Error occurred during write";
                        break;
                    case 0x34:
                        strErrorCode = "Error occurred during lock";
                        break;
                    case 0x35:
                        strErrorCode = "Error occurred during kill";
                        break;
                    case 0x36:
                        strErrorCode = "There is no tag to be operated";
                        break;
                    case 0x37:
                        strErrorCode = "Tag Inventoried but access failed";
                        break;
                    case 0x38:
                        strErrorCode = "Buffer is empty";
                        break;
                    case 0x40:
                        strErrorCode = "Access failed or wrong password";
                        break;
                    case 0x41:
                        strErrorCode = "Invalid parameter";
                        break;
                    case 0x42:
                        strErrorCode = "WordCnt is too long";
                        break;
                    case 0x43:
                        strErrorCode = "MemBank out of range";
                        break;
                    case 0x44:
                        strErrorCode = "Lock region out of range";
                        break;
                    case 0x45:
                        strErrorCode = "LockType out of range";
                        break;
                    case 0x46:
                        strErrorCode = "Invalid reader address";
                        break;
                    case 0x47:
                        strErrorCode = "AntennaID out of range";
                        break;
                    case 0x48:
                        strErrorCode = "Output power out of range";
                        break;
                    case 0x49:
                        strErrorCode = "Frequency region out of range";
                        break;
                    case 0x4A:
                        strErrorCode = "Baud rate out of range";
                        break;
                    case 0x4B:
                        strErrorCode = "Buzzer behavior out of range";
                        break;
                    case 0x4C:
                        strErrorCode = "EPC match is too long";
                        break;
                    case 0x4D:
                        strErrorCode = "EPC match length wrong";
                        break;
                    case 0x4E:
                        strErrorCode = "Invalid EPC match mode";
                        break;
                    case 0x4F:
                        strErrorCode = "Invalid frequency range";
                        break;
                    case 0x50:
                        strErrorCode = "Failed to receive RN16 from tag";
                        break;
                    case 0x51:
                        strErrorCode = "Invalid DRM mode";
                        break;
                    case 0x52:
                        strErrorCode = "PLL can not lock";
                        break;
                    case 0x53:
                        strErrorCode = "No response from RF chip";
                        break;
                    case 0x54:
                        strErrorCode = "Can't achieve desired output power level";
                        break;
                    case 0x55:
                        strErrorCode = "Can't authenticate firmware copyright";
                        break;
                    case 0x56:
                        strErrorCode = "Spectrum regulation wrong";
                        break;
                    case 0x57:
                        strErrorCode = "Output power is too low";
                        break;
                    case 0xFF:
                        strErrorCode = "Unknown error";
                        break;

                    default:
                        strErrorCode = "Unknown error";
                        break;
                }

                return strErrorCode;
            }

            public static string FormatErrorCodeCN(byte btErrorCode)
            {
                string strErrorCode = "";
                switch (btErrorCode)
                {
                    case 0x10:
                        strErrorCode = "命令已执行";
                        break;
                    case 0x11:
                        strErrorCode = "命令执行失败";
                        break;
                    case 0x20:
                        strErrorCode = "CPU 复位错误";
                        break;
                    case 0x21:
                        strErrorCode = "打开CW 错误";
                        break;
                    case 0x22:
                        strErrorCode = "天线未连接";
                        break;
                    case 0x23:
                        strErrorCode = "写Flash 错误";
                        break;
                    case 0x24:
                        strErrorCode = "读Flash 错误";
                        break;
                    case 0x25:
                        strErrorCode = "设置发射功率错误";
                        break;
                    case 0x31:
                        strErrorCode = "盘存标签错误";
                        break;
                    case 0x32:
                        strErrorCode = "读标签错误";
                        break;
                    case 0x33:
                        strErrorCode = "写标签错误";
                        break;
                    case 0x34:
                        strErrorCode = "锁定标签错误";
                        break;
                    case 0x35:
                        strErrorCode = "灭活标签错误";
                        break;
                    case 0x36:
                        strErrorCode = "无可操作标签错误";
                        break;
                    case 0x37:
                        strErrorCode = "成功盘存但访问失败";
                        break;
                    case 0x38:
                        strErrorCode = "缓存为空";
                        break;
                    case 0x40:
                        strErrorCode = "访问标签错误或访问密码错误";
                        break;
                    case 0x41:
                        strErrorCode = "无效的参数";
                        break;
                    case 0x42:
                        strErrorCode = "wordCnt 参数超过规定长度";
                        break;
                    case 0x43:
                        strErrorCode = "MemBank 参数超出范围";
                        break;
                    case 0x44:
                        strErrorCode = "Lock 数据区参数超出范围";
                        break;
                    case 0x45:
                        strErrorCode = "LockType 参数超出范围";
                        break;
                    case 0x46:
                        strErrorCode = "读卡器地址无效";
                        break;
                    case 0x47:
                        strErrorCode = "Antenna_id 超出范围";
                        break;
                    case 0x48:
                        strErrorCode = "输出功率参数超出范围";
                        break;
                    case 0x49:
                        strErrorCode = "射频规范区域参数超出范围";
                        break;
                    case 0x4A:
                        strErrorCode = "波特率参数超过范围";
                        break;
                    case 0x4B:
                        strErrorCode = "蜂鸣器设置参数超出范围";
                        break;
                    case 0x4C:
                        strErrorCode = "EPC 匹配长度越界";
                        break;
                    case 0x4D:
                        strErrorCode = "EPC 匹配长度错误";
                        break;
                    case 0x4E:
                        strErrorCode = "EPC 匹配参数超出范围";
                        break;
                    case 0x4F:
                        strErrorCode = "频率范围设置参数错误";
                        break;
                    case 0x50:
                        strErrorCode = "无法接收标签的RN16";
                        break;
                    case 0x51:
                        strErrorCode = "DRM 设置参数错误";
                        break;
                    case 0x52:
                        strErrorCode = "PLL 不能锁定";
                        break;
                    case 0x53:
                        strErrorCode = "射频芯片无响应";
                        break;
                    case 0x54:
                        strErrorCode = "输出达不到指定的输出功率";
                        break;
                    case 0x55:
                        strErrorCode = "版权认证未通过";
                        break;
                    case 0x56:
                        strErrorCode = "频谱规范设置错误";
                        break;
                    case 0x57:
                        strErrorCode = "输出功率过低";
                        break;
                    case 0xFF:
                        strErrorCode = "未知错误";
                        break;
                    default:
                        break;
                }

                return strErrorCode;
            }

            #region FromHex
            /// <summary>
            /// Convert human-readable hex string to byte array;
            /// e.g., 123456 or 0x123456 -> {0x12, 0x34, 0x56};
            /// </summary>
            /// <param name="hex">Human-readable hex string to convert</param>
            /// <returns>Byte array</returns>
            public static byte[] FromHex(string hex)
            {
                int prelen = 0;

                if (hex.StartsWith("0x") || hex.StartsWith("0X"))
                    prelen = 2;

                byte[] bytes = new byte[(hex.Length - prelen) / 2];

                for (int i = 0; i < bytes.Length; i++)
                {
                    string bytestring = hex.Substring(prelen + (2 * i), 2);
                    bytes[i] = byte.Parse(bytestring, System.Globalization.NumberStyles.HexNumber);
                }

                return bytes;
            }
            #endregion

            #region ToHex
            /// <summary>
            /// Convert byte array to human-readable hex string; e.g., {0x12, 0x34, 0x56} -> 123456
            /// </summary>
            /// <param name="bytes">Byte array to convert</param>
            /// <returns>Human-readable hex string</returns>
            public static string ToHex(byte[] bytes)
            {
                return ToHex(bytes, "0x", "");
            }

            /// <summary>
            /// Convert byte array to human-readable hex string; e.g., {0x12, 0x34, 0x56} -> 123456
            /// </summary>
            /// <param name="bytes">Byte array to convert</param>
            /// <param name="prefix">String to place before byte strings</param>
            /// <param name="separator">String to place between byte strings</param>
            /// <returns>Human-readable hex string</returns>
            public static string ToHex(byte[] bytes, string prefix, string separator)
            {
                if (null == bytes)
                    return "null";

                List<string> bytestrings = new List<string>();

                foreach (byte b in bytes)
                    bytestrings.Add(b.ToString("X2"));

                return prefix + String.Join(separator, bytestrings.ToArray());
            }

            /// <summary>
            /// Convert u16 array to human-readable hex string; e.g., {0x1234, 0x5678} -> 12345678
            /// </summary>
            /// <param name="words">u16 array to convert</param>
            /// <returns>Human-readable hex string</returns>
            public static string ToHex(UInt16[] words)
            {
                StringBuilder sb = new StringBuilder(4 * words.Length);

                foreach (UInt16 word in words)
                    sb.Append(word.ToString("X4"));

                return sb.ToString();
            }
            #endregion

            /// <summary>
            /// Extract unsigned 16-bit integer from big-endian byte string
            /// </summary>
            /// <param name="bytes">Source big-endian byte string</param>
            /// <param name="offset">Location to extract from.  Will be updated to post-decode offset.</param>
            /// <returns>Unsigned 16-bit integer</returns>
            public static UInt16 ToU16(byte[] bytes, ref int offset)
            {
                if (null == bytes) return default(byte);
                int hi = (UInt16)(bytes[offset++]) << 8;
                int lo = (UInt16)(bytes[offset++]);
                return (UInt16)(hi | lo);
            }
            #region ToU24
            public static UInt32 ToU24(byte[] bytes, ref int offset)
            {
                return (UInt32)(0
                    | ((UInt32)(bytes[offset++]) << 16)
                    | ((UInt32)(bytes[offset++]) << 8)
                    | ((UInt32)(bytes[offset++]) << 0)
                    );
            }
            #endregion

            #region ToU32
            /// <summary>
            /// Extract unsigned 32-bit integer from big-endian byte string
            /// </summary>
            /// <param name="bytes">Source big-endian byte string</param>
            /// <param name="offset">Location to extract from</param>
            /// <returns>Unsigned 32-bit integer</returns>
            public static UInt32 ToU32(byte[] bytes, ref int offset)
            {
                return (UInt32)(0
                    | ((UInt32)(bytes[offset++]) << 24)
                    | ((UInt32)(bytes[offset++]) << 16)
                    | ((UInt32)(bytes[offset++]) << 8)
                    | ((UInt32)(bytes[offset++]) << 0)
                    );
            }
            #endregion

            public static byte[] GetIpAddrBytes(string ip)
            {
                return IPAddress.Parse(ip).GetAddressBytes();
                //List<byte> list = new List<byte>();
                //foreach(string str in ip.Split('.'))
                //{
                //    list.Add(byte.Parse(Convert.ToInt32(str).ToString("x"), System.Globalization.NumberStyles.HexNumber));
                //}
                //return list.ToArray() ;
            }

            public static bool CheckIpAddr(string ip)
            {
                IPAddress address;
                return IPAddress.TryParse(ip, out address);
            }

            public static bool CheckMacAddr(string macAddr)
            {
                string pattern = @"^([0-9a-fA-F]{2}:){5}([0-9a-fA-F]{2})$";
                Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
                return regex.IsMatch(macAddr);
            }

            public static byte CheckSum(byte[] btAryBuffer, int nStartPos, int nLen)
            {
                byte btSum = 0x00;

                for (int nloop = nStartPos; nloop < nStartPos + nLen; nloop++)
                {
                    btSum += btAryBuffer[nloop];
                }

                return Convert.ToByte(((~btSum) + 1) & 0xFF);
            }

            public static bool CheckIsByteInt(string str)
            {
                str = str.Trim();
                if (str.Length == 0 || str.Length > 3)
                    return false;
                Regex regex = new Regex(@"^(([0-9]){1}|([1-9][0-9]){1}|([1][0-9][0-9]){1}|(2[0-4][0-9]){1}|(25[0-5]){1})$");
                return regex.IsMatch(str);
            }

            public static bool CheckIsInt(string str)
            {
                str = str.Trim();
                if (str.Length == 0)
                    return false;
                Regex regex = new Regex(@"^([0-9]*)$|^(-1){1}$");
                return regex.IsMatch(str);
            }

            public static bool CheckFourBytesPwd(string str)
            {
                str = str.Trim();
                if (str.Length != 8)
                    return false;
                Regex regex = new Regex(@"^([0-9a-fA-F]){8}$");
                return regex.IsMatch(str);
            }
        }

        public enum CommExceptionCode
        {
            //TCP
            ConnectToServerFail = 0x00,
            ConnectError = 0x01,
            DataRecvError = 0x02,
            DataSendError = 0x03,
            ReconnectSuccess = 0x04,
            ReconnectFailed = 0x05,
            TcpLogout = 0x06,
            NotTcpObj = 0x7,
            NotSerialObj = 0x8,
            CommError = 0x9,
        }
    }
}
