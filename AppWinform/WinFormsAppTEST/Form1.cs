using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System.Configuration;
using System.Text;

namespace WinFormsAppTEST
{
    public partial class Form1 : Form
    {
        private MqttFactory _factoryMQTT;
        private MqttClientOptions _options;
        // Create a MQTT client instance
        private static string _host = "nsp.t4tek.tk";
        private static IMqttClient _mqttClient;

        private string _brokerIP = "127.0.0.1";
        private string _brokerUserName = "nhandev";
        private string _brokerPassword = "123456";
        private string _clientID;
        private int _brokerPort = 1883;
        private List<DTOTagInfo> l = new();
        public Form1()
        {
            InitializeComponent();
            comboBox1.SelectedIndex = 0;
            Task.Run(() =>
            {
                ReconnectSubscribeToBroker();
            });
            l.Add(new(
                    "Đặng Văn A",
                    "ABCD1234",
                    "0D742810BFFF4BD884A5D9A7",
                    "E28011700000020E26B66B48",
                    "2000502486029CBE",
                    "20001348D6CD09C4",
                    "00001111",
                    "00002222",
                    "Honda",
                    false, false
                    ));
            l.Add(new(
                    "Đặng Văn B",
                    "ABCD1235",
                    "017EACEBF7884AC782D1C47A",
                    "E28011700000020E26B6625F",
                    "2000402486021CD7",
                    "2000125FD6CC09C4",
                    "00003333",
                    "00004444",
                    "Z1000",
                    false, false
                    ));
            l.Add(new(
                    "Đặng Văn C",
                    "ABCD1236",
                    "12D92ECA27B7491AB729CF2D",
                    "E28011700000020E26B6625D",
                    "200040248602E0BE",
                    "2000025DD6CC09C4",
                    "00005555",
                    "00006666",
                    "Yamaha",
                    false, false
                   ));
            l.Add(new(
                    "Đặng Văn D",
                    "ABCD1237",
                    "E280689400005024860230D7",
                    "E28011700000020E26B66B46",
                    "20005024860230D7",
                    "20000346D6CD09C4",
                    "00007777",
                    "00008888",
                    "BMW",
                   false, false
                   ));
            l.Add(new(
                    "Đặng Văn E",
                    "ABCD1238",
                    "014990BEB51E40E0AF7EE0D8",
                    "E28011700000020E26B66B4A",
                    "20005022E85490A4",
                    "2000034AD6CD09C4",
                    "00009999",
                    "11110000",
                    "Mercedes",
                    false, false
                   ));
        }
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
            // Subscribe to a topic
            await _mqttClient.SubscribeAsync(TopicSub.IN_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.OUT_MESSAGE_FEEDBACK, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqttClient.SubscribeAsync(TopicSub.SYNC_DATABASE_SERVICE, MqttQualityOfServiceLevel.AtLeastOnce);

            // Gửi message yêu cầu đồng bộ dữ liệu
            PublishMessage("call", TopicPub.CALL_SYNC_DATABSE_SERVICE);
        }

        // Hàm xử lý sự kiện ngắt kết nối
        private async Task MqttDisconnectedEvent(MqttClientDisconnectedEventArgs arg)
        {
            await _mqttClient.ConnectAsync(_options);
            Thread.Sleep(1000);
        }

        // Hàm xử lý sự kiện khi nhận message từ publiser
        private Task MqttMessageReceivedEvent(MqttApplicationMessageReceivedEventArgs arg)
        {
            string message = Encoding.UTF8.GetString(arg.ApplicationMessage.PayloadSegment.ToArray());
            if (message.Length < 2)
                return Task.CompletedTask;
            string topic = arg.ApplicationMessage.Topic;
            string tid = message.Substring(0);
            /*WriteLog($"RECEIVED message= [{message}], " +
                $"topic= [{topic}] " +
                $"({Encoding.UTF8.GetByteCount(message)} bytes)");*/

            // Nếu nhận dữ liệu feedback từ form có nghĩa là đã nhận được dữ liệu
            if (topic == TopicSub.IN_MESSAGE_FEEDBACK)
            {
                listBox1.Items.Insert(0, $"{DateTime.Now}: {message}:{topic}");
                return Task.CompletedTask;
            }

            // Nếu nhận dữ liệu feedback từ form có nghĩa là đã nhận được dữ liệu
            if (topic == TopicSub.OUT_MESSAGE_FEEDBACK)
            {
                listBox1.Items.Insert(0, $"{DateTime.Now}: {message}:{topic}");
                return Task.CompletedTask;
            }
            if (topic == TopicSub.SYNC_DATABASE_SERVICE)
            {
                listBox1.Items.Insert(0, $"{DateTime.Now}: {message}:{topic}");
                return Task.CompletedTask;
            }
            return Task.CompletedTask;
        }

        public static async void PublishMessage(string message, string topic)
        {
            if (_mqttClient == null) return;
            if (!_mqttClient.IsConnected) { return; }

            MqttApplicationMessage messageBuilder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
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
        private void button1_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
            tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
            tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_MESSAGE);
        }

        private void button6_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
        }

        private void button5_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_MESSAGE);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_FAIL_ENC_PASS);
        }


        private void button10_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_FAIL_ENC_PASS);
        }

        private void button9_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcXe, dto.tidXe, dto.passXe, "crc", "rssi", "ant", false);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.OUT_FAIL_ENC_PASS);
        }

        private void button8_Click(object sender, EventArgs e)
        {
            DTOTagInfo dto = l[comboBox1.SelectedIndex];
            TagInfo tagInfo = new(dto.epcNg, dto.tidNg, dto.passNg, "crc", "rssi", "ant", true);
            PublishMessage(JsonConvert.SerializeObject(tagInfo), TopicPub.IN_FAIL_ENC_PASS);
        }
    }
}