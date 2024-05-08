using Reader;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Service_RFID_BIG_READER.Reader.Rfid_Fonkan_E710
{
    public class TagRecordBindingList : SortableBindingList<TagRecord>
    {
        
        protected override Comparison<TagRecord> GetComparer(PropertyDescriptor prop)
        {
            Comparison<TagRecord> comparer = null;
            switch (prop.Name)
            {
                case "SerialNumber":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return (int)(a.SerialNumber - b.SerialNumber);
                    });
                    break;
                case "ReadCount":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return a.ReadCount - b.ReadCount;
                    });
                    break;
                case "PC":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.PC, b.PC);
                    });
                    break;
                case "EPC":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.EPC, b.EPC);
                    });
                    break;
                case "CRC":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.CRC, b.CRC);
                    });
                    break;
                case "Rssi":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Rssi, b.Rssi);
                    });
                    break;
                case "Freq":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Freq, b.Freq);
                    });
                    break;
                case "Phase":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Phase, b.Phase);
                    });
                    break;
                case "Antenna":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Antenna, b.Antenna);
                    });
                    break;
                case "Data":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Data, b.Data);
                    });
                    break;
                case "DataLen":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.DataLen, b.DataLen);
                    });
                    break;
                case "OpSuccessCount":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.OpSuccessCount, b.OpSuccessCount);
                    });
                    break;
                case "Region":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return a.Region - b.Region;
                    });
                    break;
                case "StartFreq":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return (int)(a.StartFreq - b.StartFreq);
                    });
                    break;
                case "EndFreq":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return a.EndFreq - b.EndFreq;
                    });
                    break;
                case "FreqSpace":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return a.FreqSpace - b.FreqSpace;
                    });
                    break;
                case "FreqQuantity":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return a.FreqQuantity - b.FreqQuantity;
                    });
                    break;
                case "Temperature":
                    comparer = new Comparison<TagRecord>(delegate (TagRecord a, TagRecord b)
                    {
                        return String.Compare(a.Temperature, b.Temperature);
                    });
                    break;
            }
            return comparer;
        }
    }

    #region TagDB
    public class TagDB
    {
        private TagRecordBindingList _tagList = new TagRecordBindingList();
        private Object lock_obj = new Object();
        /// <summary>
        /// EPC index into tag list
        /// </summary>
        private Dictionary<string, TagRecord> EpcIndex = new Dictionary<string, TagRecord>();
        private List<string> EpcIndexForTest = new List<string>();

        static long uniqueTagCounts = 0; // Total Tag quantity
        static long totalReadCounts = 0; // Total read times
        static uint totalCommandTimes = 0; // Total execution time
        uint cmdTotalRead = 0; // Number of labels read in a single inventory (including duplicate labels)
        uint cmdCommandDuration = 0; // Single execution instruction time
        ushort cmdReadRate = 0; // The counting rate of a single execution instruction
        uint cmdTotalUniqueRead = 0; //Number of labels read in a single inventory (excluding duplicate labels)

        #region 0x79
        byte region;
        uint startFreq;
        byte endFreq;
        byte freqSpace;
        byte freqQuantity;
        #endregion 0x79

        private byte antGroup = 0x00;
        private int keyLen = 0;
        private TagVendor chipType = TagVendor.NormalTag;

        public BindingList<TagRecord> TagList
        {
            get { return _tagList; }
        }
        public long TotalTagCounts
        {
            get { return uniqueTagCounts; }
        }
        public long TotalReadCounts
        {
            get { return totalReadCounts; }
        }

        public uint TotalCommandTime
        {
            get { return totalCommandTimes; }
        }

        public uint CommandDuration
        {
            get { return cmdCommandDuration; }
        }

        public long CmdTotalRead
        {
            get { return cmdTotalRead; }
        }

        public int CmdReadRate
        {
            get { return cmdReadRate; }
        }

        public uint CmdUniqueTagCount
        {
            get { return cmdTotalUniqueRead; }
            set
            {
                if (value == 0)
                {
                    EpcIndexForTest.Clear();
                }
                cmdTotalUniqueRead = value;
            }
        }

        public int MinRSSI { get; internal set; }
        public int MaxRSSI { get; internal set; }
        public byte AntGroup
        {
            get { return antGroup; }
            set
            {
                if (value == 0x00)
                {
                    antGroup = 0x00;
                }
                else
                {
                    antGroup = 0x01;
                }
            }
        }

        public byte CurRegion { get { return region; } }
        public uint FrequencyStart { get { return startFreq; } }
        public byte FrequencyEnd { get { return endFreq; } }
        public byte FreqQuantity { get { return freqQuantity; } }
        public byte FrequencyInterval { get { return freqSpace; } }

        public void UpdateCmd89ExecuteSuccess(byte[] data)
        {
            //msg : [hdr][len][addr][cmd][data][check]
            //data: [antId][TotalRead][CommandDuration]
            //      [  1  ][  2      ][      4        ] 
            //Console.WriteLine("data={0}", ReaderUtils.ToHex(data, "", " "));
            int readIndex = 0;
            byte antId = data[readIndex++];
            ushort readRate = ReaderUtils.ToU16(data, ref readIndex);

            uint totalRead = ReaderUtils.ToU32(data, ref readIndex);

            cmdTotalRead = totalRead;
            cmdReadRate = readRate;
            cmdCommandDuration = cmdReadRate == 0 ? cmdCommandDuration : ((cmdTotalRead * 1000) / cmdReadRate);
            totalCommandTimes += cmdCommandDuration;

            //uniqueTagCounts = 0;
            //totalReadCounts += totalRead;
            //Console.WriteLine("antId={0}, readRate={1}, totalRead={2}, totalTime={3}", antId, readRate, totalRead, cmdReadRate == 0 ? cmdCommandDuration : ((cmdTotalRead * 1000) / cmdReadRate));
        }

        public void UpdateCmd8AExecuteSuccess(byte[] data)
        {
            //msg : [hdr][len][addr][cmd][data][check]
            //data: [TotalRead][CommandDuration]
            //      [  3      ][      4        ] 
            //Console.WriteLine("data={0}", ReaderUtils.ToHex(data, "", " "));
            int readIndex = 0;
            byte[] bTotalRead = new byte[4];
            Array.Copy(data, 0, bTotalRead, 1, bTotalRead.Length - 1);
            uint totalRead = ReaderUtils.ToU32(bTotalRead, ref readIndex);
            readIndex--;

            //Console.WriteLine("readIndex={0}, bTotalRead={1}", readIndex, ReaderUtils.ToHex(bTotalRead, "", " "));
            uint commandDuration = ReaderUtils.ToU32(data, ref readIndex);
            //Console.WriteLine("readIndex={0}, totalRead={1}, commandDuration={2}", readIndex, totalRead, commandDuration);

            cmdTotalRead = totalRead;
            cmdCommandDuration = commandDuration;
            cmdReadRate = (cmdCommandDuration == 0 ? cmdReadRate : (ushort)(cmdTotalRead * 1000 / cmdCommandDuration));
            totalCommandTimes += cmdCommandDuration;

            //uniqueTagCounts = 0;
            //totalReadCounts += cmdTotalRead;
            //Console.WriteLine("antId={0}, readRate={1}, totalRead={2}, totalTime={3}", antId, readRate, totalRead, cmdReadRate == 0 ? cmdCommandDuration : ((cmdTotalRead * 1000) / cmdReadRate));
        }

        public void UpdateCmd80ExecuteSuccess(byte[] data)
        {
            //msg : [hdr][len][addr][cmd][data][check]
            //data: [AntId][TagCount][ReadRate][TotalRead]
            //      [  1  ][   2    ][  2     ][   4     ] 
            //Console.WriteLine("data={0}", ReaderUtils.ToHex(data, "", " "));
            int readIndex = 0;
            byte antId = data[readIndex++];
            int tagCount = ReaderUtils.ToU16(data, ref readIndex);
            int readRate = ReaderUtils.ToU16(data, ref readIndex);
            uint totalRead = ReaderUtils.ToU32(data, ref readIndex);

            //Console.WriteLine("readIndex={0}, antId={1}, tagCount={2}, readRate={3}, totalRead={4}", readIndex, antId, tagCount, readRate, totalRead);

            cmdTotalRead = totalRead;
            cmdReadRate = (ushort)readRate;
            uniqueTagCounts = (uint)tagCount;
            totalReadCounts += totalRead;
            cmdCommandDuration = (uint)(cmdReadRate > 0 ? (cmdTotalRead*1000 / cmdReadRate) : 0);
            totalCommandTimes += cmdCommandDuration;
            cmdTotalUniqueRead = cmdTotalRead;
        }

        public void Clear()
        {
            lock (lock_obj)
            {
                EpcIndex.Clear();
                _tagList.Clear();
                MaxRSSI = 0;
                MinRSSI = 0;
                uniqueTagCounts = 0;
                totalReadCounts = 0;
                totalCommandTimes = 0;
                cmdCommandDuration = 0;
                cmdReadRate = 0;
                cmdTotalRead = 0;
                // Clear doesn't fire notifications on its own
                _tagList.ResetBindings();

                EpcIndexForTest.Clear();
                cmdTotalUniqueRead = 0;
            }

        }

        public void Add(Tag addData)
        {
            lock (lock_obj)
            {
                string key = null;
                if (keyLen != 0)
                {
                    if (addData.EPC.Length > keyLen)
                        key = addData.EPC.Replace(" ", "").Substring(0, keyLen);
                    else
                        key = addData.EPC.Replace(" ", "");
                }
                else
                {
                    key = addData.EPC.Replace(" ", "");
                }

                if (!EpcIndexForTest.Contains(key))
                {
                    cmdTotalUniqueRead++;
                    EpcIndexForTest.Add(key);
                }

                uniqueTagCounts = 0;
                totalReadCounts = 0;

                if (!EpcIndex.ContainsKey(key))
                {
                    //Console.WriteLine("({1}) Add key={0}", key, keyLen);
                    TagRecord value = new TagRecord(addData, chipType);
                    value.SerialNumber = (uint)EpcIndex.Count + 1;
                    value.Region = region;
                    value.StartFreq = startFreq;
                    value.EndFreq = endFreq;
                    value.FreqSpace = freqSpace;
                    value.FreqQuantity = freqQuantity;

                    _tagList.Add(value);
                    EpcIndex.Add(key, value);
                    //Call this method to calculate total tag reads and unique tag read counts 
                    UpdateTagCountTextBox(EpcIndex);
                }
                else
                {
                    EpcIndex[key].Update(addData);
                    UpdateTagCountTextBox(EpcIndex);
                }
            }
        }

        //Calculate total tag reads and unique tag reads.
        public void UpdateTagCountTextBox(Dictionary<string, TagRecord> EpcIndex)
        {
            uniqueTagCounts = EpcIndex.Count;
            TagRecord[] dataRecord = new TagRecord[EpcIndex.Count];
            EpcIndex.Values.CopyTo(dataRecord, 0);
            totalReadCounts = 0;
            for (int i = 0; i < dataRecord.Length; i++)
            {
                totalReadCounts += dataRecord[i].ReadCount;
            }
        }

        #region 0x79
        public string UpdateRegionInfoByData(byte[] data)
        {
            int readIndex = 0;
            region = data[readIndex++];
            switch (region)
            {
                case 0x01:
                case 0x02:
                case 0x03:
                    startFreq = data[readIndex++];
                    endFreq = data[readIndex++];
                    freqSpace = 0x05;
                    freqQuantity = (byte)((endFreq - startFreq) / freqSpace);
                    break;
                case 0x04:
                    freqSpace = data[readIndex++];
                    freqQuantity = data[readIndex++];
                    startFreq = ReaderUtils.ToU24(data, ref readIndex);
                    endFreq = (byte)(startFreq + (freqSpace * 10) * freqQuantity);
                    break;
                default:
                    startFreq = 0;
                    endFreq = 0;
                    freqSpace = 0;
                    freqQuantity = 0;
                    break;
            }
            return string.Format("{0},{1},{2},{3},{4}", region, startFreq, freqSpace, freqQuantity, endFreq);
        }

        [Obsolete]
        public void UpdateRegion(byte bRegion, byte bStart, byte bEnd)
        {
            region = bRegion;
            startFreq = bStart;
            endFreq = bEnd;
            freqSpace = 0x05;
            freqQuantity = (byte)((endFreq - startFreq) / freqSpace);
        }
        [Obsolete]
        public void UpdateUserDefineRegion(byte bStart, byte bSpace, byte bQuantity)
        {
            region = 0x04;
            freqSpace = bSpace;
            freqQuantity = bQuantity;
            startFreq = bStart;
            endFreq = (byte)(startFreq + (freqSpace * 10) * freqQuantity);
        }
        #endregion //0x79
        /// <summary>
        /// Manually release change events
        /// </summary>
        public void Repaint()
        {
            _tagList.RaiseListChangedEvents = true;

            //Causes a control bound to the BindingSource to reread all the items in the list and refresh their displayed values.
            _tagList.ResetBindings();

            _tagList.RaiseListChangedEvents = false;
        }

        public void SetJoharKeyLen()
        {
            keyLen = 4;
            chipType = TagVendor.JoharTag_1;
        }

        public void ClearKeyLen()
        {
            keyLen = 0;
            chipType = TagVendor.NormalTag;
        }
    }
    #endregion TagDB
}
