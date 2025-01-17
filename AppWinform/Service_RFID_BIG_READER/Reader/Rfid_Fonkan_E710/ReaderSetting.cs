﻿namespace Service_RFID_BIG_READER.Reader.Rfid_Fonkan_E710
{
    class ReaderSetting
    {
        public byte btReadId;
        public byte btMajor;
        public byte btMinor;
        public byte btInternalVersion;
        public byte btIndexBaudrate;
        public byte btPlusMinus;
        public byte btTemperature;
        public byte btOutputPower;
        public byte btWorkAntenna;
        public byte btDrmMode;
        
        public byte btBeeperMode;
        public byte btGpio1Value;
        public byte btGpio2Value;
        public byte btGpio3Value;
        public byte btGpio4Value;
        public byte btAntDetector;
        public byte btMonzaStatus;
        public string btReaderIdentifier;
        public byte btAntImpedance;

        public byte btLinkProfile;
        public byte[] btOutputPowers;

        public ReaderSetting()
        {
            btReadId = 0xFF;
            btMajor = 0x00;
            btMinor = 0x00;
            btInternalVersion = 0x00;
            btIndexBaudrate = 0x00;
            btPlusMinus = 0x00;
            btTemperature = 0x00;
            btOutputPower = 0x00;
            btWorkAntenna = 0x00;
            btDrmMode = 0x00;
            btBeeperMode = 0x00;
            btGpio1Value = 0x00;
            btGpio2Value = 0x00;
            btGpio3Value = 0x00;
            btGpio4Value = 0x00;
            btAntDetector = 0x00;
        }
    }
}