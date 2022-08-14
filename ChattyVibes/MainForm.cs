﻿using Buttplug;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Forms;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;

namespace ChattyVibes
{
    public partial class MainForm : Form
    {
        private const string CTwitchRedirectUrl = "http://localhost:3000";
        private const string CTwitchClientId = "77nu3r5gyqhuzsambceccrbd9ctjdo";
        private const string CTwitchAuthStateVerify = "chattyvibes_123test";
        private string _authToken = null;
        private readonly HttpListener _server = new HttpListener();

        private readonly Config _conf = new Config();
        private ConnectionState _plugState = ConnectionState.NotConnected;
        private ButtplugClient _plugClient = null;
        private ConnectionState _chatState = ConnectionState.NotConnected;
        private TwitchClient _chatClient = null;

        private readonly Queue<QueuedItemType> _queue = new Queue<QueuedItemType>();
        private Thread _worker;
        private Thread _batteryWorker;

        public MainForm()
        {
            InitializeComponent();
            Config.Load(ref _conf);
            _server.Prefixes.Add($"{CTwitchRedirectUrl}/");
        }

        private void UpdateGUI()
        {
            switch (_plugState)
            {
                case ConnectionState.NotConnected:
                    {
                        btnConnectBP.Enabled = true;
                        btnDisconnectBP.Enabled = false;
                        lblBPStatus.Text = "Not Connected";
                        tbHostname.Enabled = true;
                        numPort.Enabled = true;
                        break;
                    }
                case ConnectionState.Connecting:
                    {
                        btnConnectBP.Enabled = false;
                        btnDisconnectBP.Enabled = false;
                        lblBPStatus.Text = "Connecting";
                        tbHostname.Enabled = false;
                        numPort.Enabled = false;
                        break;
                    }
                case ConnectionState.Connected:
                    {
                        btnConnectBP.Enabled = false;
                        btnDisconnectBP.Enabled = true;
                        lblBPStatus.Text = "Connected";
                        tbHostname.Enabled = false;
                        numPort.Enabled = false;
                        break;
                    }
                case ConnectionState.Disconnecting:
                    {
                        btnConnectBP.Enabled = false;
                        btnDisconnectBP.Enabled = false;
                        lblBPStatus.Text = "Disconnecting";
                        tbHostname.Enabled = false;
                        numPort.Enabled = false;
                        break;
                    }
                case ConnectionState.Error:
                    {
                        btnConnectBP.Enabled = true;
                        btnDisconnectBP.Enabled = false;
                        lblBPStatus.Text = "Connection Error";
                        tbHostname.Enabled = true;
                        numPort.Enabled = true;
                        break;
                    }
            }

            switch (_chatState)
            {
                case ConnectionState.NotConnected:
                    {
                        btnConnectTwitch.Enabled = true;
                        lblChatStatus.Text = "Not Connected";
                        tbUsername.Enabled = true;
                        break;
                    }
                case ConnectionState.Connecting:
                    {
                        btnConnectTwitch.Enabled = false;
                        lblChatStatus.Text = "Connecting";
                        tbUsername.Enabled = false;
                        break;
                    }
                case ConnectionState.Connected:
                    {
                        btnConnectTwitch.Enabled = false;
                        lblChatStatus.Text = "Connected";
                        tbUsername.Enabled = false;
                        break;
                    }
                case ConnectionState.Disconnecting:
                    {
                        btnConnectTwitch.Enabled = false;
                        lblChatStatus.Text = "Disconnecting";
                        tbUsername.Enabled = false;
                        break;
                    }
                case ConnectionState.Error:
                    {
                        btnConnectTwitch.Enabled = true;
                        lblChatStatus.Text = "Connection Error";
                        tbUsername.Enabled = true;
                        break;
                    }
            }
        }

