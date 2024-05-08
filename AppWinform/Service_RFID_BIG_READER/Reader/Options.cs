using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Reader_DLL
{
    public class Options 
    {
        public enum ConnectType
        {
            COM = 0x01,
            USB = 0x02,
            TcpCli = 0x03,
            TcpSvr = 0x04,
            UDP = 0x05,
        }
        public enum ReaderType
        {
            SYC_R16 = 1,
            ZTX_G20 = 2,
            CF_RU6403 = 3,
            FONKAN_E710 = 4,
        }

        public enum ReaderAntTypeForFonkanE710
        {
            ANT_TYPE_1 = 1,
            ANT_TYPE_4 = 4,
            ANT_TYPE_8 = 8,
            ANT_TYPE_16 = 16
        }
    }
}
