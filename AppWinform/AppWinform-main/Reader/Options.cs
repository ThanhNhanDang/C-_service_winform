namespace AppWinform_main.Reader_DLL
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
        }



    }
}