        private void HandleQueue()
        {
            int sleepTime;

            while (true)
            {
                try
                {
                    sleepTime = Math.Max(
                        _conf.BindReSubDuration, Math.Max(
                            _conf.BindPrimeSubDuration, Math.Max(
                                _conf.BindComSubDuration, Math.Max(
                                    _conf.BindContGiftSubDuration, Math.Max(
                                        _conf.BindGiftSubDuration, Math.Max(
                                            _conf.BindNewSubDuration, Math.Max(
                                                _conf.BindWhisperDuration, _conf.BindMsgDuration
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    );

                    if (_queue.Count > 0)
                    {
                        LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Dequeueing message of type \"{_queue.Peek()}\"").Wait();
                        HandleToys(_queue.Dequeue());
                        Thread.Sleep(sleepTime);
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
                catch (ThreadAbortException)
                {
                    return;
                }
            }
        }

        private void HandleBatteries()
        {
            while (true)
            {
                try
                {
                    if (_plugClient == null || (!_plugClient.Connected) || _plugClient.Devices.Length <= 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    var results = new List<DeviceBattery>();

                    foreach (var toy in _plugClient.Devices)
                    {
                        try
                        {
                            results.Add(SendBatteryLevelCommand(toy).GetAwaiter().GetResult());
                        }
                        catch (ButtplugDeviceException)
                        {
                            LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Tried to talk to a disconnected device.").Wait();
                        }
                    }

                    lvDevices.Invoke((MethodInvoker)delegate {
                        lvDevices.BeginUpdate();
                        lvDevices.Items.Clear();

                        foreach (var item in results)
                        {
                            var newEntry = new ListViewItem { Text = item.Name };
                            newEntry.SubItems.Add(item.Level);
                            lvDevices.Items.Add(newEntry);
                        }

                        lvDevices.EndUpdate();
                    });

                    for (int i = 0; i < 30; i++)
                        Thread.Sleep(1000); // Every 30 seconds is plenty fast enough
                }
                catch (ThreadAbortException)
                {
                    return;
                }
            }
        }

        private async void HandleToys(QueuedItemType aType)
        {
            bool vibeEna = false;
            float vibeLvl = 0;
            bool rotEna = false;
            float rotLvl = 0;
            bool rotClock = false;
            bool strEna = false;
            float strMin = 0;
            float strMax = 0;
            int duration = 0;

            switch (aType)
            {
                case QueuedItemType.Message:
                    {
                        vibeEna = _conf.BindMsgVibeEna;
                        vibeLvl = _conf.BindMsgVibeLvl;
                        rotEna = _conf.BindMsgRotEna;
                        rotLvl = _conf.BindMsgRotLvl;
                        rotClock = _conf.BindMsgRotClock;
                        strEna = _conf.BindMsgStrEna;
                        strMin = _conf.BindMsgStrMin;
                        strMax = _conf.BindMsgStrMax;
                        duration = _conf.BindMsgDuration;
                        break;
                    }
                case QueuedItemType.Whisper:
                    {
                        vibeEna = _conf.BindWhisperVibeEna;
                        vibeLvl = _conf.BindWhisperVibeLvl;
                        rotEna = _conf.BindWhisperRotEna;
                        rotLvl = _conf.BindWhisperRotLvl;
                        rotClock = _conf.BindWhisperRotClock;
                        strEna = _conf.BindWhisperStrEna;
                        strMin = _conf.BindWhisperStrMin;
                        strMax = _conf.BindWhisperStrMax;
                        duration = _conf.BindWhisperDuration;
                        break;
                    }
                case QueuedItemType.NewSub:
                    {
                        vibeEna = _conf.BindNewSubVibeEna;
                        vibeLvl = _conf.BindNewSubVibeLvl;
                        rotEna = _conf.BindNewSubRotEna;
                        rotLvl = _conf.BindNewSubRotLvl;
                        rotClock = _conf.BindNewSubRotClock;
                        strEna = _conf.BindNewSubStrEna;
                        strMin = _conf.BindNewSubStrMin;
                        strMax = _conf.BindNewSubStrMax;
                        duration = _conf.BindNewSubDuration;
                        break;
                    }
                case QueuedItemType.GiftSub:
                    {
                        vibeEna = _conf.BindGiftSubVibeEna;
                        vibeLvl = _conf.BindGiftSubVibeLvl;
                        rotEna = _conf.BindGiftSubRotEna;
                        rotLvl = _conf.BindGiftSubRotLvl;
                        rotClock = _conf.BindGiftSubRotClock;
                        strEna = _conf.BindGiftSubStrEna;
                        strMin = _conf.BindGiftSubStrMin;
                        strMax = _conf.BindGiftSubStrMax;
                        duration = _conf.BindGiftSubDuration;
                        break;
                    }
                case QueuedItemType.ContGiftSub:
                    {
                        vibeEna = _conf.BindContGiftSubVibeEna;
                        vibeLvl = _conf.BindContGiftSubVibeLvl;
                        rotEna = _conf.BindContGiftSubRotEna;
                        rotLvl = _conf.BindContGiftSubRotLvl;
                        rotClock = _conf.BindContGiftSubRotClock;
                        strEna = _conf.BindContGiftSubStrEna;
                        strMin = _conf.BindContGiftSubStrMin;
                        strMax = _conf.BindContGiftSubStrMax;
                        duration = _conf.BindContGiftSubDuration;
                        break;
                    }
                case QueuedItemType.ComSub:
                    {
                        vibeEna = _conf.BindComSubVibeEna;
                        vibeLvl = _conf.BindComSubVibeLvl;
                        rotEna = _conf.BindComSubRotEna;
                        rotLvl = _conf.BindComSubRotLvl;
                        rotClock = _conf.BindComSubRotClock;
                        strEna = _conf.BindComSubStrEna;
                        strMin = _conf.BindComSubStrMin;
                        strMax = _conf.BindComSubStrMax;
                        duration = _conf.BindComSubDuration;
                        break;
                    }
                case QueuedItemType.PrimeSub:
                    {
                        vibeEna = _conf.BindPrimeSubVibeEna;
                        vibeLvl = _conf.BindPrimeSubVibeLvl;
                        rotEna = _conf.BindPrimeSubRotEna;
                        rotLvl = _conf.BindPrimeSubRotLvl;
                        rotClock = _conf.BindPrimeSubRotClock;
                        strEna = _conf.BindPrimeSubStrEna;
                        strMin = _conf.BindPrimeSubStrMin;
                        strMax = _conf.BindPrimeSubStrMax;
                        duration = _conf.BindPrimeSubDuration;
                        break;
                    }
                case QueuedItemType.ReSub:
                    {
                        vibeEna = _conf.BindReSubVibeEna;
                        vibeLvl = _conf.BindReSubVibeLvl;
                        rotEna = _conf.BindReSubRotEna;
                        rotLvl = _conf.BindReSubRotLvl;
                        rotClock = _conf.BindReSubRotClock;
                        strEna = _conf.BindReSubStrEna;
                        strMin = _conf.BindReSubStrMin;
                        strMax = _conf.BindReSubStrMax;
                        duration = _conf.BindReSubDuration;
                        break;
                    }
            }

            foreach (var toy in _plugClient.Devices)
            {
                try
                {
                    if (vibeEna && toy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.VibrateCmd))
                        SendVibeCommand(toy, vibeLvl, duration);

                    if (rotEna && toy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.RotateCmd))
                        SendRotateCommand(toy, rotLvl, rotClock, duration);

                    if (strEna && toy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.LinearCmd))
                        SendStrokeCommand(toy, strMin, strMax, duration);
                }
                catch (ButtplugDeviceException)
                {
                    await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Device \"{toy.Name}\" (idx {toy.Index}) disconnected. Please try another device.");
                }
            }
        }

        private async void SendVibeCommand(ButtplugClientDevice aToy, float aLevel, int aDuration)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Sending vibrations");
            await aToy.SendVibrateCmd(aLevel);
            await Task.Delay(aDuration);
            await aToy.SendVibrateCmd(0);
        }

        private async void SendRotateCommand(ButtplugClientDevice aToy, float aLevel, bool aClockwise, int aDuration)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Sending rotations");
            await aToy.SendRotateCmd(aLevel, aClockwise);
            await Task.Delay(aDuration);
            await aToy.SendRotateCmd(0, aClockwise);
        }

        private async void SendStrokeCommand(ButtplugClientDevice aToy, float aMin, float aMax, int aDuration)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Sending strokes");
            int durationSplit = aDuration / 2;
            int delay = durationSplit + 100;
            await aToy.SendLinearCmd((uint)durationSplit, aMin);
            await Task.Delay(delay);
            await aToy.SendLinearCmd((uint)durationSplit, aMax);
            await Task.Delay(delay);
        }

        private async Task<DeviceBattery> SendBatteryLevelCommand(ButtplugClientDevice aToy)
        {
            var result = new DeviceBattery { Name = $"{aToy.Index} - {aToy.Name}" };

            if (aToy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.BatteryLevelCmd))
            {
                double level = await aToy.SendBatteryLevelCmd();
                result.Level = $"{level:P0}";
            }
            else if (aToy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.RssilevelCmd))
            {
                int level = await aToy.SendRSSIBatteryLevelCmd();
                result.Level = $"{100 + level}";
            }
            else
            {
                result.Level = "N/A";
            }

