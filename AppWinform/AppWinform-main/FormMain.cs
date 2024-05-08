using System.Media;
using System.Diagnostics;
using System.Configuration;
using System.Runtime.InteropServices;

using Emgu.CV;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
using MQTTnet.Exceptions;

using Newtonsoft.Json;

using AppWinform_main.DTO;
using AppWinform_main.Reader;
using AppWinform_main.Entity;
using AppWinform_main.Service;
using AppWinform_main.Database;
using AppWinform_main.MQTT_Util;

namespace AppWinform_main
{
    public partial class FormMain : Form
    {

        private Dictionary<string, string> _dayOfWeek = new Dictionary<string, string>() {
                { "Monday", "Thứ 2" },
                { "Tuesday", "Thứ 3" },
                { "Wednesday", "Thứ 4" },
                { "Thursday", "Thứ 5" },
                { "Friday", "Thứ 6" },
                { "Saturday", "Thứ 7" },
                { "Sunday", "CN" },
            };

        #region MQTT
        // Create a MQTT client factory
        private static MqttFactory _factory_MQTT;
        // Create a MQTT client instance
        private static IMqttClient _mqtt_Client;
        private string _client_ID;
        private MqttClientOptions _options;
        private string? _brokerIP = ConfigurationManager.AppSettings["serverIP"];
        private string? _brokerUserName = ConfigurationManager.AppSettings["userName"];
        private string? _brokerPassword = ConfigurationManager.AppSettings["password"];
        private int? _brokerPort = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]);
        #endregion

        #region RFID 
        private Dictionary<string, TagAlert> _tagAlertDict = new Dictionary<string, TagAlert>();

        private string? _audio = $"{AppContext.BaseDirectory}Audio\\";
        private SoundPlayer _soundPlayer = new SoundPlayer();
        private byte _timeLimitInOut = 1;
        #endregion

        #region Camera
        private int _isTurnOnCamera = Convert.ToInt32(ConfigurationManager.AppSettings["isTurnOnCamera"]);
        private string? _cameraVao1Url = ConfigurationManager.AppSettings["cameraVaoTruoc"];
        private string? _cameraVao2Url = ConfigurationManager.AppSettings["cameraVaoSau"];
        private string? _cameraRa1Url = ConfigurationManager.AppSettings["cameraRaTruoc"];
        private string? _cameraRa2Url = ConfigurationManager.AppSettings["cameraRaSau"];

        private VideoCapture _captureVao1;
        private VideoCapture _captureVao2;
        private Mat _frameVao1;
        private Mat _frameVao2;
        #endregion

        #region Thread
        private Thread _checkServiceThr;
        private Thread _dateTimeThr;
        private Thread _IsLimitReachedThr;
        private byte _bcheckServiceIn = 0;
        private byte _bcheckServiceOut = 0;
        #endregion

        #region Update Homepage
        private string _pathSave = Application.StartupPath + @"Images\User";
        private string _timeFormat = "dd-MM-yyy, hh:mm:ss tt";
        #endregion

        #region Responsive
        // Constants for mouse events
        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        #endregion

        #region Import user32.dll functions
        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        #endregion

        #region Service
        private TagInfoService _tagInfoService = new TagInfoService();
        private HistoryInService _historyInService = new HistoryInService();
        private HistoryOutService _historyOutService = new HistoryOutService();
        #endregion

        public FormMain()
        {
            InitializeComponent();
        }

        #region Responsive Event

        private void panelTop_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                btnRestoreDown.Image = Properties.Resources.square;
                ReleaseCapture();
                _ = SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }
        #endregion

        #region Form Event
        private void FormMain_Load(object sender, EventArgs e)
        {

            Task.Run(() =>
            {
                /* 
                List<DTOTagInfo>? l = _tagInfoService.GetAll();
                 l.Add(new DTOTagInfo(
                         "Đặng Văn A",
                         "ABCD1234",
                         "0D742810BFFF4BD884A5D9A7",
                         "E28011700000020E26B66B48",
                         "2000502486029CBE",
                         "20001348D6CD09C4",
                         "00001111",
                         "00002222",
                         "Honda",
                         false
                         ));
                 l.Add(new DTOTagInfo(
                         "Đặng Văn B",
                         "ABCD1235",
                         "017EACEBF7884AC782D1C47A",
                         "E28011700000020E26B6625F",
                         "2000402486021CD7",
                         "2000125FD6CC09C4",
                         "00003333",
                         "00004444",
                         "Z1000",
                         false
                         ));
                 l.Add(new DTOTagInfo(
                         "Đặng Văn C",
                         "ABCD1236",
                         "12D92ECA27B7491AB729CF2D",
                         "E28011700000020E26B6625D",
                         "200040248602E0BE",
                         "2000025DD6CC09C4",
                         "00005555",
                         "00006666",
                         "Yamaha",
                         false
                        ));
                 l.Add(new DTOTagInfo(
                         "Đặng Văn D",
                         "ABCD1237",
                         "E280689400005024860230D7",
                         "E28011700000020E26B66B46",
                         "20005024860230D7",
                         "20000346D6CD09C4",
                         "00007777",
                         "00008888",
                         "BMW",
                        false
                        ));
                 l.Add(new DTOTagInfo(
                         "Đặng Văn E",
                         "ABCD1238",
                         "014990BEB51E40E0AF7EE0D8",
                         "E28011700000020E26B66B4A",
                         "20005022E85490A4",
                         "2000034AD6CD09C4",
                         "00009999",
                         "11110000",
                         "Mercedes",
                         false
                        ));

                 for (int i = 0; i < 5; i++)
                 {
                     SqliteDataAccessImpl.SaveTag(l[i]);
                 }*/
                /*  List<DTOTagInfo>? tagList = SqliteDataAccessImpl.LoadTag();
                  if (tagList == null)
                  {
                      MessageBox.Show("Danh Sách trống");
                  }*/
                ConnectSubscribeToTheMQTTBroker();
            });
            Task.Run(() =>
            {
                _checkServiceThr = new Thread(new ThreadStart(CheckServiceThrd));
                _checkServiceThr.Start();
                _checkServiceThr.IsBackground = true;

                _dateTimeThr = new Thread(new ThreadStart(DateTimeThr));
                _dateTimeThr.Start();
                _dateTimeThr.IsBackground = true;

                _IsLimitReachedThr = new Thread(new ThreadStart(HandleTagAlertsThr));
                _IsLimitReachedThr.Start();
                _IsLimitReachedThr.IsBackground = true;
            });
            InitCamera();
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_mqtt_Client == null)
                return;
            if (_mqtt_Client.IsConnected)
            {
                _mqtt_Client.DisconnectAsync();
                _mqtt_Client.Dispose();
            }

        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("Tải xong");
        }
        #endregion

        #region Init
        private void InitCamera()
        {
            if (_isTurnOnCamera == 0)
                return;
            Task.Run(() =>
            {
                _captureVao1?.Dispose();
                //Set up capture device
                _captureVao1 = new VideoCapture(_cameraVao1Url);
                _captureVao1.ImageGrabbed += CaptureVao1_ImageGrabbed;
                _captureVao1.Start();

                _frameVao1?.Dispose();
                _frameVao1 = new Mat();

                _captureVao2?.Dispose();
                //Set up capture device
                _captureVao2 = new VideoCapture(_cameraVao2Url);
                _captureVao2.ImageGrabbed += CaptureVao2_ImageGrabbed;
                _captureVao2.Start();

                _frameVao2?.Dispose();
                _frameVao2 = new Mat();
            });
        }
        #endregion

        #region Camera
        private void CaptureVao1_ImageGrabbed(object? sender, EventArgs e)
        {
            bool read_success = _captureVao1.Read(_frameVao1);
            if (read_success)
            {
                try
                {
                    this.Invoke(new MethodInvoker(delegate ()
                    {
                        ptbVao1.Image?.Dispose();
                        ptbVao1.Image = _frameVao1.ToBitmap();
                    }));
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine("Exception _captureVao1");
                }
            }
            else
                Thread.Sleep(50);
        }


        private void CaptureVao2_ImageGrabbed(object? sender, EventArgs e)
        {
            bool read_success = _captureVao2.Read(_frameVao2);
            if (read_success)
            {
                try
                {
                    this.Invoke(new MethodInvoker(delegate ()
                    {
                        ptbVao3.Image?.Dispose();
                        ptbVao3.Image = _frameVao2.ToBitmap();
                    }));
                }
                catch (Exception)
                {
                    System.Diagnostics.Debug.WriteLine("Exception _captureVao2");
                }
            }
            else
                Thread.Sleep(50);
        }
        #endregion

        #region Btn Event

        #region Close Form
        private void btnCloseForm_MouseEnter(object sender, EventArgs e)
        {
            btnCloseForm.BackColor = Color.Red;
            btnCloseForm.Image = Properties.Resources.CloseW;
        }

        private void btnCloseForm_MouseLeave(object sender, EventArgs e)
        {
            btnCloseForm.BackColor = Color.White;
            btnCloseForm.Image = Properties.Resources.Close;
        }

        private void btnCloseForm_Click(object sender, EventArgs e)
        {
            Close();
        }
        #endregion

        #region Restore Down
        private void btnRestoreDown_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Maximized)
            {
                this.WindowState = FormWindowState.Normal;
                this.Size = new Size(1000, 900);
                btnRestoreDown.Image = Properties.Resources.square;
            }
            else
            {
                this.WindowState = FormWindowState.Maximized;
                btnRestoreDown.Image = Properties.Resources.RestoreDdown;
            }
        }
        #endregion

        #region Minisize Form
        private void btnMinisize_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }
        #endregion

        #region Add User
        private void btnAddUser_Click(object sender, EventArgs e)
        {
            FormSaveImage formSaveImage = new FormSaveImage();
            formSaveImage.ShowDialog();
        }
        #endregion

        #endregion

        #region MQTT

        #region ReConnect/Disconnect
        // Kết nối và đăng ký topic
        private async void ConnectSubscribeToTheMQTTBroker()
        {
            _factory_MQTT = new MqttFactory();
            _mqtt_Client = _factory_MQTT.CreateMqttClient();
            _client_ID = Guid.NewGuid().ToString();

            // Create MQTT client options
            _options = new MqttClientOptionsBuilder()
            .WithTcpServer(_brokerIP, _brokerPort) // MQTT broker address and port
            .WithCredentials(_brokerUserName, _brokerPassword) // Set username and password
            .WithClientId(_client_ID)
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

            await Reconnect_Using_Event();
            // Subscribe
        }

        public async Task Reconnect_Using_Event()
        {
            // Callback function khi kết nối với broker
            _mqtt_Client.ConnectedAsync += Mqtt_Client_ConnectedAsync;

            // Callback function khi NGẮT kết nối với broker
            _mqtt_Client.DisconnectedAsync += Mqtt_Client_DisconnectedAsync;

            // Callback function when a message is received
            _mqtt_Client.ApplicationMessageReceivedAsync += Mqtt_Client_ApplicationMessageReceivedAsync;

            try
            {
                await _mqtt_Client.ConnectAsync(_options);
            }
            catch (MqttCommunicationException)
            {
                // Sự kiện DisconnectedAsync sẽ được kích hoạt
            }

        }

        private async Task Mqtt_Client_ConnectedAsync(MqttClientConnectedEventArgs arg)
        {
            ptbMqttStatus.Invoke(new MethodInvoker(() =>
            {
                ptbMqttStatus.Tag = "0";
                ptbMqttStatus.Image = Properties.Resources.server_connect;
            }));

            List<ETagInfoSync>? entities = await _tagInfoService.SyncDatbase();
            PublishMessage(JsonConvert.SerializeObject(entities), TopicPub.SYNC_DATABASE_SERVICE);
            // Subscribe to a topic
            await _mqtt_Client.SubscribeAsync(TopicSub.IN_MESSAGE, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt_Client.SubscribeAsync(TopicSub.IN_FAIL_ENC_PASS, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt_Client.SubscribeAsync(TopicSub.OUT_MESSAGE, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt_Client.SubscribeAsync(TopicSub.OUT_FAIL_ENC_PASS, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt_Client.SubscribeAsync(TopicSub.READER_STATUS, MqttQualityOfServiceLevel.AtLeastOnce);
            await _mqtt_Client.SubscribeAsync(TopicSub.CALL_SYNC_DATABSE_SERVICE, MqttQualityOfServiceLevel.AtLeastOnce);
        }

        // Hàm xử lý sự kiện ngắt kết nối
        private async Task Mqtt_Client_DisconnectedAsync(MqttClientDisconnectedEventArgs arg)
        {
            ptbMqttStatus.Invoke(new MethodInvoker(() =>
            {

                if (ptbMqttStatus.Tag.ToString() == "0")
                {
                    ptbMqttStatus.Image?.Dispose();
                    ptbMqttStatus.Image = Properties.Resources.server_disconnect_2;
                    ptbMqttStatus.Tag = "1";
                }
                else
                {
                    ptbMqttStatus.Image?.Dispose();
                    ptbMqttStatus.Image = Properties.Resources.server_disconnect;
                    ptbMqttStatus.Tag = "0";
                }
            }));
            Thread.Sleep(1000);

            await _mqtt_Client.ConnectAsync(_options);

        }

        #endregion

        #region MessageReceived
        // Hàm xử lý sự kiện khi nhận message từ publiser

        private void UpdateIconReaderStatus(bool isIn, bool isConnect, string tag)
        {
            if (isConnect)
            {
                if (isIn)
                {
                    if (ptbReaderIn1.Tag.ToString() != tag)
                    {
                        ptbReaderIn1.Tag = tag;
                        ptbReaderIn1.Image?.Dispose();
                        ptbReaderIn1.Image = Properties.Resources.Connect_rfid;
                        return;
                    }
                    return;
                }
                if (ptbReaderOut1.Tag.ToString() != tag)
                {
                    ptbReaderOut1.Tag = tag;
                    ptbReaderOut1.Image?.Dispose();
                    ptbReaderOut1.Image = Properties.Resources.Connect_rfid;
                    return;
                }
                return;
            }

            if (isIn)
            {
                if (ptbReaderIn1.Tag.ToString() != tag)
                {
                    ptbReaderIn1.Tag = tag;
                    ptbReaderIn1.Image?.Dispose();
                    ptbReaderIn1.Image = Properties.Resources.Disconnect_rfid;
                    return;
                }
                return;
            }
            if (ptbReaderOut1.Tag.ToString() != tag)
            {
                ptbReaderOut1.Tag = tag;
                ptbReaderOut1.Image?.Dispose();
                ptbReaderOut1.Image = Properties.Resources.Disconnect_rfid;
                return;
            }
            return;

        }
        private async Task Mqtt_Client_ApplicationMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs arg)
        {
            //  if (!_isStart) return;

            string message = arg.ApplicationMessage.ConvertPayloadToString();

            string topic = arg.ApplicationMessage.Topic;
            if (topic == TopicSub.READER_STATUS)
            {
                char r = message[0];
                char c = message[1];// SYC_R16 (1) Ra
                                    // ZTX_G20 (2) Vao
                                    // CF_RU6403 (3) Vao&Ra
                                    // C  Connected
                                    // D  Disconnect

                if (r == '3')
                {
                    _bcheckServiceIn = 0;
                    _bcheckServiceOut = 0;

                    Invoke(new MethodInvoker(() =>
                    {
                        if (c == 'C')
                        {
                            UpdateIconReaderStatus(true, true, "1");
                            UpdateIconReaderStatus(false, true, "1");
                            return; // MethodInvoker
                        }
                        if (c == 'D')
                        {
                            UpdateIconReaderStatus(true, false, "0");
                            UpdateIconReaderStatus(false, false, "0");
                            return; // MethodInvoker
                        }
                    }));
                    return; // if r
                }
                if (r == '2')
                {
                    _bcheckServiceIn = 0;

                    ptbReaderIn1.Invoke(new MethodInvoker(() =>
                    {
                        if (c == 'C')
                        {
                            UpdateIconReaderStatus(true, true, "1");
                            return; //MethodInvoker
                        }
                        if (c == 'D')
                        {
                            UpdateIconReaderStatus(true, false, "0");
                            return; // MethodInvoker
                        }
                    }));
                    return; // if r
                }
                if (r == '1')
                {
                    _bcheckServiceOut = 0;
                    ptbReaderOut1.Invoke(new MethodInvoker(() =>
                    {
                        if (c == 'C')
                        {
                            UpdateIconReaderStatus(false, true, "1");
                            return; // MethodInvoker
                        }
                        if (c == 'D')
                        {
                            UpdateIconReaderStatus(false, false, "0");
                            return; // MethodInvoker
                        }
                    }));
                    return; // if r
                }
                return; // if topic
            }

            if (topic == TopicSub.CALL_SYNC_DATABSE_SERVICE)
            {
                List<ETagInfoSync>? entities = await _tagInfoService.SyncDatbase();
                PublishMessage(JsonConvert.SerializeObject(entities), TopicPub.SYNC_DATABASE_SERVICE);
                return;
            }

            Reader.TagInfo? tagInfo = JsonConvert.DeserializeObject<Reader.TagInfo>(message);
            tagInfo = tagInfo ?? new Reader.TagInfo();

            if (topic == TopicSub.IN_MESSAGE)
            {
                try
                {
                    await HandleTagIn(tagInfo);
                    return;
                }
                catch (ArgumentException)
                {
                    System.Diagnostics.Debug.WriteLine("HandleTagIn ArgumentException");
                    return;
                }
            }

            if (topic == TopicSub.OUT_MESSAGE)
            {
                try
                {
                    await HandleTagOut(tagInfo);
                    return;
                }
                catch (ArgumentException)
                {
                    System.Diagnostics.Debug.WriteLine("HandleTagOut ArgumentException");
                    return;
                }
            }
            if (topic == TopicSub.IN_FAIL_ENC_PASS)
            {
                try
                {
                    CheckFailTag(null, tagInfo.tid, true, true);
                    //  await CheckInFailEncPass(tagInfo);
                }
                catch (ArgumentException)
                {
                    System.Diagnostics.Debug.WriteLine("CheckInFailEncPass ArgumentException");
                    return;
                }
            }
            if (topic == TopicSub.OUT_FAIL_ENC_PASS)
            {
                try
                {
                    CheckFailTag(null, tagInfo.tid, false, true);
                    return;
                }
                catch (ArgumentException)
                {
                    System.Diagnostics.Debug.WriteLine("CheckOutFailEncPass ArgumentException");
                    return;
                }
            }
            return;
        }
        #endregion

        #region HandleTagIn
        private bool IsLimitReached(DateTime dateTime)
        {
            int totalSeconds = (int)(DateTime.Now - dateTime).TotalSeconds;
            return totalSeconds <= _timeLimitInOut;
        }
        private bool CheckFailTag(DTOTagInfo? dto, string tid, bool isIn, bool isEncPass)
        {
            lock (_tagAlertDict)
            {
                if (dto == null)
                {
                    dto = _tagInfoService.FindOrByMulKey(new string[] { "tidNg", "tidXe" }, tid).Result;
                    if (dto == null) return false;
                }
                bool isNg = false;
                if (dto.tidNg == tid)
                    isNg = true;
                if (dto.isInNg != isIn)
                {
                    if (!_tagAlertDict.ContainsKey(dto.tidXe))
                    {
                        if (isNg) return false;
                        _tagAlertDict.Add(dto.tidXe, new TagAlert(
                            DateTime.Now,
                            dto.tidXe,
                            isNg && isEncPass,
                            !isNg && isEncPass,
                            isIn,
                            isNg,
                            !isNg
                        ));
                        return true;
                    }
                    else
                    {
                        TagAlert tagAlert = _tagAlertDict[dto.tidXe];
                        _tagAlertDict[dto.tidXe] = new TagAlert(
                            tagAlert.dateTime,
                            dto.tidXe,
                            isNg ? isEncPass : tagAlert.isEncPassNg,
                            isNg ? tagAlert.isEncPassXe : isEncPass,
                            tagAlert.isIn,
                            isNg || tagAlert.isNg,
                            !isNg || tagAlert.isXe
                        );
                        return true;
                    }
                }
                return false; // No update needed if isIn matches dto.isInNg
            }
        }

        private async Task<bool> HandleTagIn(Reader.TagInfo tagInfo)
        {
            DTOTagInfo? dto = _tagInfoService.FindOrByMulKey(new string[] { "tidNg", "tidXe" }, tagInfo.tid).Result;
            if (dto == null) return false;
            // Nếu xe đã vào thì thoát
            if (dto.isInXe)
                return false;

            //Nếu thẻ có trong danh sách trước đó thì cập nhật false;
            if (!CheckFailTag(dto, tagInfo.tid, true, false))
                return false;
            // Nếu thẻ có trong danh sách trước đó thì cập nhật false;
            // Kiểm tra nếu thẻ còn lại có tồn tại trong hàng đợi và thẻ có phải không sai mật khẩu
            TagAlert tagAlert = _tagAlertDict[dto.tidXe];
            if (!tagAlert.isIn)
                return false;
            if (tagInfo.isNg ? (tagAlert.isXe && !tagAlert.isEncPassXe) : (tagAlert.isNg && !tagAlert.isEncPassNg))
            {
                dto = await _tagInfoService.UpdateByMulKey(
                        new string[] { "lastUpdate", "isInNg", "isInXe" },
                        new string[] { DateTime.UtcNow.ToString(SqliteUtil.TIME_FORMAT), "1", "1" },
                        "tidXe", dto.tidXe
                        );
                if (dto == null) { return false; }
                await _historyInService.Save(new DTOHistoryIn(dto.id, "Test1", "Test2"));
                PublishMessage(dto.tidXe, TopicPub.IN_MESSAGE_FEEDBACK);
                PlaySound("sound_OK_IN.wav");
                await UpdateHomepageIn(dto, false, null);
                _tagAlertDict.Remove(dto.tidXe);
                return true;
            }
            return false;
        }
        #endregion

        #region HandleTagOut
        private async Task<bool> HandleTagOut(Reader.TagInfo tagInfo)
        {
            DTOTagInfo? dto = _tagInfoService.FindOrByMulKey(new string[] { "tidNg", "tidXe" }, tagInfo.tid).Result;

            if (dto == null)
                return false;
            if (dto.isInXe == false)
                return false;
            //Nếu thẻ có trong danh sách trước đó thì cập nhật false;
            if (!CheckFailTag(dto, tagInfo.tid, false, false))
                return false;
            //Nếu thẻ có trong danh sách trước đó thì cập nhật false;
            // Kiểm tra nếu thẻ còn lại có tồn tại trong hàng đợi và thẻ có phải không sai mật khẩu
            TagAlert tagAlert = _tagAlertDict[dto.tidXe];
            if (tagAlert.isIn)
                return false;
            if (tagInfo.isNg ? (tagAlert.isXe && !tagAlert.isEncPassXe) : (tagAlert.isNg && !tagAlert.isEncPassNg))
            {
                //Nếu thẻ còn lại mà item2 bằng true thì return
                DateTime dateTimeIn = dto.lastUpdate.ToUniversalTime();
                TimeSpan timeSpan = DateTime.UtcNow - dateTimeIn;
                string dateTime = "" + (int)timeSpan.Days + " Ngày - " + timeSpan.Hours + ":" + timeSpan.Minutes + ":" + timeSpan.Seconds;
                await _historyOutService.Save(new DTOHistoryOut(dto.id, dateTimeIn.ToString(SqliteUtil.TIME_FORMAT), dateTime, "Test1", "Test2"));
                dto = await _tagInfoService.UpdateByMulKey(
                        new string[] { "lastUpdate", "isInNg", "isInXe" },
                        new string[] { DateTime.UtcNow.ToString(SqliteUtil.TIME_FORMAT), "0", "0" },
                        "tidXe", dto.tidXe
                        );
                if (dto == null)
                    return false;

                PublishMessage(dto.tidXe, TopicPub.OUT_MESSAGE_FEEDBACK);
                PlaySound("sound_OK_OUT.wav");

                await UpdateHomepageOut(dto, false, null);
                _tagAlertDict.Remove(dto.tidXe);
                return true;
            }
            return false;
        }
        #endregion

        #region Update Hompage
        private async Task UpdateHomepageIn(DTOTagInfo dto, bool isAlert, string? message)
        {
            _ = Invoke(new MethodInvoker(async () =>
            {
                // Clear
                ptbVao2.Image?.Dispose();
                ptbVao2.Image = null;
                ptbVao4.Image?.Dispose();
                ptbVao4.Image = null;
                ptbVao5.Image?.Dispose();
                ptbVao5.Image = null;
                ptbCheckVao.Image?.Dispose();
                ptbCheckVao.Image = Properties.Resources.UnChecked;

                lbNameNg.Text = "";
                lbPhuongTien.Text = "";
                lbTGVao.Text = "";
                lbMaThe.Text = "";
                tbBienSo.Text = "";
                await Task.Delay(100);
                Image bienSo = new Bitmap($@"{_pathSave}{dto.imgBienSoPath}");
                ptbVao2.Image?.Dispose();
                ptbVao2.Image = new Bitmap($@"{_pathSave}{dto.imgNgPath}");
                ptbVao4.Image?.Dispose();
                ptbVao4.Image = new Bitmap($@"{_pathSave}{dto.imgXePath}");
                ptbVao5.Image?.Dispose();
                ptbVao5.Image = bienSo;
                ptbCheckVao.Image?.Dispose();

                lbNameNg.Text = dto.nameNg;
                lbPhuongTien.Text = dto.typeXe;
                lbMaThe.Text = dto.tidXe;
                tbBienSo.Text = dto.nameXe;
                if (!isAlert)
                {
                    ptbVao2.Tag = "0";
                    ptbVao4.Tag = "0";
                    ptbCheckVao.Image?.Dispose();
                    ptbCheckVao.Image = Properties.Resources._checked;
                    lbTGVao.BackColor = Color.FromArgb(224, 224, 224);
                    lbTGVao.ForeColor = Color.Navy;
                    lbTGVao.Text = dto.lastUpdate.ToString(_timeFormat);
                    ptbVaoBottom5.Image?.Dispose();
                    ptbVaoBottom5.Image = (ptbVaoBottom4.Image != null) ? (Image)ptbVaoBottom4.Image.Clone() : null;
                    ptbVaoBottom4.Image?.Dispose();
                    ptbVaoBottom4.Image = (ptbVaoBottom3.Image != null) ? (Image)ptbVaoBottom3.Image.Clone() : null;
                    ptbVaoBottom3.Image?.Dispose();
                    ptbVaoBottom3.Image = (ptbVaoBottom2.Image != null) ? (Image)ptbVaoBottom2.Image.Clone() : null;
                    ptbVaoBottom2.Image?.Dispose();
                    ptbVaoBottom2.Image = (ptbVaoBottom1.Image != null) ? (Image)ptbVaoBottom1.Image.Clone() : null;
                    ptbVaoBottom1.Image?.Dispose();
                    ptbVaoBottom1.Image = (Image)bienSo.Clone();
                }
                else
                {
                    if (dto.isInNg) return;
                    ptbCheckVao.Image?.Dispose();
                    ptbCheckVao.Image = Properties.Resources.UnChecked;
                    lbTGVao.BackColor = Color.Red;
                    lbTGVao.ForeColor = Color.White;
                    lbTGVao.Text = message;
                    ptbVao2.Tag = "1";
                    ptbVao4.Tag = "1";
                    ptbVao2.Invalidate();
                    ptbVao4.Invalidate();

                }
            }));
            await Task.Delay(100);

        }
        private async Task UpdateHomepageOut(DTOTagInfo dto, bool isAlert, string? message)
        {
            Invoke(new MethodInvoker(async () =>
            {
                ptbRa2.Image?.Dispose();
                ptbRa2.Image = null;
                ptbRa4.Image?.Dispose();
                ptbRa4.Image = null;
                ptbRa5.Image?.Dispose();
                ptbRa5.Image = null;
                ptbCheckRa.Image?.Dispose();
                ptbCheckRa.Image = Properties.Resources.UnChecked;

                lbNameNg1.Text = "";
                lbPhuongTien1.Text = "";
                lbTGGui1.Text = "";
                lbMaThe1.Text = "";
                tbBienSo1.Text = "";

                await Task.Delay(100);

                Image bienSo = new Bitmap($@"{_pathSave}{dto.imgBienSoPath}");

                ptbRa2.Image?.Dispose();
                ptbRa2.Image = new Bitmap($@"{_pathSave}{dto.imgNgPath}");
                ptbRa4.Image?.Dispose();
                ptbRa4.Image = new Bitmap($@"{_pathSave}{dto.imgXePath}");
                ptbRa5.Image?.Dispose();
                ptbRa5.Image = bienSo;
                ptbCheckRa.Image?.Dispose();

                lbNameNg1.Text = dto.nameNg;
                lbPhuongTien1.Text = dto.typeXe;
                lbMaThe1.Text = dto.tidXe;
                tbBienSo1.Text = dto.nameXe;
                if (!isAlert)
                {
                    ptbRa2.Tag = "0";
                    ptbRa4.Tag = "0";
                    ptbCheckRa.Image?.Dispose();
                    ptbCheckRa.Image = Properties.Resources._checked;
                    lbTGGui1.BackColor = Color.FromArgb(224, 224, 224);
                    lbTGGui1.ForeColor = Color.Navy;
                    lbTGGui1.Text = dto.lastUpdate.ToString(_timeFormat);
                    ptbRaBottom5.Image?.Dispose();
                    ptbRaBottom5.Image = (ptbRaBottom4.Image != null) ? (Image)ptbRaBottom4.Image.Clone() : null;
                    ptbRaBottom4.Image?.Dispose();
                    ptbRaBottom4.Image = (ptbRaBottom3.Image != null) ? (Image)ptbRaBottom3.Image.Clone() : null;
                    ptbRaBottom3.Image?.Dispose();
                    ptbRaBottom3.Image = (ptbRaBottom2.Image != null) ? (Image)ptbRaBottom2.Image.Clone() : null;
                    ptbRaBottom2.Image?.Dispose();
                    ptbRaBottom2.Image = (ptbRaBottom1.Image != null) ? (Image)ptbRaBottom1.Image.Clone() : null;
                    ptbRaBottom1.Image?.Dispose();
                    ptbRaBottom1.Image = (Image)bienSo.Clone();
                }
                else
                {
                    if (!dto.isInNg) return;
                    ptbCheckRa.Image?.Dispose();
                    ptbCheckRa.Image = Properties.Resources.UnChecked;
                    lbTGGui1.BackColor = Color.Red;
                    lbTGGui1.ForeColor = Color.White;
                    lbTGGui1.Text = message;
                    ptbRa2.Tag = "1";
                    ptbRa4.Tag = "1";
                    ptbRa2.Invalidate();
                    ptbRa4.Invalidate();
                }
            }));

            await Task.Delay(100);
        }
        #endregion

        #region PublishMessage
        // Gửi message cho subcriber
        private async void PublishMessage(string message, string topic)
        {
            if (_mqtt_Client == null) return;
            if (!_mqtt_Client.IsConnected) { return; }
            MqttApplicationMessage message_Builder = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(message)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();
            await _mqtt_Client.PublishAsync(message_Builder);
        }
        #endregion

        #endregion

        #region Thread handle
        private void CheckServiceThrd()
        {
            while (true)
            {
                Thread.Sleep(1000);
                if (_bcheckServiceIn < 3)
                    _bcheckServiceIn++;
                if (_bcheckServiceOut < 3)
                    _bcheckServiceOut++;

                if (_bcheckServiceIn == 3)
                {
                    ptbReaderIn1.Invoke(new MethodInvoker(() =>
                    {
                        UpdateIconReaderStatus(true, false, "0");
                        return;
                    }));
                    _bcheckServiceIn++;
                }
                if (_bcheckServiceOut == 3)
                {
                    ptbReaderOut1.Invoke(new MethodInvoker(() =>
                    {
                        UpdateIconReaderStatus(false, false, "0");
                        return; // MethodInvoker
                    }));
                    _bcheckServiceOut++;
                }
            }
        }
        private void DateTimeThr()
        {
            
            while (true)
            {
                DateTime dateTime = DateTime.Now;
                Invoke(new MethodInvoker(() =>
                {
                    lbDate.Text = _dayOfWeek[dateTime.DayOfWeek.ToString()] + " " + dateTime.ToString("dd-MM-yyyy");
                    lbTime.Text = dateTime.ToString("HH:mm:ss");
                }));
                Thread.Sleep(1000);
            }
        }
        #region Handle Tag Alerts Thread

        //Phương thức HandleTagAlerts() là vòng lặp vô tận, kiểm tra từng cặp khóa-giá trị trong _tagAlertDict.
        private void HandleTagAlertsThr()
        {
            while (true)
            {
                try
                {
                    foreach (KeyValuePair<string, TagAlert> kvp in _tagAlertDict)
                    {
                        TagAlert tagAlert = kvp.Value;
                        // Nếu cảnh báo không bị giới hạn (theo thời gian),
                        // phương thức sẽ tìm kiếm thông tin thẻ từ _tagInfoService
                        // và gọi các phương thức xử lý cảnh báo đến và đi.
                        if (!IsLimitReached(tagAlert.dateTime))
                        {
                            DTOTagInfo? tagInfo = _tagInfoService.FindByKey("tidXe", kvp.Key).Result;
                            if (tagInfo == null)
                                continue;

                            //HandleIncomingAlert()
                            //xử lý cảnh báo đến bằng cách kiểm tra trạng thái isIn và isInNg của thẻ. 
                            //Nếu điều kiện đúng, nó sẽ phát ra âm thanh cảnh báo và gọi HandleMissingTagAlerts()
                            HandleIncomingAlert(tagAlert, tagInfo);
                            HandleOutgoingAlert(tagAlert, tagInfo);

                            tagAlert.dateTime = DateTime.Now;
                            _tagAlertDict[kvp.Key] = tagAlert;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Exception in HandleTagAlerts: {ex.Message}");
                }
                Thread.Sleep(50);
            }
        }

        private void HandleIncomingAlert(TagAlert tagAlert, DTOTagInfo tagInfo)
        {
            if (tagAlert.isIn && !tagInfo.isInNg)
            {
                PlaySound("sound_ng_IN.wav");
                HandleMissingTagAlerts(tagAlert, tagInfo, true);
            }
        }

        private void HandleOutgoingAlert(TagAlert tagAlert, DTOTagInfo tagInfo)
        {
            if (!tagAlert.isIn && tagInfo.isInNg)
            {
                PlaySound("sound_ng_OUT.wav");
                HandleMissingTagAlerts(tagAlert, tagInfo, false);
            }
        }

        // HandleMissingTagAlerts() xử lý các trường hợp thiếu thẻ hoặc mã khóa thẻ không hợp lệ.
        // Nó hiển thị thông báo lên giao diện và chờ 1 giây trước khi xử lý trường hợp tiếp theo.
        private void HandleMissingTagAlerts(TagAlert tagAlert, DTOTagInfo tagInfo, bool isIncoming)
        {
            string message;
            if ((tagAlert.isNg && !tagAlert.isEncPassXe) || tagAlert.isEncPassNg)
            {
                message = tagAlert.isEncPassNg ? "THẺ NGƯỜI SAI MK" : "THIẾU THẺ XE";
                UpdateHomepage(tagInfo, isIncoming, message);
                Thread.Sleep(1000);
            }

            if ((tagAlert.isXe && !tagAlert.isEncPassNg) || tagAlert.isEncPassXe)
            {
                message = tagAlert.isEncPassXe ? "THẺ XE SAI MK" : "THIẾU THẺ NGƯỜI";
                UpdateHomepage(tagInfo, isIncoming, message);
                Thread.Sleep(1000);
            }
        }
        //PlaySound() phát ra âm thanh tương ứng.
        private void PlaySound(string soundFile)
        {
            _soundPlayer.SoundLocation = _audio + soundFile;
            _soundPlayer.Load();
            _soundPlayer.Play();
        }

        //UpdateHomepage() cập nhật giao diện chính với thông báo tương ứng,
        //dựa trên thông tin thẻ và trạng thái đến/đi
        private async void UpdateHomepage(DTOTagInfo tagInfo, bool isIncoming, string message)
        {
            if (isIncoming)
                await UpdateHomepageIn(tagInfo, true, message);
            else
                await UpdateHomepageOut(tagInfo, true, message);
        }
        #endregion

        #endregion

        #region PainX 
        private void ptbVao2_Paint(object sender, PaintEventArgs e)
        {
            if ((string)ptbVao2.Tag == "0") return;
            // Thiết lập màu cho đường chéo
            using Pen pen = new Pen(Color.Red, 5);
            // Vẽ đường chéo từ góc trên trái sang góc dưới phải
            e.Graphics.DrawLine(pen, 0, 0, ptbVao2.Width, ptbVao2.Height);
            // Vẽ đường chéo từ góc dưới trái sang góc trên phải
            e.Graphics.DrawLine(pen, 0, ptbVao2.Height, ptbVao2.Width, 0);
        }

        private void ptbVao4_Paint(object sender, PaintEventArgs e)
        {
            if ((string)ptbVao4.Tag == "0") return;
            // Thiết lập màu cho đường chéo
            using Pen pen = new Pen(Color.Red, 5);
            // Vẽ đường chéo từ góc trên trái sang góc dưới phải
            e.Graphics.DrawLine(pen, 0, 0, ptbVao4.Width, ptbVao4.Height);
            // Vẽ đường chéo từ góc dưới trái sang góc trên phải
            e.Graphics.DrawLine(pen, 0, ptbVao4.Height, ptbVao4.Width, 0);
        }

        private void ptbRa4_Paint(object sender, PaintEventArgs e)
        {
            if ((string)ptbRa4.Tag == "0") return;
            // Thiết lập màu cho đường chéo
            using Pen pen = new Pen(Color.Red, 5);
            // Vẽ đường chéo từ góc trên trái sang góc dưới phải
            e.Graphics.DrawLine(pen, 0, 0, ptbRa4.Width, ptbRa4.Height);
            // Vẽ đường chéo từ góc dưới trái sang góc trên phải
            e.Graphics.DrawLine(pen, 0, ptbRa4.Height, ptbRa4.Width, 0);
        }

        private void ptbRa2_Paint(object sender, PaintEventArgs e)
        {
            if ((string)ptbRa2.Tag == "0") return;
            // Thiết lập màu cho đường chéo
            using Pen pen = new Pen(Color.Red, 5);
            // Vẽ đường chéo từ góc trên trái sang góc dưới phải
            e.Graphics.DrawLine(pen, 0, 0, ptbRa2.Width, ptbRa2.Height);
            // Vẽ đường chéo từ góc dưới trái sang góc trên phải
            e.Graphics.DrawLine(pen, 0, ptbRa2.Height, ptbRa2.Width, 0);
        }
        #endregion
    }
}
