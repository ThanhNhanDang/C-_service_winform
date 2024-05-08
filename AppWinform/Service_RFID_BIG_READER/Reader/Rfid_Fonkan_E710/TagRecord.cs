using Reader;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Service_RFID_BIG_READER.Reader.Rfid_Fonkan_E710
{
    public class TagRecord : INotifyPropertyChanged
    {
        protected Tag RawRead = null;
        protected bool dataChecked = false;
        protected UInt32 serialNo = 0;
        protected string oldTemp = "null";
        private TagVendor vendor = TagVendor.NormalTag;

        #region 0x79
        byte region;
        uint startFreq;
        byte endFreq;
        byte freqSpace;
        byte freqQuantity;
        #endregion 0x79

        public TagRecord(Tag newData, TagVendor chipType)
        {
            lock (new Object())
            {
                RawRead = newData;
                vendor = chipType;
            }
        }
        /// <summary>
        /// Merge new tag read with existing one
        /// </summary>
        /// <param name="data">New tag read</param>
        public void Update(Tag mergeData)
        {
            //Console.WriteLine("Update {0}", mergeData.EPC);
            mergeData.ReadCount += ReadCount;
            RawRead = mergeData;
            RawRead.ReadCount = mergeData.ReadCount;
            if (!RawRead.Temperature.Equals("null"))
            {
                oldTemp = RawRead.Temperature;
            }
            OnPropertyChanged(null);
        }

        public UInt32 SerialNumber
        {
            get { return serialNo; }
            set { serialNo = value; }
        }

        //public DateTime TimeStamp
        //{
        //    get
        //    {
        //        //return DateTime.Now.ToLocalTime();
        //        TimeSpan difftime = (DateTime.Now.ToUniversalTime() - RawRead.Time.ToUniversalTime());
        //        //double a1111 = difftime.TotalSeconds;
        //        if (difftime.TotalHours > 24)
        //            return DateTime.Now.ToLocalTime();
        //        else
        //            return RawRead.Time.ToLocalTime();
        //    }
        //}

        public int ReadCount
        {
            get { return RawRead.ReadCount; }
        }

        public string PC
        {
            get { return RawRead.PC; }
        }

        public string EPC
        {
            get {
                return RawRead.EPC;
            }
        }

        public string CRC
        {
            get { return RawRead.CRC; }
        }

        public string Rssi
        {
            get { return RawRead.Rssi; }
        }

        public string Freq
        {
            get {
                //Console.WriteLine("Freq {0:X2}", RawRead.FreqByte);
                //Console.WriteLine("Region {0:X2} [{1:X2} to {2:X2}] space={3:X2} quantity={4:X2}", 
                //    region, startFreq, endFreq, freqSpace, freqQuantity);
                string strFreq;
                switch (region)
                {
                    case 0x01:
                    case 0x02:
                    case 0x03:
                        if (RawRead.FreqByte < 0x07)
                            strFreq = (865 + RawRead.FreqByte * 0.5).ToString("0.00");
                        else
                        {
                            strFreq = (902 + (RawRead.FreqByte - 7) * 0.5).ToString("0.00");
                        }
                        break;
                    case 0x04:
                        strFreq = ((startFreq + (freqSpace * 10) * RawRead.FreqByte) / 1000).ToString("0.00"); 
                        break;
                    default:
                        strFreq = "0.00";
                        break;
                }

                return strFreq;
            }
        }

        public string Phase
        {
            get { return RawRead.Phase; }
        }

        public string Antenna
        {
            get { return RawRead.Antenna; }
        }

        public string Data
        {
            get { return RawRead.Data; }
        }

        public string DataLen
        {
            get { return RawRead.DataLen.ToString(); }
        }

        public string OpSuccessCount
        {
            get { return RawRead.OpSuccessCount.ToString(); }
        }

        public byte Region
        {
            get { return region; }
            set { region = value; }
        }
        public uint StartFreq
        {
            get { return startFreq; }
            set { startFreq = value; }
        }
        public byte EndFreq
        {
            get { return endFreq; }
            set { endFreq = value; }
        }
        public byte FreqSpace
        {
            get { return freqSpace; }
            set { freqSpace = value; }
        }
        public byte FreqQuantity
        {
            get { return freqQuantity; }
            set { freqQuantity = value; }
        }
        public string Temperature
        {
            get
            {
                //Console.WriteLine("vendor={0} [{1}]", vendor, RawRead.EPC);
                if (vendor == TagVendor.NormalTag)
                {
                    return "null";
                }
                bool checkTemp = RawRead.Temperature.Equals("null");
                return (checkTemp == false ? RawRead.Temperature : oldTemp);
            }
        }

        //public bool Checked
        //{
        //    get { return dataChecked; }
        //    set
        //    {
        //        dataChecked = value;
        //    }
        //}

        #region INotifyPropertyChanged Members

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            PropertyChangedEventArgs td = new PropertyChangedEventArgs(name);
            try
            {
                if (null != PropertyChanged)
                {
                    PropertyChanged(this, td);
                }
            }
            finally
            {
                td = null;
            }
        }
        #endregion
    }
}
