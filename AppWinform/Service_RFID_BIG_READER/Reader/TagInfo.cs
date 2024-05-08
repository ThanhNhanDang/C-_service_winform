
using System;

namespace Service_RFID_BIG_READER.Reader
{
    internal class TagInfo
    {
        public DateTime dateTime { get; set; }
        public string epc { get; set; }
        public string tid { get; set; }
        public string password { get; set; }
        public string crc { get; set; }
        public string rssi { get; set; }
        public string atn { get; set; }
        public string topic { get; set; }
        public bool isIn { get; set; }
        public bool isNg { get; set; }

        public TagInfo()
        {
            epc = string.Empty;
            tid = string.Empty;
            crc = string.Empty;
            rssi = string.Empty;
            atn = string.Empty;
        }

        public TagInfo(string epc, string tid, string password, string crc, string rssi, string ant, bool isNg)
        {
            this.epc = epc;
            this.tid = tid;
            this.password = password;
            this.crc = crc;
            this.rssi = rssi;
            this.atn = ant;
            this.isNg = isNg;
        }
        public TagInfo(DateTime dateTime,string epc, string tid, string password, string crc, string rssi, string ant,string topic, bool isNg, bool isIn)
        {
            this.dateTime = dateTime;
            this.epc = epc;
            this.tid = tid;
            this.password = password;
            this.crc = crc;
            this.rssi = rssi;
            this.atn = ant;
            this.topic = topic;
            this.isNg = isNg;
            this.isIn = isIn;
        }
    }
}
