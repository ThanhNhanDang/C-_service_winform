using Newtonsoft.Json;
using Service_RFID_BIG_READER.Reader;
using Service_RFID_BIG_READER.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Service_RFID_BIG_READER.Reader_DLL.Options;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    internal class SwitchDLL : InterfaceRfidAPI
    {
        SYC_R16_DLL_DEFINE sYC_R16_DLL_DEFINE = null;
        ZTX_G20_DLL_DEFINE zTX_G20_DLL_DEFINE = null;
        CF_RU6403_DLL_DEFINE cF_RU6403_DLL_DEFINE = null;
        Fonkan_E710_DLLProject fonkan_E710_DLLProject = null;
        ReaderType readerType;

        public SwitchDLL(ReaderType readerType)
        {
            this.readerType = readerType;
        }
        public bool Connect(string port)
        {
            bool result = false;
            if (readerType == ReaderType.SYC_R16)
            {
                Service1.WriteLog("SYC_R16");
                if (sYC_R16_DLL_DEFINE == null)
                    sYC_R16_DLL_DEFINE = new SYC_R16_DLL_DEFINE();
                result = sYC_R16_DLL_DEFINE.Connect(port);
                return result;
            }
            if (readerType == ReaderType.ZTX_G20)
            {
                Service1.WriteLog("ZTX_G20");
                if (zTX_G20_DLL_DEFINE == null)
                    zTX_G20_DLL_DEFINE = new ZTX_G20_DLL_DEFINE();
                result = zTX_G20_DLL_DEFINE.Connect(port);

                return result;
            }

            if (readerType == ReaderType.CF_RU6403)
            {
                Service1.WriteLog("CF_RU6403");
                if (cF_RU6403_DLL_DEFINE == null)
                    cF_RU6403_DLL_DEFINE = new CF_RU6403_DLL_DEFINE();
                result = cF_RU6403_DLL_DEFINE.Connect(port);
                return result;
            }
            if (readerType == ReaderType.FONKAN_E710)
            {
                Service1.WriteLog("FONKAN_E710");
                if (fonkan_E710_DLLProject == null)
                    fonkan_E710_DLLProject = new Fonkan_E710_DLLProject();
                result = fonkan_E710_DLLProject.Connect(port);
                return result;
            }
            return result;
        }

        public bool Disconnect(Options.ConnectType connectType)
        {
            throw new NotImplementedException();
        }

        public void Inventory()
        {
            if (readerType == ReaderType.SYC_R16)
            {
                SYC_R16_DLLProject.ReadSingle();
                return;
            }
            if (readerType == ReaderType.ZTX_G20)
            {
                zTX_G20_DLL_DEFINE.Inventory();
                return;
            }
            if (readerType == ReaderType.CF_RU6403)
            {
                if (ConfigMapping.ARR_ANTENNA_IN.Length != 0)
                    for (int inAtn = ConfigMapping.ARR_ANTENNA_IN.Length - 1; inAtn >= 0; inAtn--)
                        cF_RU6403_DLL_DEFINE.Inventory((byte)(0x80 | ConfigMapping.ARR_ANTENNA_IN[inAtn]));
                if (ConfigMapping.ARR_ANTENNA_OUT.Length != 0)
                    for (int outAtn = ConfigMapping.ARR_ANTENNA_OUT.Length - 1; outAtn >= 0; outAtn--)
                        cF_RU6403_DLL_DEFINE.Inventory((byte)(0x80 | ConfigMapping.ARR_ANTENNA_OUT[outAtn]));
                return;
            }

            if (readerType == ReaderType.FONKAN_E710)
            {
                fonkan_E710_DLLProject.Inventory();
                return;
            }
        }
    }
}
