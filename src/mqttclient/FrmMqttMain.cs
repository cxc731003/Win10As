﻿using System;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using System.Threading;
using System.IO;
using System.Drawing.Imaging;
using System.ComponentModel;
using System.Speech.Synthesis;
using System.Runtime.InteropServices;
using System.Linq;
using Newtonsoft.Json;
using System.Reflection;

namespace mqttclient
{
    public partial class FrmMqttMain : Form
    {
        BindingList<MqttTrigger> MqttTriggerList = new BindingList<MqttTrigger>();
        #region monitor onoff
        public class NativeMethods
        {
            [DllImport("user32.dll")]
            internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        }

        private const int WM_SYSCOMMAND = 0x0112;
        private const int SC_MONITORPOWER = 0xF170;
        private const int MonitorTurnOn = -1;
        private const int MonitorShutoff = 2;

        #endregion

        private const string appID = "Win Mqtt Client";
        private const string NotifyIconText = "Mqtt client";
        private const string NotifyIconBalloonTipText = "Mqtt client minimized to systemtray";
        private const int NotifyIconBalloonTipTimer = 200;

        MqttClient _client;
        readonly Audio _audioobj = new Audio();

        private string GMqtttopic { get; set; }
        private string GTriggerFile { get; set; }
        private string GLocalScreetshotFile { get; set; }

        private void MqttPublishImage(string topic, string file)
        {
            if (_client.IsConnected == true)
            {
                _client.Publish(SetSubTopic(topic), File.ReadAllBytes(file));
            }
        }
        private void MqttPublish(string topic, string message, bool retain = false)
        {
            if (_client.IsConnected == true)
            {
                if (retain == true)
                {

                    _client.Publish(topic, Encoding.UTF8.GetBytes(message), 0, retain);
                }
                else
                {
                    _client.Publish(topic, Encoding.UTF8.GetBytes(message));
                }
            }
        }
        private void SetupTimer()
        {
            try
            {
                if (Properties.Settings.Default["mqtttimerinterval"].ToString() == "")
                {
                    Properties.Settings.Default["mqtttimerinterval"] = 6000;
                    //txtMqttTimerInterval.Text = Properties.Settings.Default["mqtttimerinterval"].ToString();
                    Properties.Settings.Default.Save();
                }

                timer1.Interval = Convert.ToInt32(Properties.Settings.Default["mqtttimerinterval"].ToString());
                timer1.Start();
                //timer1_Tick(null, null);

            }
            catch (Exception)
            {

            }
        }
        public FrmMqttMain()
        {
            try
            {
                InitializeComponent();
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                toolStripStatusLabel2.Text = "";

                GTriggerFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "triggers.json");
                GLocalScreetshotFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "primonitor.jpg");

                Properties.Settings.Default.Upgrade();
                MqttConnect();
                SetupTimer();

                LoadTriggerlist();

                notifyIcon1.Visible = false;
                notifyIcon1.Text = NotifyIconText;
                notifyIcon1.BalloonTipText = NotifyIconBalloonTipText;
                notifyIcon1.ShowBalloonTip(NotifyIconBalloonTipTimer);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

        }

        private void MqttConnect()
        {
            try
            {
                if (Properties.Settings.Default["mqttserver"].ToString().Length > 3)
                {

                    try
                    {
                        _client = new MqttClient(Properties.Settings.Default["mqttserver"].ToString(), Convert.ToInt32(Properties.Settings.Default["mqttport"].ToString()), false, null, null, MqttSslProtocols.None, null);

                        if (Properties.Settings.Default["mqttusername"].ToString().Length > 3)
                        {
                            byte code = _client.Connect(Guid.NewGuid().ToString());
                        }
                        else
                        {
                            byte code = _client.Connect(Guid.NewGuid().ToString(), Properties.Settings.Default["mqttusername"].ToString(), Properties.Settings.Default["mqttpassword"].ToString());
                        }
                    }
                    catch (Exception ex)
                    {

                        toolStripStatusLabel1.Text = "not connected,check connection settings error:" + ex.Message;
                    }

                    try
                    {
                        if (_client.IsConnected == true)
                        {
                            GMqtttopic = Properties.Settings.Default["mqtttopic"].ToString() + "/#";
                            _client.MqttMsgPublishReceived += client_MqttMsgPublishReceived;
                            _client.ConnectionClosed += client_MqttConnectionClosed;
                            _client.Subscribe(new string[] { GMqtttopic }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE });
                            toolStripStatusLabel1.Text = "connected";
                        }
                    }
                    catch (Exception ex)
                    {

                        toolStripStatusLabel1.Text = "not connected,check mqtt setup error:" + ex.Message;
                    }




                }
                else
                {
                    toolStripStatusLabel1.Text = "not connected,check settings mqttservername not entered";
                }
            }

