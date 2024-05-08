using static Service_RFID_BIG_READER.Reader_DLL.Options;

namespace Service_RFID_BIG_READER
{
    internal interface InterfaceRfidAPI
    {
        bool Connect(string port);
        bool Disconnect (ConnectType connectType);
        void Inventory();

    }
}