            return result;
        }

        /*
         * Form actions
         */

        private void MainForm_Load(object sender, EventArgs e)
        {
            tabControl1.SelectedIndex = 0;
            UpdateGUI();

            lvDevices.BeginUpdate();
            lvDevices.Items.Clear();
            lvDevices.EndUpdate();

            ButtplugFFILog.LogMessage += ButtplugFFILog_LogMessage;
            ButtplugFFILog.SetLogOptions(ButtplugLogLevel.Info, false);
            _plugClient = new ButtplugClient("ChattyVibes v" + Assembly.GetExecutingAssembly().GetName().Version.ToString());
            _plugClient.ErrorReceived += _plugClient_ErrorReceived;
            _plugClient.ServerDisconnect += _plugClient_ServerDisconnect;
            _plugClient.ScanningFinished += _plugClient_ScanningFinished;
            _plugClient.DeviceAdded += _plugClient_DeviceAdded;
            _plugClient.DeviceRemoved += _plugClient_DeviceRemoved;

            // Buttplug.IO / Intiface config
            tbHostname.Text = _conf.ButtplugHostname;
            numPort.Value = _conf.ButtplugPort;

            // Twitch config
            tbUsername.Text = _conf.TwitchUsername;

            cbChatVibe.Checked = _conf.BindMsgVibeEna;
            numChatVibe.Value = (decimal)_conf.BindMsgVibeLvl;
            cbChatRot.Checked = _conf.BindMsgRotEna;
            numChatRot.Value = (decimal)_conf.BindMsgRotLvl;
            cbChatRotClock.Checked = _conf.BindMsgRotClock;
            cbChatStroke.Checked = _conf.BindMsgStrEna;
            numChatStrokeMin.Value = (decimal)_conf.BindMsgStrMin;
            numChatStrokeMax.Value = (decimal)_conf.BindMsgStrMax;
            numChatDuration.Value = _conf.BindMsgDuration;

            cbWhisperVibe.Checked = _conf.BindWhisperVibeEna;
            numWhisperVibe.Value = (decimal)_conf.BindWhisperVibeLvl;
            cbWhisperRot.Checked = _conf.BindWhisperRotEna;
            numWhisperRot.Value = (decimal)_conf.BindWhisperRotLvl;
            cbWhisperRotClock.Checked = _conf.BindWhisperRotClock;
            cbWhisperStroke.Checked = _conf.BindWhisperStrEna;
            numWhisperStrokeMin.Value = (decimal)_conf.BindWhisperStrMin;
            numWhisperStrokeMax.Value = (decimal)_conf.BindWhisperStrMax;
            numWhisperDuration.Value = _conf.BindWhisperDuration;

            cbSubVibe.Checked = _conf.BindNewSubVibeEna;
            numSubVibe.Value = (decimal)_conf.BindNewSubVibeLvl;
            cbSubRot.Checked = _conf.BindNewSubRotEna;
            numSubRot.Value = (decimal)_conf.BindNewSubRotLvl;
            cbSubRotClock.Checked = _conf.BindNewSubRotClock;
            cbSubStroke.Checked = _conf.BindNewSubStrEna;
            numSubStrokeMin.Value = (decimal)_conf.BindNewSubStrMin;
            numSubStrokeMax.Value = (decimal)_conf.BindNewSubStrMax;
            numSubDuration.Value = _conf.BindNewSubDuration;

            cbGiftVibe.Checked = _conf.BindGiftSubVibeEna;
            numGiftVibe.Value = (decimal)_conf.BindGiftSubVibeLvl;
            cbGiftRot.Checked = _conf.BindGiftSubRotEna;
            numGiftRot.Value = (decimal)_conf.BindGiftSubRotLvl;
            cbGiftRotClock.Checked = _conf.BindGiftSubRotClock;
            cbGiftStroke.Checked = _conf.BindGiftSubStrEna;
            numGiftStrokeMin.Value = (decimal)_conf.BindGiftSubStrMin;
            numGiftStrokeMax.Value = (decimal)_conf.BindGiftSubStrMax;
            numGiftDuration.Value = _conf.BindGiftSubDuration;

            cbContGiftVibe.Checked = _conf.BindContGiftSubVibeEna;
            numContGiftVibe.Value = (decimal)_conf.BindContGiftSubVibeLvl;
            cbContGiftRot.Checked = _conf.BindContGiftSubRotEna;
            numContGiftRot.Value = (decimal)_conf.BindContGiftSubRotLvl;
            cbContGiftRotClock.Checked = _conf.BindContGiftSubRotClock;
            cbContGiftStroke.Checked = _conf.BindContGiftSubStrEna;
            numContGiftStrokeMin.Value = (decimal)_conf.BindContGiftSubStrMin;
            numContGiftStrokeMax.Value = (decimal)_conf.BindContGiftSubStrMax;
            numContGiftDuration.Value = _conf.BindContGiftSubDuration;

            cbComSubVibe.Checked = _conf.BindComSubVibeEna;
            numComSubVibe.Value = (decimal)_conf.BindComSubVibeLvl;
            cbComSubRot.Checked = _conf.BindComSubRotEna;
            numComSubRot.Value = (decimal)_conf.BindComSubRotLvl;
            cbComSubRotClock.Checked = _conf.BindComSubRotClock;
            cbComSubStroke.Checked = _conf.BindComSubStrEna;
            numComSubStrokeMin.Value = (decimal)_conf.BindComSubStrMin;
            numComSubStrokeMax.Value = (decimal)_conf.BindComSubStrMax;
            numComSubDuration.Value = _conf.BindComSubDuration;

            cbPrimeVibe.Checked = _conf.BindPrimeSubVibeEna;
            numPrimeVibe.Value = (decimal)_conf.BindPrimeSubVibeLvl;
            cbPrimeRot.Checked = _conf.BindPrimeSubRotEna;
            numPrimeRot.Value = (decimal)_conf.BindPrimeSubRotLvl;
            cbPrimeRotClock.Checked = _conf.BindPrimeSubRotClock;
            cbPrimeStroke.Checked = _conf.BindPrimeSubStrEna;
            numPrimeStrokeMin.Value = (decimal)_conf.BindPrimeSubStrMin;
            numPrimeStrokeMax.Value = (decimal)_conf.BindPrimeSubStrMax;
            numPrimeDuration.Value = _conf.BindPrimeSubDuration;

            cbReSubVibe.Checked = _conf.BindReSubVibeEna;
            numReSubVibe.Value = (decimal)_conf.BindReSubVibeLvl;
            cbReSubRot.Checked = _conf.BindReSubRotEna;
            numReSubRot.Value = (decimal)_conf.BindReSubRotLvl;
            cbReSubRotClock.Checked = _conf.BindReSubRotClock;
            cbReSubStroke.Checked = _conf.BindReSubStrEna;
            numReSubStrokeMin.Value = (decimal)_conf.BindReSubStrMin;
            numReSubStrokeMax.Value = (decimal)_conf.BindReSubStrMax;
            numReSubDuration.Value = _conf.BindReSubDuration;

            _worker = new Thread(new ThreadStart(HandleQueue)) { IsBackground = true };
            _worker.Start();

            _batteryWorker = new Thread(new ThreadStart(HandleBatteries)) { IsBackground = true };
            _batteryWorker.Start();
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            _worker.Abort();
            _worker.Join(5000);

            _batteryWorker.Abort();
            _batteryWorker.Join(5000);

            if (_chatClient != null && _chatClient.IsConnected)
            {
                _chatClient.Disconnect();
                _chatClient = null;
            }

            if (_plugClient != null && _plugClient.Connected)
            {
                _plugClient.DisconnectAsync().Wait();
                _plugClient = null;
            }

            Config.Save(_conf);
        }