            catch (Exception ex)
            {
                toolStripStatusLabel1.Text = "not connected,check settings. Error:" + ex.InnerException.ToString();
            }

        }
        private void client_MqttConnectionClosed(object sender, EventArgs e)
        {
            try
            {
                toolStripStatusLabel1.Text = "not connected";
            }
            catch (Exception)
            {

                throw;
            }

        }
        private void LoadTriggerlist()
        {
            try
            {
                if (File.Exists(GTriggerFile))
                {
                    string s = File.ReadAllText(GTriggerFile);
                    BindingList<MqttTrigger> deserializedProduct = JsonConvert.DeserializeObject<BindingList<MqttTrigger>>(s);
                    MqttTriggerList = deserializedProduct;

                }
            }
            catch (Exception)
            {

                throw;
            }


        }
        void client_MqttMsgPublished(object sender, MqttMsgPublishedEventArgs e)
        {
            try
            {
                WriteToLog("MessageId = " + e.MessageId + " Published = " + e.IsPublished);
            }
            catch (Exception ex)
            {
                WriteToLog("error: " + ex.Message);
                //throw;
            }

        }
        void client_MqttMsgSubscribed(object sender, MqttMsgSubscribedEventArgs e)
        {
            try
            {
                WriteToLog("Subscribed for id = " + e.MessageId);
            }
            catch (Exception ex)
            {
                WriteToLog("error: " + ex.Message);
            }

        }
        void client_MqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
        {
            try
            {
                string message = Encoding.UTF8.GetString(e.Message);
                MqttTrigger CurrentMqttTrigger = new MqttTrigger();
                WriteToLog("Message recived " + e.Topic + " value " + message);

                string TopLevel = Properties.Settings.Default["mqtttopic"].ToString().Replace("/#", "");
                string subtopic = e.Topic.Replace(TopLevel + "/", "");

                foreach (MqttTrigger tmpTrigger in MqttTriggerList)
                {
                    if (tmpTrigger.Name == subtopic)
                    {
                        CurrentMqttTrigger = tmpTrigger;
                        CurrentMqttTrigger.Id = 1;
                        break;
                    }
                }

                if (CurrentMqttTrigger.Id == 1)
                {

                    switch (subtopic)
                    {
                        case "app/running":
                            MqttPublish(SetSubTopic("app/running/" + message), ExeRunning(message, ""));
                            break;
                        case "app/close":
                            MqttPublish(SetSubTopic("app/running/" + message), ExeClose(message));
                            break;

                        case "monitor/set":

                            using (var f = new Form())
                            {
                                if (message == "1")
                                {

                                    NativeMethods.SendMessage(f.Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MonitorTurnOn);
                                    MqttPublish(SetSubTopic("monitor"), "1");
                                }
                                else
                                {
                                    NativeMethods.SendMessage(f.Handle, WM_SYSCOMMAND, (IntPtr)SC_MONITORPOWER, (IntPtr)MonitorShutoff);
                                    MqttPublish(SetSubTopic("monitor"), "0");
                                }
                            }
                            break;

                        case "mute/set":

                            if (message == "1")
                            {
                                _audioobj.Mute(true);
                            }
                            else
                            {
                                _audioobj.Mute(false);
                            }

                            MqttPublish(TopLevel + "/mute", message);
                            break;
                        case "mute":
                            break;
                        case "volume":
                            break;
                        case "volume/set":
                            _audioobj.Volume(Convert.ToInt32(message));
                            break;
                        case "hibernate":
                            Application.SetSuspendState(PowerState.Hibernate, true, true);
                            break;
                        case "suspend":
                            Application.SetSuspendState(PowerState.Suspend, true, true);
                            break;
                        case "reboot":
                            System.Diagnostics.Process.Start("shutdown.exe", "-r -t 10");
                            break;
                        case "shutdown":
                            System.Diagnostics.Process.Start("shutdown.exe", "-s -t 10");
                            break;
                        case "tts":
                            SpeechSynthesizer synthesizer = new SpeechSynthesizer();
                            // synthesizer.GetInstalledVoices

                            synthesizer.Volume = 100;  // 0...100
                            synthesizer.SpeakAsync(message);
                            break;
                        case "toast":

                            int i = 0;

                            string Line1 = "";
                            string Line2 = "";
                            string Line3 = "";
                            string FileURI = "";

                            string[] words = message.Split(',');
                            foreach (string word in words)
                            {

                                switch (i)
                                {
                                    case 0:
                                        Line1 = word;
                                        break;
                                    case 1:
                                        Line2 = word;
                                        break;
                                    case 2:
                                        Line3 = word;
                                        break;
                                    case 3:
                                        FileURI = word;
                                        break;
                                    default:
                                        break;
                                }
                                i = i + 1;
                            }
                            Toastmessage(Line1, Line2, Line3, FileURI, ToastTemplateType.ToastImageAndText04);


                            break;
                        default:

                            if (CurrentMqttTrigger.CmdText.Length > 2)
                            {
                                ProcessStartInfo startInfo = new ProcessStartInfo(CurrentMqttTrigger.CmdText);
                                startInfo.WindowStyle = ProcessWindowStyle.Maximized;
                                if (CurrentMqttTrigger.CmdParameters.Length > 2)
                                {
                                    startInfo.Arguments = CurrentMqttTrigger.CmdParameters;
                                }
                                Process.Start(startInfo);
                            }

                            break;
                    }
                }

            }
            catch (Exception ex)
            {
                WriteToLog("error: " + ex.Message);

            }

        }
        private void Toastmessage(string line1, string line2, string line3, string fileUri, ToastTemplateType ts)
        {
            switch (ts)
            {
                case ToastTemplateType.ToastImageAndText04:

                    XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastImageAndText04);

                    string tempLine = "";

                    XmlNodeList stringElements = toastXml.GetElementsByTagName("text");
                    for (int i = 0; i < stringElements.Length; i++)
                    {

                        switch (i)
                        {
                            case 0:
                                tempLine = line1;
                                break;
                            case 1:
                                tempLine = line2;
                                break;
                            case 2:
                                tempLine = line3;
                                break;
                        }
                        stringElements[i].AppendChild(toastXml.CreateTextNode(tempLine));
                    }

                    String imagePath = "file:///" + fileUri;

                    XmlNodeList toastImageAttributes = toastXml.GetElementsByTagName("image");
                    ((XmlElement)toastImageAttributes[0]).SetAttribute("src", imagePath);
                    ((XmlElement)toastImageAttributes[0]).SetAttribute("alt", "alt text");

                    ToastNotification toast = new ToastNotification(toastXml);
                    toast.Activated += ToastActivated;
                    toast.Dismissed += ToastDismissed;
                    toast.Failed += ToastFailed;
                    ToastNotificationManager.CreateToastNotifier(appID).Show(toast);
                    break;

                default:
                    break;

            }
        }
        private void ToastFailed(ToastNotification sender, ToastFailedEventArgs args)
        {
            throw new NotImplementedException();
        }
        private void ToastDismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            throw new NotImplementedException();
        }
        private void ToastActivated(ToastNotification sender, object args)
        {
            throw new NotImplementedException();
        }
        private void WriteToLog(string logtext)
        {
            try
            {
                this.Invoke((MethodInvoker)(() => listBox1.Items.Insert(0, logtext)));
                if (listBox1.Items.Count > 20)
                {
                    this.Invoke((MethodInvoker)(() => listBox1.Items.RemoveAt(20)));
                }
            }
            catch (Exception)
            {

                // throw;
            }

        }
        delegate void SetTextCallback(string text);
        private string SetSubTopic(string Topic)
        {
            return GMqtttopic.Replace("#", Topic);
        }
        private void timer1_Tick(object sender, EventArgs e)
        {
            try
            {

                if (_client.IsConnected == false)
                {
                    MqttConnect();
                }

                if (_client.IsConnected == true)
                {



                    if (Convert.ToBoolean(Properties.Settings.Default["Cpusensor"].ToString()) == true)
                    {
                        try
                        {
                            MqttPublish(SetSubTopic("cpuprosessortime"), HardwareSensors.GetCpuProsessorTime());
                        }
                        catch (Exception)
                        {
                            //we ignore 
                        }
                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["Freememorysensor"].ToString()) == true)
                    {
                        MqttPublish(SetSubTopic("freememory"), HardwareSensors.GetFreeMemory());

                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["Volumesensor"].ToString()) == true)
                    {

                        MqttPublish(SetSubTopic("volume"), _audioobj.GetVolume(), true);
                    }

                    try
                    {
                        if (_audioobj.IsMuted() == true)
                        {
                            MqttPublish(SetSubTopic("mute"), "1");
                        }
                        else
                        {
                            MqttPublish(SetSubTopic("mute"), "0");
                        }
                    }
                    catch (Exception)
                    {

                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["screenshotenable"]) == true)
                    {
                        TakeScreenshot(Properties.Settings.Default["ScreenShotpath"].ToString());
                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["MqttSlideshow"].ToString()) == true)
                    {
                        if (Properties.Settings.Default["MqttSlideshowFolder"].ToString().Length > 5)
                        {
                            string folder = @Properties.Settings.Default["MqttSlideshowFolder"].ToString();
                            MqttCameraSlide(folder);
                        }
                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["BatterySensor"].ToString()) == true)
                    {
                        MqttPublish(SetSubTopic("/Power/BatteryChargeStatus"), Power.BatteryChargeStatus());
                        MqttPublish(SetSubTopic("/Power/BatteryFullLifetime"), Power.BatteryFullLifetime());
                        MqttPublish(SetSubTopic("/Power/BatteryLifePercent"), Power.BatteryLifePercent());
                        MqttPublish(SetSubTopic("/Power/BatteryLifeRemaining"), Power.BatteryLifeRemaining());
                        MqttPublish(SetSubTopic("/Power/PowerLineStatus"), Power.PowerLineStatus());
                    }

                    if (Convert.ToBoolean(Properties.Settings.Default["DiskSensor"].ToString()) == true)
                    {
                        PublishDiskStatus();
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        private void PublishDiskStatus()
        {
            try
            {
                foreach (var drive in DriveInfo.GetDrives())
                {
                    double freeSpace = drive.TotalFreeSpace;
                    double totalSpace = drive.TotalSize;
                    double percentFree = (freeSpace / totalSpace) * 100;
                    float num = (float)percentFree;

                    string rawdrivename = drive.Name.Replace(":\\", "");

                    MqttPublish(SetSubTopic("drive/" + rawdrivename + "/totalsize"), drive.TotalSize.ToString());
                    MqttPublish(SetSubTopic("drive/" + rawdrivename + "/percentfree"), Convert.ToString(Math.Round(Convert.ToDouble(percentFree.ToString()), 0)));
                    MqttPublish(SetSubTopic("drive/" + rawdrivename + "/availablefreespace"), drive.AvailableFreeSpace.ToString());
                }
            }
            catch (Exception)
            {

                // throw;
            }

        }
        public bool NetworkUp()
        {

            try
            {
                return System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable();
            }
            catch (Exception)
            {

                return false;
            }

        }
        private void TakeScreenshot(string fileUri)
        {
            try
            {
                if (NetworkUp() == true)
                {
                    using (var bmpScreenshot = new Bitmap(Screen.PrimaryScreen.Bounds.Width, Screen.PrimaryScreen.Bounds.Height, PixelFormat.Format32bppArgb))
                    {
                        using (var gfxScreenshot = Graphics.FromImage(bmpScreenshot))
                        {
                            gfxScreenshot.CopyFromScreen(Screen.PrimaryScreen.Bounds.X, Screen.PrimaryScreen.Bounds.Y, 0, 0, Screen.PrimaryScreen.Bounds.Size, CopyPixelOperation.SourceCopy);

                            if (Convert.ToBoolean(Properties.Settings.Default["ScreenshotMqtt"]) == true)
                            {
                                bmpScreenshot.Save(GLocalScreetshotFile, ImageFormat.Png);
                                MqttPublishImage("mqttcamera", GLocalScreetshotFile);
                            }
                            else
                            {
                                bmpScreenshot.Save(fileUri, ImageFormat.Jpeg);
                            }

                        }
                    }
                }
            }
            catch (Exception)
            {
            }


        }
        private void MqttCameraSlide(string folder)
        {
            var rand = new Random();
            var files = Directory.GetFiles(folder, "*.jpg");
            string topic = "slideshow";
            _client.Publish(SetSubTopic(topic), File.ReadAllBytes(files[rand.Next(files.Length)]));
        }
        public void HandleUnhandledException(Exception e)
        {
            if (MessageBox.Show("An unexpected error has occurred. details:" + e.Message + "innerException:" + e.InnerException + "Continue?",
                "MqttClient" + e.Message + " inner:" + e.InnerException, MessageBoxButtons.YesNo, MessageBoxIcon.Stop,
                MessageBoxDefaultButton.Button2) == DialogResult.No)
            {
                Application.Exit();
            }
        }
        public void UnhandledThreadExceptionHandler(object sender, ThreadExceptionEventArgs e)
        {
            this.HandleUnhandledException(e.Exception);
        }
        private void listBox1_KeyUp(object sender, KeyEventArgs e)
        {
            if (sender != listBox1) return;

            if (e.Control && e.KeyCode == Keys.C)
                try
                {
                    Clipboard.SetText(listBox1.SelectedItems[0].ToString());
                }
                catch (Exception)
                {
                }

        }
        private string ExeRunning(string exename, string location)
        {
            try
            {
                bool isRunning = Process.GetProcessesByName(exename)
                            .FirstOrDefault(p => p.MainModule.FileName.StartsWith(location)) != default(Process);
                if (isRunning == true)
                {
                    return "1";
                }
                else
                {
                    return "0";
                }
            }
            catch (Exception)
            {
                return "0";
            }
        }
        private string ExeClose(string exename)
        {
            try
            {
                foreach (Process proc in Process.GetProcessesByName(exename))
                {
                    proc.Kill();
                }
                return "1";
            }
            catch (Exception)
            {
                return "0";
            }
        }
        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frmSettingsFrom = new FrmOptions(this);
            frmSettingsFrom.Show();
        }
        public string FrmOptionExitVal { get; set; }
        public void ReloadApp()
        {
            try
            {
                if (_client != null)
                {
                    if (_client.IsConnected == true)
                    {
                        _client.Disconnect();
                    }
                }

                MqttConnect();
                SetupTimer();
                LoadTriggerlist();
            }
            catch (Exception)
            {

                throw;
            }


        }
        private void FrmMqttMain_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Minimized)
            {
                if (Convert.ToBoolean(Properties.Settings.Default["MinimizeToTray"].ToString()) == true)
                {
                    notifyIcon1.Visible = true;
                    this.ShowInTaskbar = false;
                    this.Hide();
                }
            }
            else
            {
                this.Show();
            }
        }
        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }
        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
        }
        private void FrmMqttMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            //hard exit
            Environment.Exit(0);
        }
        private void toolStripStatusLabel2_Click(object sender, EventArgs e)
        {

        }
    }
}

