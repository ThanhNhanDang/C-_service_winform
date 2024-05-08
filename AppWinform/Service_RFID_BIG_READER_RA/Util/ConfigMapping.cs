using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service_RFID_BIG_READER.Util
{
    internal class ConfigMapping
    {
        public static byte ENC_PASS = Convert.ToByte(ConfigurationManager.AppSettings["encPass"]);

        public static List<byte> ARR_ANTENNA_IN;
        public static List<byte> ARR_ANTENNA_OUT;

        public static HashSet<string> IN_HASH = new HashSet<string>();
        public static HashSet<string> OUT_HASH = new HashSet<string>();
        static ConfigMapping()
        {
            string antennaIn = ConfigurationManager.AppSettings["antennaIn"];
            string antennaOut = ConfigurationManager.AppSettings["antennaOut"];
            string num = "";
            int i = 0;

            
            ARR_ANTENNA_IN = new List<byte>();
            ARR_ANTENNA_OUT = new List<byte>();

            for (i = 0; i < antennaIn.Length; i++)
            {
                if (antennaIn[i] == '+')
                {
                    ARR_ANTENNA_IN.Add(Convert.ToByte(num));
                    num = "";
                    continue;
                }
                num += antennaIn[i];
            }
            ARR_ANTENNA_IN.Add(Convert.ToByte(num));

            for (i = 0, num = ""; i < antennaOut.Length; i++)
            {
                if (antennaOut[i] == '+')
                {
                    ARR_ANTENNA_OUT.Add(Convert.ToByte(num));
                    num = "";
                    continue;
                }
                num += antennaOut[i];
            }
            ARR_ANTENNA_OUT.Add(Convert.ToByte(num));
        }
    }
}