        private async void btnConnectBP_Click(object sender, EventArgs e)
        {
            _plugState = ConnectionState.Connecting;
            UpdateGUI();
            Config.Save(_conf);

            try
            {
                await _plugClient.ConnectAsync(new ButtplugWebsocketConnectorOptions(new Uri($"ws://{_conf.ButtplugHostname}:{_conf.ButtplugPort}")));
            }
            catch (ButtplugConnectorException ex)
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Can't connect to Buttplug Server, exiting! - Message: {ex.InnerException.Message}");
                _plugState = ConnectionState.Error;
                UpdateGUI();
                return;
            }
            catch (ButtplugHandshakeException ex)
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Handshake with Buttplug Server, exiting! - Message: {ex.InnerException.Message}");
                _plugState = ConnectionState.Error;
                UpdateGUI();
                return;
            }

            try
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Connected, scanning for devices");
                await _plugClient.StartScanningAsync();
            }
            catch (ButtplugException ex)
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Scanning failed - Message: {ex.InnerException.Message}");
                _plugState = ConnectionState.Error;
                await _plugClient.DisconnectAsync();
                return;
            }

            _plugState = ConnectionState.Connected;
            UpdateGUI();

            await Task.Delay(5000);

            if (_plugClient.IsScanning)
                await _plugClient.StopScanningAsync();

            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Scanning done");
        }

        private async void btnDisconnectBP_Click(object sender, EventArgs e)
        {
            _plugState = ConnectionState.Disconnecting;
            UpdateGUI();

            if (_plugClient.Connected)
                await _plugClient.DisconnectAsync();

            _plugState = ConnectionState.NotConnected;
            UpdateGUI();
        }

        private async void btnRescanDevices_Click(object sender, EventArgs e)
        {
            if (_plugClient.IsScanning)
            {
                await _plugClient.StopScanningAsync();
                await Task.Delay(100);
            }

            try
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Rescanning devices");
                await _plugClient.StartScanningAsync();
            }
            catch (ButtplugException ex)
            {
                await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Scanning failed - Message: {ex.InnerException.Message}");
                return;
            }

            await Task.Delay(5000);

            if (_plugClient.IsScanning)
                await _plugClient.StopScanningAsync();
        }

        private async void btnConnectTwitch_Click(object sender, EventArgs e)
        {
            _chatState = ConnectionState.Connecting;
            UpdateGUI();
            Config.Save(_conf);

            var twitchOptions = new ClientOptions
            {
                ThrottlingPeriod = TimeSpan.FromSeconds(30),
                MessagesAllowedInPeriod = 750,
                WhisperThrottlingPeriod = TimeSpan.FromSeconds(30),
                WhispersAllowedInPeriod = 50
            };
            _chatClient = new TwitchClient(new WebSocketClient(twitchOptions));
            _chatClient.OnLog += _chatClient_OnLog;
            _chatClient.OnConnected += _chatClient_OnConnected;
            _chatClient.OnJoinedChannel += _chatClient_OnJoinedChannel;

            // MVP goal, vibe on received msg
            _chatClient.OnMessageReceived += _chatClient_OnMessageReceived;
            _chatClient.OnWhisperReceived += _chatClient_OnWhisperReceived;

            // Additional goals
            _chatClient.OnNewSubscriber += _chatClient_OnNewSubscriber;
            _chatClient.OnGiftedSubscription += _chatClient_OnGiftedSubscription;
            _chatClient.OnContinuedGiftedSubscription += _chatClient_OnContinuedGiftedSubscription;
            _chatClient.OnCommunitySubscription += _chatClient_OnCommunitySubscription;
            _chatClient.OnPrimePaidSubscriber += _chatClient_OnPrimePaidSubscriber;
            _chatClient.OnReSubscriber += _chatClient_OnReSubscriber;

            _authToken = null;
            _server.Start();

            Process.Start(
                "https://id.twitch.tv/oauth2/authorize" +
                "?response_type=token" +
                $"&client_id={CTwitchClientId}" +
                $"&redirect_uri={CTwitchRedirectUrl}" +
                $"&scope={HttpUtility.UrlEncode("chat:read chat:edit channel:read:subscriptions whispers:read channel:read:redemptions channel:read:hype_train channel:read:goals")}" +
                $"&state={CTwitchAuthStateVerify}" +
                "&force_verify=true"
            );

            _server.BeginGetContext(new AsyncCallback(IncomingHttpRequest), _server);

            while (_server.IsListening)
                await Task.Delay(10);

            // _server.Stop();
            var twitchCreds = new ConnectionCredentials(_conf.TwitchUsername, _authToken);
            _chatClient.Initialize(
                credentials: twitchCreds,
                channel: _conf.TwitchUsername,
                autoReListenOnExceptions: true
            );

            if (!_chatClient.Connect())
            {
                _chatState = ConnectionState.Error;
                UpdateGUI();
                return;
            }

            _chatState = ConnectionState.Connected;
            UpdateGUI();
        }

        private void IncomingHttpRequest(IAsyncResult result)
        {
            var httpListener = (HttpListener)result.AsyncState;
            var httpContext = httpListener.EndGetContext(result);
            // If we'd like the HTTP listener to accept more incoming requests, we'd just restart the "get context" here:
            httpListener.BeginGetContext(new AsyncCallback(IncomingAuth), httpListener);
            var httpRequest = httpContext.Request;

            // Build a response to send JS back to the browser for OAUTH Relay
            var httpResponse = httpContext.Response;
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes($@"<html>
  <body>
    <b id=""auth"">Login Complete</b><br>
    <script type=""text/javascript"">
      window.onload = () => {{
        var xhr = new XMLHttpRequest();
        xhr.open('POST', '{CTwitchRedirectUrl}');
        xhr.send(window.location); // Sending the window location (the url bar) from the browser to our listener
      }};
    </script>
  </body>
</html>");

            httpResponse.ContentLength64 = buffer.Length;
            httpResponse.OutputStream.Write(buffer, 0, buffer.Length);
            httpResponse.OutputStream.Close();
        }

        private void IncomingAuth(IAsyncResult ar)
        {
            var httpListener = (HttpListener)ar.AsyncState;
            var httpContext = httpListener.EndGetContext(ar);
            var httpRequest = httpContext.Request;

            // Filter out any default requests we don't want
            if (httpRequest.Url.AbsolutePath.StartsWith("/favicon.ico"))
            {
                httpListener.BeginGetContext(new AsyncCallback(IncomingAuth), httpListener);
                return;
            }

            // This time we take an input stream from the request to recieve the url
            string url;

            using (var reader = new StreamReader(httpRequest.InputStream, httpRequest.ContentEncoding))
                url = reader.ReadToEnd();

            Regex rx = new Regex(@".+#access_token=(.+)&scope.*&state=(.+)&token_type=bearer");
            var match = rx.Match(url);

            // If state doesnt match reject data
            if (match.Groups[2].Value != CTwitchAuthStateVerify)
            {
                httpListener.BeginGetContext(new AsyncCallback(IncomingAuth), httpListener);
                return;
            }

            _authToken = match.Groups[1].Value;
            httpListener.Stop();
        }

        private void CheckedChanged(object sender, EventArgs e)
        {
            var tSender = sender as CheckBox;

            if (tSender == cbChatVibe) { _conf.BindMsgVibeEna = tSender.Checked; return; }
            if (tSender == cbChatRot) { _conf.BindMsgRotEna = tSender.Checked; return; }
            if (tSender == cbChatRotClock) { _conf.BindMsgRotClock = tSender.Checked; return; }
            if (tSender == cbChatStroke) { _conf.BindMsgStrEna = tSender.Checked; return; }

            if (tSender == cbWhisperVibe) { _conf.BindWhisperVibeEna = tSender.Checked; return; }
            if (tSender == cbWhisperRot) { _conf.BindWhisperRotEna = tSender.Checked; return; }
            if (tSender == cbWhisperRotClock) { _conf.BindWhisperRotClock = tSender.Checked; return; }
            if (tSender == cbWhisperStroke) { _conf.BindWhisperStrEna = tSender.Checked; return; }

            if (tSender == cbSubVibe) { _conf.BindNewSubVibeEna = tSender.Checked; return; }
            if (tSender == cbSubRot) { _conf.BindNewSubRotEna = tSender.Checked; return; }
            if (tSender == cbSubRotClock) { _conf.BindNewSubRotClock = tSender.Checked; return; }
            if (tSender == cbSubStroke) { _conf.BindNewSubStrEna = tSender.Checked; return; }

            if (tSender == cbGiftVibe) { _conf.BindGiftSubVibeEna = tSender.Checked; return; }
            if (tSender == cbGiftRot) { _conf.BindGiftSubRotEna = tSender.Checked; return; }
            if (tSender == cbGiftRotClock) { _conf.BindGiftSubRotClock = tSender.Checked; return; }
            if (tSender == cbGiftStroke) { _conf.BindGiftSubStrEna = tSender.Checked; return; }

            if (tSender == cbContGiftVibe) { _conf.BindContGiftSubVibeEna = tSender.Checked; return; }
            if (tSender == cbContGiftRot) { _conf.BindContGiftSubRotEna = tSender.Checked; return; }
            if (tSender == cbContGiftRotClock) { _conf.BindContGiftSubRotClock = tSender.Checked; return; }
            if (tSender == cbContGiftStroke) { _conf.BindContGiftSubStrEna = tSender.Checked; return; }

            if (tSender == cbComSubVibe) { _conf.BindMsgVibeEna = tSender.Checked; return; }
            if (tSender == cbComSubRot) { _conf.BindMsgRotEna = tSender.Checked; return; }
            if (tSender == cbComSubRotClock) { _conf.BindMsgRotClock = tSender.Checked; return; }
            if (tSender == cbComSubStroke) { _conf.BindMsgStrEna = tSender.Checked; return; }

            if (tSender == cbPrimeVibe) { _conf.BindPrimeSubVibeEna = tSender.Checked; return; }
            if (tSender == cbPrimeRot) { _conf.BindPrimeSubRotEna = tSender.Checked; return; }
            if (tSender == cbPrimeRotClock) { _conf.BindPrimeSubRotClock = tSender.Checked; return; }
            if (tSender == cbPrimeStroke) { _conf.BindPrimeSubStrEna = tSender.Checked; return; }

            if (tSender == cbReSubVibe) { _conf.BindReSubVibeEna = tSender.Checked; return; }
            if (tSender == cbReSubRot) { _conf.BindReSubRotEna = tSender.Checked; return; }
            if (tSender == cbReSubRotClock) { _conf.BindReSubRotClock = tSender.Checked; return; }
            if (tSender == cbReSubStroke) { _conf.BindReSubStrEna = tSender.Checked; return; }
        }

        private void NumValueChanged(object sender, EventArgs e)
        {
            var tSender = sender as NumericUpDown;

            if (tSender == numPort) { _conf.ButtplugPort = (uint)tSender.Value; return; }

            if (tSender == numChatVibe) { _conf.BindMsgVibeLvl = (float)tSender.Value; return; }
            if (tSender == numChatRot) { _conf.BindMsgRotLvl = (float)tSender.Value; return; }
            if (tSender == numChatStrokeMin) { _conf.BindMsgStrMin = (float)tSender.Value; return; }
            if (tSender == numChatStrokeMax) { _conf.BindMsgStrMax = (float)tSender.Value; return; }
            if (tSender == numChatDuration) { _conf.BindMsgDuration = (int)tSender.Value; return; }

            if (tSender == numWhisperVibe) { _conf.BindWhisperVibeLvl = (float)tSender.Value; return; }
            if (tSender == numWhisperRot) { _conf.BindWhisperRotLvl = (float)tSender.Value; return; }
            if (tSender == numWhisperStrokeMin) { _conf.BindWhisperStrMin = (float)tSender.Value; return; }
            if (tSender == numWhisperStrokeMax) { _conf.BindWhisperStrMax = (float)tSender.Value; return; }
            if (tSender == numWhisperDuration) { _conf.BindWhisperDuration = (int)tSender.Value; return; }

            if (tSender == numSubVibe) { _conf.BindNewSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numSubRot) { _conf.BindNewSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numSubStrokeMin) { _conf.BindNewSubStrMin = (float)tSender.Value; return; }
            if (tSender == numSubStrokeMax) { _conf.BindNewSubStrMax = (float)tSender.Value; return; }
            if (tSender == numSubDuration) { _conf.BindNewSubDuration = (int)tSender.Value; return; }

            if (tSender == numGiftVibe) { _conf.BindGiftSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numGiftRot) { _conf.BindGiftSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numGiftStrokeMin) { _conf.BindGiftSubStrMin = (float)tSender.Value; return; }
            if (tSender == numGiftStrokeMax) { _conf.BindGiftSubStrMax = (float)tSender.Value; return; }
            if (tSender == numGiftDuration) { _conf.BindGiftSubDuration = (int)tSender.Value; return; }

            if (tSender == numContGiftVibe) { _conf.BindContGiftSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numContGiftRot) { _conf.BindContGiftSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numContGiftStrokeMin) { _conf.BindContGiftSubStrMin = (float)tSender.Value; return; }
            if (tSender == numContGiftStrokeMax) { _conf.BindContGiftSubStrMax = (float)tSender.Value; return; }
            if (tSender == numContGiftDuration) { _conf.BindContGiftSubDuration = (int)tSender.Value; return; }

            if (tSender == numComSubVibe) { _conf.BindComSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numComSubRot) { _conf.BindComSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numComSubStrokeMin) { _conf.BindComSubStrMin = (float)tSender.Value; return; }
            if (tSender == numComSubStrokeMax) { _conf.BindComSubStrMax = (float)tSender.Value; return; }
            if (tSender == numComSubDuration) { _conf.BindComSubDuration = (int)tSender.Value; return; }

            if (tSender == numPrimeVibe) { _conf.BindPrimeSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numPrimeRot) { _conf.BindPrimeSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numPrimeStrokeMin) { _conf.BindPrimeSubStrMin = (float)tSender.Value; return; }
            if (tSender == numPrimeStrokeMax) { _conf.BindPrimeSubStrMax = (float)tSender.Value; return; }
            if (tSender == numPrimeDuration) { _conf.BindPrimeSubDuration = (int)tSender.Value; return; }

            if (tSender == numReSubVibe) { _conf.BindReSubVibeLvl = (float)tSender.Value; return; }
            if (tSender == numReSubRot) { _conf.BindReSubRotLvl = (float)tSender.Value; return; }
            if (tSender == numReSubStrokeMin) { _conf.BindReSubStrMin = (float)tSender.Value; return; }
            if (tSender == numReSubStrokeMax) { _conf.BindReSubStrMax = (float)tSender.Value; return; }
            if (tSender == numReSubDuration) { _conf.BindReSubDuration = (int)tSender.Value; return; }
        }

        private void TbTextChanged(object sender, EventArgs e)
        {
            var tSender = sender as TextBox;

            if (tSender == tbHostname) { _conf.ButtplugHostname = tSender.Text; return; }

            if (tSender == tbUsername) { _conf.TwitchUsername = tSender.Text; return; }
        }

        private async Task LogMsg(string aMsg)
        {
            tbLog.Invoke((MethodInvoker)delegate {
                tbLog.AppendText(aMsg);
            });
            await Task.Delay(1);
        }

        /*
         * Twitch actions
         */

        private async void _chatClient_OnLog(object sender, OnLogArgs e) =>
            await LogMsg($"\r\n{e.DateTime} - Twitch: {e.BotUsername} - {e.Data}");

        private async void _chatClient_OnConnected(object sender, OnConnectedArgs e) =>
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.BotUsername} - Connected to {e.AutoJoinChannel}");

        private async void _chatClient_OnJoinedChannel(object sender, OnJoinedChannelArgs e)
        {
            _chatClient.SendMessage(e.Channel, "ChattyVibes joined the channel, ready for work");
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.BotUsername} - Sent message \"ChattyVibes joined the channel, ready for work\"");

            if (!_plugClient.Connected)
                return;

            foreach (var toy in _plugClient.Devices)
            {
                if (toy.AllowedMessages.ContainsKey(ServerMessage.Types.MessageAttributeType.VibrateCmd))
                {
                    try
                    {
                        await toy.SendVibrateCmd(.25);
                        await Task.Delay(1000);
                        await toy.SendVibrateCmd(.0);
                    }
                    catch (ButtplugDeviceException)
                    {
                        await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Device \"{toy.Name}\" (idx {toy.Index}) disconnected. Please try another device.");
                    }
                }
            }
        }

        // MVP goal, vibe on received msg

        private async void _chatClient_OnMessageReceived(object sender, OnMessageReceivedArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.ChatMessage.DisplayName} - Sent Message \"{e.ChatMessage.Message}\"");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.Message);
        }

        private async void _chatClient_OnWhisperReceived(object sender, OnWhisperReceivedArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.WhisperMessage.DisplayName} - Sent Whisper \"{e.WhisperMessage.Message}\"");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.Whisper);
        }

        // Additional goals

        private async void _chatClient_OnNewSubscriber(object sender, OnNewSubscriberArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.Subscriber.DisplayName} - New Sub tier {e.Subscriber.SubscriptionPlanName}");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        private async void _chatClient_OnGiftedSubscription(object sender, OnGiftedSubscriptionArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.GiftedSubscription.DisplayName} - New Gift Sub \"{e.GiftedSubscription.MsgParamRecipientDisplayName}\" Tier {e.GiftedSubscription.MsgParamSubPlanName} Length {e.GiftedSubscription.MsgParamMonths}");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        private async void _chatClient_OnReSubscriber(object sender, OnReSubscriberArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.ReSubscriber.DisplayName} - Re-Sub tier {e.ReSubscriber.SubscriptionPlanName} Message \"{e.ReSubscriber.ResubMessage}\"");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        private async void _chatClient_OnPrimePaidSubscriber(object sender, OnPrimePaidSubscriberArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.PrimePaidSubscriber.DisplayName} - Prime Subscriber");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        private async void _chatClient_OnCommunitySubscription(object sender, OnCommunitySubscriptionArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.GiftedSubscription.DisplayName} - Community Subscriber tier {e.GiftedSubscription.MsgParamSubPlan}");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        private async void _chatClient_OnContinuedGiftedSubscription(object sender, OnContinuedGiftedSubscriptionArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Twitch: {e.ContinuedGiftedSubscription.DisplayName} - Continued Gifted Subscriber");

            if (!_plugClient.Connected)
                return;

            _queue.Enqueue(QueuedItemType.NewSub);
        }

        /*
         * Buttplug.io actions
         */

        private async void ButtplugFFILog_LogMessage(object sender, string e) =>
            await LogMsg($"\r\n{e}");

        private async void _plugClient_ErrorReceived(object sender, ButtplugExceptionEventArgs e) =>
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Error received from the server.  Message: {e.Exception.Message}");

        private async void _plugClient_ServerDisconnect(object sender, EventArgs e)
        {
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Disconnected from the server");
            _plugState = ConnectionState.NotConnected;

            Invoke((MethodInvoker)delegate {
                UpdateGUI();
            });
        }

        private async void _plugClient_ScanningFinished(object sender, EventArgs e) =>
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Finished scanning for devices");

        private async void _plugClient_DeviceAdded(object sender, DeviceAddedEventArgs e)
        {
            string logMsg = $"\r\n{DateTime.UtcNow:o} - Buttplug: New device \"{e.Device.Name}\" (idx {e.Device.Index}) supports these messages:";

            foreach (var msgInfo in e.Device.AllowedMessages)
            {
                logMsg += $"\r\n  - {msgInfo.Key}";

                if (msgInfo.Value.FeatureCount != 0)
                    logMsg += $"\r\n    - Features: {msgInfo.Value.FeatureCount}";
            }
            await LogMsg(logMsg);
        }

        private async void _plugClient_DeviceRemoved(object sender, DeviceRemovedEventArgs e) =>
            await LogMsg($"\r\n{DateTime.UtcNow:o} - Buttplug: Device \"{e.Device.Name}\" (idx {e.Device.Index}) disconnected");
    }
}
