using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Management;
using System.Configuration;
using System.ComponentModel;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Net.NetworkInformation;

using MQTTnet;
using MQTTnet.Server;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Exceptions;

using Newtonsoft.Json;

using Service_RFID_BIG_READER.Util;
using Service_RFID_BIG_READER.Entity;
using Service_RFID_BIG_READER.Database;
using Service_RFID_BIG_READER.Reader_DLL;
using static Service_RFID_BIG_READER.Reader_DLL.Options;


namespace Service_RFID_BIG_READER
{
    [RunInstaller(true)]
    public partial class Service1 : ServiceBase
    {
        #region MQTT
        // Tạo một factory để tạo các client MQTT
        private MqttFactory _factoryMQTT;
        // Các tùy chọn được sử dụng để tạo một client MQTT
        private MqttClientOptions _options;
        // Thực thể client để tương tác với MQTT broker
        private static IMqttClient _mqttClient;
        // Client ID được tạo cho phiên bản này
        private string _clientID;

        // Tên miền server (chưa sử dụng)
        private const string _host = "nsp.t4tek.tk";

        // Đọc thông tin kết nối (IP, username, password, port) từ app settings
        private string _brokerIP = ConfigurationManager.AppSettings["serverIP"];
        private string _brokerUserName = ConfigurationManager.AppSettings["userName"];
        private string _brokerPassword = ConfigurationManager.AppSettings["password"];
        private int _brokerPort = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]);
        #endregion

        #region thread
        // Thread để xử lý giao tiếp MQTT
        private Thread _inventoryThrd = null;
        // Thread để xử lý kết nối reader
        private Thread _readerConnectThrd = null;
        #endregion

        #region log
        // Đường dẫn đến file log (đọc từ app settings)
        private static string _pathLogFile = ConfigurationManager.AppSettings["pathLogFile"];
        #endregion

        #region reader
        // Loại kết nối (ví dụ: USB, Serial) - đọc từ app settings
        public static ConnectType _connectType;
        // Loại reader (ví dụ: CF_RU6403, ZTX6_G20) - đọc từ app settings
        public static ReaderType _readerType;
        // Cờ báo reader đã ngắt kết nối chưa
        public static bool _isReaderDisconnect = false;
        // Khoảng thời gian tối đa giữa các hoạt động kiểm kê (đọc từ app settings bằng mili giây)
        private int _maxInventoryTime = Convert.ToInt32(ConfigurationManager.AppSettings["maxInventoryTime"]);
        // Khoảng thời gian chờ trước khi thử lại kết nối reader (đọc từ app settings bằng mili giây)
        private int _readerReconnectTime = Convert.ToInt32(ConfigurationManager.AppSettings["readerReconnectTime"]);
        // Giá trị chuỗi được sử dụng để lọc các cổng serial (có thể để phát hiện USB) - đọc từ app settings
        private string _connectDivide = ConfigurationManager.AppSettings["connectDevide"];
        // Thực thể để tương tác với thiết bị reader (có thể sử dụng một DLL cụ thể)
        private SwitchDLL switchDLL = null;
        #endregion

        public Service1()
        {
            InitializeComponent();
        }
        #region service event
        // Phương thức được gọi cho mục đích gỡ lỗi (giả lập OnStart)
        public void OnDebug()
        {
            OnStart(null);
        }
        protected override void OnStart(string[] args)
        {
            // Ghi log thời gian khởi động dịch vụ
            WriteLog($"Windows Service is called on {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt")}.");

            // Khởi tạo loại kết nối và loại reader từ app settings
            InitConnectAndReaderType();
            // Cố gắng kết nối với reader bằng cách sử dụng cổng đã khởi tạo
            _isReaderDisconnect = InitReader(InitSerialPort());

            // Khởi chạy một thread riêng biệt để kết nối lại và đăng ký với MQTT broker
            Task.Run(() =>
            {
                ReconnectSubscribeToBroker();
            });

            // Khởi chạy một thread cho các hoạt động kiểm kê định kỳ
            _inventoryThrd = new Thread(new ThreadStart(MqttThrd));
            _inventoryThrd.Start();
            _inventoryThrd.IsBackground = true;

            // Khởi chạy một thread để xử lý kết nối và kết nối lại reader
            _readerConnectThrd = new Thread(new ThreadStart(ReaderConnectThrd));
            _readerConnectThrd.Start();
            _readerConnectThrd.IsBackground = true;
        }


        protected override void OnStop()
        {
            try
            {
                // Dừng thread kiểm kê nếu nó đang chạy
                if (_inventoryThrd != null & _inventoryThrd.IsAlive)
                {
                    _inventoryThrd.Abort();
                }
                // Dừng thread kết nối reader nếu nó đang chạy
                if (_readerConnectThrd != null & _readerConnectThrd.IsAlive)
                {
                    _readerConnectThrd.Abort();
                }
                // Ngắt kết nối khỏi broker MQTT nếu được kết nối
                if (_mqttClient.IsConnected)
                {
                    _mqttClient.DisconnectAsync();
                    // Giải phóng tài nguyên liên quan đến client MQTT
                    _mqttClient.Dispose();
                }
            }
            catch (Exception)
            {
                // Ném lại bất kỳ ngoại lệ nào bắt được trong quá trình dọn dẹp để ngăn dịch vụ dừng
                throw;
            }
        }

        #endregion

        #region init
        private bool InitConnectAndReaderType()
        {
            // Đọc loại kết nối và loại reader từ cấu hình
            string connectType = ConfigurationManager.AppSettings["connectType"];
            string readerType = ConfigurationManager.AppSettings["readerType"];

            // Kiểm tra xem cả hai loại đều được cấu hình hay không
            if (connectType == null || readerType == null)
            {
                WriteLog("ERROR ConnectType or ReaderType not found!");
                return false;
            }
            try
            {
                // Chuyển đổi chuỗi sang kiểu enum (ConnectType và ReaderType)
                _connectType = (ConnectType)Convert.ToInt16(connectType);
                _readerType = (ReaderType)Convert.ToInt16(readerType);
                return true;

            }
            catch (FormatException)
            {
                WriteLog("ERROR Convert _connectType or _readerType!");
                return false;
            }
        }

        private string InitSerialPort()
        {
            // Tùy chọn 1: Lấy tên cổng nối tiếp từ cấu hình
            /* string portName = ConfigurationManager.AppSettings["comPort"];
               if (portName != null)
                         return portName;
         */
            // Tùy chọn 2: Tìm kiếm cổng nối tiếp bằng WMI
            ManagementObjectSearcher searcher =
                   new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'");
            if (searcher == null) return null;

            // Lấy tên cổng từ kết quả tìm kiếm
            IEnumerable<string> ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["caption"].ToString());

            // Lọc cổng dựa trên tiêu chí cụ thể dựa trên _connectDivide
            string port_select = ports.FirstOrDefault(p => p.Contains(_connectDivide));
            if (port_select == null) return null;

            // Trích xuất số cổng thực tế từ chuỗi
            int index = port_select.IndexOf("(");
            string port = port_select.Substring(index + 1); // COM8)
            port = port.Substring(0, port.Length - 1);  //COM8
            //WriteLog(port);
            return port;
        }
        private static bool IsConnectedToInternet()
        {
            using (Ping p = new Ping())
            {
                try
                {
                    PingReply reply = p.Send(_host, 10000);
                    p.Dispose();
                    if (reply.Status == IPStatus.Success)
                        return true;
                }
                catch { }
            }
            return false;
        }

        bool InitReader(string port)
        {
            // Kiểm tra nếu cổng COM là null và loại kết nối không phải USB thì trả về false
            if (port == null && _connectType != ConnectType.USB) return false;

            // Kiểm tra nếu reader đã được ngắt kết nối (tránh khởi tạo lại)
            if (_isReaderDisconnect)
                return true;

            // Kiểm tra xem loại kết nối và reader đã được khởi tạo chính xác (bảo đảm chúng được thiết lập đúng)
            if (!InitConnectAndReaderType()) return false;

            // Gọi hàm Connect để thiết lập kết nối với reader
            bool result = Connect(_connectType, port, _readerType);

            return result;

        }
        #endregion

        #region write log
        public static async void WriteLog(string log)
        {
            using (StreamWriter sw = new StreamWriter(_pathLogFile, true))
            {
                await sw.WriteLineAsync($"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt")}: {log}");
                sw.Close();
            }
        }
        #endregion

        #region mqtt reconnect và đăng ký topic
        private async void ReconnectSubscribeToBroker()
        {
            _factoryMQTT = new MqttFactory();
            _mqttClient = _factoryMQTT.CreateMqttClient();
            _clientID = Guid.NewGuid().ToString();

            // ... (tạo các option cho kết nối MQTT)
            _options = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerIP, _brokerPort) // MQTT broker address and port
            .WithCredentials(_brokerUserName, _brokerPassword) // Set username and password
            .WithClientId(_clientID)
            .WithCleanSession()
            .Build();
            #region Using TLS/SSL
            /*  options = new MqttClientOptionsBuilder()
                  .WithTcpServer(broker, port) // MQTT broker address and port
                  .WithCredentials(username, password) // Set username and password
                  .WithClientId(clientId)
                  .WithCleanSession()
                  .WithTls(
                      o =>
                      {
                          // The used public broker sometimes has invalid certificates. This sample accepts all
                          // certificates. This should not be used in live environments.
                          o.CertificateValidationHandler = _ => true;

                          // The default value is determined by the OS. Set manually to force version.
                          o.SslProtocol = SslProtocols.Tls12; ;

                          // Please provide the file path of your certificate file. The current directory is /bin.
                          var certificate = new X509Certificate("/opt/emqxsl-ca.crt", "");
                          o.Certificates = new List<X509Certificate> { certificate };
                      }
                  )
                  .Build();
            */
            #endregion

            await ReconnectBrokerUsingEvent();
        }

        public async Task ReconnectBrokerUsingEvent()
        {
            // Thiết lập các hàm callback khi kết nối/ngắt kết nối và nhận message MQTT
            _mqttClient.ConnectedAsync += MqttConnectedEvent;
            _mqttClient.DisconnectedAsync += MqttDisconnectedEvent;
            _mqttClient.ApplicationMessageReceivedAsync += MqttMessageReceivedEvent;

            try
            {
                await _mqttClient.ConnectAsync(_options);
            }
            catch (MqttCommunicationException)
            {
                // Sự kiện DisconnectedAsync sẽ được kích hoạt
            }
        }

        private async Task MqttConnectedEvent(MqttClientConnectedEventArgs arg)
        {
            WriteLog("CONNECTED to MQTT broker successfully.");

            // Đăng ký lắng nghe các topic
            await _mqttClient.SubscribeAsync(TopicSub.IN_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.OUT_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.SYNC_DATABASE_SERVICE, MqttQualityOfServiceLevel.AtLeastOnce);

            // Gửi message yêu cầu đồng bộ dữ liệu
            PublishMessage("call", TopicPub.CALL_SYNC_DATABSE_SERVICE);
        }

        // Hàm xử lý sự kiện ngắt kết nối
        private async Task MqttDisconnectedEvent(MqttClientDisconnectedEventArgs arg)
        {
            WriteLog("RECONNECTING to server...");
            //Tái kết nối: Hàm thực hiện kết nối lại với MQTT Broker bằng cách sử dụng
            //các option đã được thiết lập trước đó(_options).
            await _mqttClient.ConnectAsync(_options);
            Thread.Sleep(1000);
        }

        // Hàm xử lý sự kiện khi nhận message từ publiser
        private async Task MqttMessageReceivedEvent(MqttApplicationMessageReceivedEventArgs arg)
        {
            // Lấy nội dung dạng string
            string message = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.ToArray());
            // Lấy chủ đề của message   
            string topic = arg.ApplicationMessage.Topic;

            // Xử lý phản hồi về message đi vào/ra
            if (topic == TopicSub.IN_MESSAGE_FEEDBACK || topic == TopicSub.OUT_MESSAGE_FEEDBACK)
            {
                // Cập nhật thời gian cập nhật (`lastUpdate`), trạng thái trong (`isInNg`) và trạng thái ngoài (`isInXe`) của thẻ
                // dựa vào tid (mã nhận dạng thẻ) trong database `TagInfo`
                await SqliteDataAccess.UpdateByKey(
                  "TagInfo",
                  new string[] { "lastUpdate", "isInNg", "isInXe" },
                  new string[] {
                      DateTime.UtcNow.ToString(SqliteDataAccess._timeFormat),
                      (topic == TopicSub.IN_MESSAGE_FEEDBACK) ? "1" : "0",
                      (topic == TopicSub.IN_MESSAGE_FEEDBACK) ? "1" : "0"
                  },
                  "tidXe", message
                );
                return;
            }

            // Xử lý yêu cầu đồng bộ dữ liệu
            if (topic == TopicSub.SYNC_DATABASE_SERVICE)
            {
                // Chuyển đổi nội dung JSON của message thành danh sách các đối tượng ETagInfoSync
                List<ETagInfoSync> entities = JsonConvert.DeserializeObject<List<ETagInfoSync>>(message);
                if (entities.Count == 0)
                    return;
                // Duyệt qua từng đối tượng trong danh sách entities
                for (int i = entities.Count - 1; i >= 0; i--)
                {
                    await SqliteDataAccess.UpdateByKey("TagInfo", new string[] { "isInNg", "isInXe" },
                        new string[] { entities[i].isInNg == 1 ? "1" : "0", entities[i].isInXe == 1 ? "1" : "0" },
                        "tidNg", entities[i].tidNg
                        );
                }
                return;
            }
        }

        public static async void PublishMessage(string message, string topic)
        {
            if (_mqttClient == null) return;
            if (!_mqttClient.IsConnected) { return; }

            MqttApplicationMessage messageBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                //.WithRetainFlag()// Thiết lập cờ lưu trữ (giữ message mới nhất trên chủ đề)
                .Build();
            try
            {
                await _mqttClient.PublishAsync(messageBuilder);
            }
            catch (Exception) { }
            /*WriteLog($"SENDING message= [{message}], " +
                $"topic= [{topic}] " +
                $"({Encoding.UTF8.GetByteCount(message)} bytes)");*/
        }

        #endregion

        #region Thread handle
        private void MqttThrd()
        {
            while (true)
            {
                try
                {
                    Inventory();
                }
                catch (Exception e)
                {
                    WriteLog(e.Message);
                }
                Thread.Sleep(_maxInventoryTime);
                /* TagInfo tagInfo = new TagInfo("0D742810BFFF4BD884A5D9A7", "2000502486029CBE", "00001111", "crc", "rssi", "ant", true);
                 Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                 tagInfo = new TagInfo("E28011700000020E26B66B48", "20001348D6CD09C4", "00002222", "crc", "rssi", "ant", false);
                 Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);

                 tagInfo = new TagInfo("E28011700000020E26B6625F", "2000125FD6CC09C4", "00004444", "crc", "rssi", "ant", false);
                 Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);*/

                /*
                 * Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                Thread.Sleep(t);
                Thread.Sleep(_maxInventoryTime);

                tagInfo = new TagInfo("0D742810BFFF4BD884A5D9A7", "2000502486029CBE", "00001111", "crc", "rssi", "ant", true);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                tagInfo = new TagInfo("E28011700000020E26B66B48", "20001348D6CD09C4", "00002222", "crc", "rssi", "ant", false);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                Thread.Sleep(t);
                Thread.Sleep(_maxInventoryTime);

                tagInfo = new TagInfo("017EACEBF7884AC782D1C47A", "2000402486021CD7", "00003333", "crc", "rssi", "ant", true);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                tagInfo = new TagInfo("E28011700000020E26B6625F", "2000125FD6CC09C4", "00004444", "crc", "rssi", "ant", false);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                Thread.Sleep(t);
                Thread.Sleep(_maxInventoryTime);

                tagInfo = new TagInfo("017EACEBF7884AC782D1C47A", "2000402486021CD7", "00003333", "crc", "rssi", "ant", true);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                tagInfo = new TagInfo("E28011700000020E26B6625F", "2000125FD6CC09C4", "00004444", "crc", "rssi", "ant", false);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
                Thread.Sleep(t);*/
            }
        }

        private void ReaderConnectThrd()
        {
            while (true)
            {
                Thread.Sleep(_readerReconnectTime);
                string port = InitSerialPort();
                string r;
                ReaderType choice;
                if (Enum.TryParse(_readerType.ToString(), out choice))
                {
                    r = (UInt16)choice + "";
                }
                else
                {
                    r = "-1";
                    /* error: the string was not an enum member */
                }

                if (port == null && _connectType != ConnectType.USB)
                {
                    PublishMessage(r + 'D', TopicPub.READER_STATUS); //Disconnected
                    _isReaderDisconnect = false;
                    continue;
                }
                _isReaderDisconnect = InitReader(port);

                if (_isReaderDisconnect)
                {
                    PublishMessage(r + 'C', TopicPub.READER_STATUS); // Connected
                    continue;
                }
                PublishMessage(r + 'D', TopicPub.READER_STATUS); // Disconnected
            }
        }

        #endregion

        #region reader reconnect / disconnect
        public bool Connect(ConnectType connectType, string port, ReaderType readerType)
        {
            bool result = false;
            if (switchDLL == null)
                switchDLL = new SwitchDLL(readerType);
            result = switchDLL.Connect(port);
            return result;
        }
        public bool Disconnect(ConnectType connectType)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region reader inventory

        public void Inventory()
        {
            if (!_isReaderDisconnect)
                return;
            switchDLL.Inventory();
        }
        #endregion
    }
}
