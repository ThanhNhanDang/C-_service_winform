using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;

namespace Service_RFID_BIG_READER.Util
{
    internal class ConfigMapping
    {
        public static byte ENC_PASS = Convert.ToByte(ConfigurationManager.AppSettings["encPass"]);
        public static byte[] ARR_ANTENNA_IN;
        public static byte[] ARR_ANTENNA_OUT;

        private static List<byte> ConvertStringToByteList(string input)
        {
            var result = new List<byte>();
            string num = "";

            foreach (char c in input)
            {
                if (c == '+')
                {
                    result.Add((byte)(Convert.ToByte(num) - 1));
                    num = "";
                }
                else
                {
                    num += c;
                }
            }

            if (!string.IsNullOrEmpty(input))
                result.Add((byte)(Convert.ToByte(num) - 1));
            return result;
        }

        static ConfigMapping()
        {
            string antennaInString = ConfigurationManager.AppSettings["antennaIn"];
            string antennaOutString = ConfigurationManager.AppSettings["antennaOut"];

            ARR_ANTENNA_IN = ConvertStringToByteList(antennaInString).ToArray();

            ARR_ANTENNA_OUT = ConvertStringToByteList(antennaOutString).ToArray();
        }
    }
}