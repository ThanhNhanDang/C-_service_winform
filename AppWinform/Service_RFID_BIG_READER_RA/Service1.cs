using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Protocol;
using MQTTnet.Server;
using Newtonsoft.Json;
using Service_RFID_BIG_READER.Database;
using Service_RFID_BIG_READER.Entity;
using Service_RFID_BIG_READER.Reader_DLL;
using Service_RFID_BIG_READER.Util;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Net.NetworkInformation;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Service_RFID_BIG_READER.Reader_DLL.Options;


namespace Service_RFID_BIG_READER
{
    [RunInstaller(true)]
    public partial class Service1 : ServiceBase
    {
        #region MQTT
        // Create a MQTT client factory
        private MqttFactory _factoryMQTT;
        private MqttClientOptions _options;
        // Create a MQTT client instance
        private static string _host = "nsp.t4tek.tk";
        private static IMqttClient _mqttClient;

        private string _brokerIP = ConfigurationManager.AppSettings["serverIP"];
        private string _brokerUserName = ConfigurationManager.AppSettings["userName"];
        private string _brokerPassword = ConfigurationManager.AppSettings["password"];
        private string _clientID;
        private int _brokerPort = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]);
        #endregion

        #region thread

        private Thread _inventoryThrd = null;
        private Thread _readerConnectThrd = null;
        #endregion

        #region log
        private static string _pathLogFile = ConfigurationManager.AppSettings["pathLogFile"];
        #endregion

        #region reader
        public static ConnectType _connectType;
        public static ReaderType _readerType;
        public static bool _isReaderDisconnect = false;
        private int _maxInventoryTime = Convert.ToInt32(ConfigurationManager.AppSettings["maxInventoryTime"]);
        private int _readerReconnectTime = Convert.ToInt32(ConfigurationManager.AppSettings["readerReconnectTime"]);
        private string _connectDivide = ConfigurationManager.AppSettings["connectDevide"];
        private SwitchDLL switchDLL = null;
        #endregion

        public Service1()
        {
            InitializeComponent();
        }
        #region service event
        public void OnDebug()
        {
            OnStart(null);
        }
        protected override void OnStart(string[] args)
        {
            WriteLog($"Windows Service is called on {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt")}.");

            InitConnectAndReaderType();

            _isReaderDisconnect = InitReader(InitSerialPort());
            Task.Run(() =>
            {
                ReconnectSubscribeToBroker();
            });

            // Process.Start("D:\\workspaces\\t4tek\\2024\\Pub_Sub_Mqtt\\AppWinform\\AppWinform\\bin\\Debug\\net6.0-windows\\AppWinform.exe");
            _inventoryThrd = new Thread(new ThreadStart(MqttThrd));
            _inventoryThrd.Start();
            _inventoryThrd.IsBackground = true;

            _readerConnectThrd = new Thread(new ThreadStart(ReaderConnectThrd));
            _readerConnectThrd.Start();
            _readerConnectThrd.IsBackground = true;

        }


        protected override void OnStop()
        {
            try
            {
                if (_inventoryThrd != null & _inventoryThrd.IsAlive)
                {
                    _inventoryThrd.Abort();
                }
                if (_readerConnectThrd != null & _readerConnectThrd.IsAlive)
                {
                    _readerConnectThrd.Abort();
                }
                if (_mqttClient.IsConnected)
                {
                    _mqttClient.DisconnectAsync();
                    _mqttClient.Dispose();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        #endregion

        #region init
        private bool InitConnectAndReaderType()
        {
            string connectType = ConfigurationManager.AppSettings["connectType"];
            string readerType = ConfigurationManager.AppSettings["readerType"];
            if (connectType == null || readerType == null)
            {
                WriteLog("ERROR ConnectType or ReaderType not found!");
                return false;
            }
            try
            {
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
            /* string portName = ConfigurationManager.AppSettings["comPort"];
             if (portName != null)
                 return portName;

             string[] portNames = SerialPort.GetPortNames();

             if (portNames.Length == 0)
                 return null;
             return portNames[0];
 */
            ManagementObjectSearcher searcher =
                   new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE Caption like '%(COM%'");
            if (searcher == null) return null;

            IEnumerable<string> ports = searcher.Get().Cast<ManagementBaseObject>().ToList().Select(p => p["caption"].ToString());

            string port_select = ports.FirstOrDefault(p => p.Contains(_connectDivide));
            if (port_select == null) return null;

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
            if (port == null && _connectType != ConnectType.USB) return false;
            if (_isReaderDisconnect)
                return true;

            if (!InitConnectAndReaderType()) return false;

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

            // Create MQTT client options
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
            // Callback function khi kết nối với broker
            _mqttClient.ConnectedAsync += MqttConnectedEvent;

            // Callback function khi NGẮT kết nối với broker
            _mqttClient.DisconnectedAsync += MqttDisconnectedEvent;

            // Callback function when a message is received
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

            // Subscribe to a topic
            await _mqttClient.SubscribeAsync(TopicSub.IN_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.OUT_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.SYNC_DATABASE_SERVICE, MqttQualityOfServiceLevel.AtLeastOnce);

            PublishMessage("call", TopicPub.CALL_SYNC_DATABSE_SERVICE);
        }

        // Hàm xử lý sự kiện ngắt kết nối
        private async Task MqttDisconnectedEvent(MqttClientDisconnectedEventArgs arg)
        {
            WriteLog("RECONNECTING to server...");
            await _mqttClient.ConnectAsync(_options);
            Thread.Sleep(1000);
        }

        // Hàm xử lý sự kiện khi nhận message từ publiser
        private async Task MqttMessageReceivedEvent(MqttApplicationMessageReceivedEventArgs arg)
        {
            string message = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.ToArray());
            if (message.Length < 2)
                return;
            string topic = arg.ApplicationMessage.Topic;
            string tid = message.Substring(0);
            /*WriteLog($"RECEIVED message= [{message}], " +
                $"topic= [{topic}] " +
                $"({Encoding.UTF8.GetByteCount(message)} bytes)");*/

            // Nếu nhận dữ liệu feedback từ form có nghĩa là đã nhận được dữ liệu
            if (topic == TopicSub.IN_MESSAGE_FEEDBACK)
            {
                await SqliteDataAccess.UpdateByKey(
                    "TagInfo",
                    new string[] { "lastUpdate", "isInNg", "isInXe" },
                    new string[] { DateTime.UtcNow.ToString(SqliteDataAccess._timeFormat), "1", "1" },
                    "tidNg", tid
                    );
                return;
            }

            // Nếu nhận dữ liệu feedback từ form có nghĩa là đã nhận được dữ liệu
            if (topic == TopicSub.OUT_MESSAGE_FEEDBACK)
            {
                await SqliteDataAccess.UpdateByKey(
                    "TagInfo",
                    new string[] { "lastUpdate", "isInNg", "isInXe" },
                    new string[] { DateTime.UtcNow.ToString(SqliteDataAccess._timeFormat), "0", "0" },
                    "tidNg", tid
                    );
                return;
            }
            if (topic == TopicSub.SYNC_DATABASE_SERVICE)
            {

                List<ETagInfoSync> entities = JsonConvert.DeserializeObject<List<ETagInfoSync>>(message);
                if (entities.Count == 0)
                    return;
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
                .WithRetainFlag()
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
                Inventory();
                /*TagInfo tagInfo = new TagInfo("0D742810BFFF4BD884A5D9A7", "2000502486029CBE", "00001111", "crc", "rssi", "ant", true);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
                tagInfo = new TagInfo("E28011700000020E26B66B48", "20001348D6CD09C4", "00002222", "crc", "rssi", "ant", false);
                Service1.PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
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
                Thread.Sleep(_maxInventoryTime);
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
