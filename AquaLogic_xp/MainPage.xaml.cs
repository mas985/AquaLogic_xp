using AquaLogic;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;
using System;
using System.ComponentModel;
using System.Threading;

namespace AquaLogic_xp
{
    public partial class MainPage : TabbedPage
	{
		public MainPage()
		{
            InitializeComponent();

            LoadSettings();

            InitializeBackgroundWorker();
            
            App_Version.Text = VersionTracking.CurrentVersion.ToString();

           // App_Version.FontSize = nFont;
        }

        string _ipAddr;
        int _portNum;
        int _logInt;
        bool _resetSocket = false;
        private void GetParms()
        {
            _ipAddr = ipAddr.Text;
            _ = int.TryParse(portNum.Text, out int pNum);
            if (pNum > 0) { _portNum = pNum; }
            portNum.Text = _portNum.ToString();
            _ = int.TryParse(LogInt.Text, out _logInt);
            LogInt.Text = _logInt.ToString();
        }
        protected void OnTabSelected(object sender, EventArgs e)
        {
            GetParms();
            SaveSettings();
        }

        private void Restart_Click(object sender, EventArgs args)
        {
            TabPage.CurrentPage = TabPage.Children[0];
            _resetSocket = true;
        }
        string _key = "";
        private void Button_Click(object sender, EventArgs args)
        {
            Button button = (Button)sender;
            _key = button.StyleId;
        }

        public void LoadSettings()
        {
            Aux1_Edit.Text = Preferences.Get(Aux1_Edit.StyleId, "Aux1");
            Aux2_Edit.Text = Preferences.Get(Aux2_Edit.StyleId, "Aux2");
            Aux3_Edit.Text = Preferences.Get(Aux3_Edit.StyleId, "Aux3");
            Aux4_Edit.Text = Preferences.Get(Aux4_Edit.StyleId, "Aux4");
            Aux5_Edit.Text = Preferences.Get(Aux5_Edit.StyleId, "Aux5");
            Aux6_Edit.Text = Preferences.Get(Aux6_Edit.StyleId, "Aux6");
            Valve3_Edit.Text = Preferences.Get(Valve3_Edit.StyleId, "Valve3");
            Valve4_Edit.Text = Preferences.Get(Valve4_Edit.StyleId, "Valve4");
        }
        public void SaveSettings()
        {
            Preferences.Set(Aux1_Edit.StyleId, Aux1_Edit.Text);
            Preferences.Set(Aux2_Edit.StyleId, Aux2_Edit.Text);
            Preferences.Set(Aux3_Edit.StyleId, Aux3_Edit.Text);
            Preferences.Set(Aux4_Edit.StyleId, Aux4_Edit.Text);
            Preferences.Set(Aux5_Edit.StyleId, Aux5_Edit.Text);
            Preferences.Set(Aux6_Edit.StyleId, Aux6_Edit.Text);
            Preferences.Set(Valve3_Edit.StyleId, Valve3_Edit.Text);
            Preferences.Set(Valve4_Edit.StyleId, Valve4_Edit.Text);
        }
        
        // UI Update

        private readonly string _logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "AquaLogic.csv");
        private DateTime _lastLog = DateTime.Now;
        private void UpdateDisplay(SocketProcess.SocketData socketData)
        {
            try
            {
                if (socketData.DisplayText != null)
                {
                    TextDisplay.Text = socketData.DisplayText;
                }

                if (socketData.Status != 0)
                {
                    SetStatus(Pool, socketData.Status, socketData.Blink, SocketProcess.States.POOL);
                    SetStatus(Spa, socketData.Status, socketData.Blink, SocketProcess.States.SPA);
                    SetStatus(Spillover, socketData.Status, socketData.Blink, SocketProcess.States.SPILLOVER);
                    SetStatus(Filter, socketData.Status, socketData.Blink, SocketProcess.States.FILTER);
                    SetStatus(Lights, socketData.Status, socketData.Blink, SocketProcess.States.LIGHTS);
                    SetStatus(Heater1, socketData.Status, socketData.Blink, SocketProcess.States.HEATER_1);
                    SetStatus(Valve3, socketData.Status, socketData.Blink, SocketProcess.States.VALVE_3);
                    SetStatus(Valve4, socketData.Status, socketData.Blink, SocketProcess.States.VALVE_4);
                    SetStatus(Aux1, socketData.Status, socketData.Blink, SocketProcess.States.AUX_1);
                    SetStatus(Aux2, socketData.Status, socketData.Blink, SocketProcess.States.AUX_2);
                    SetStatus(Aux3, socketData.Status, socketData.Blink, SocketProcess.States.AUX_3);
                    SetStatus(Aux4, socketData.Status, socketData.Blink, SocketProcess.States.AUX_4);
                    SetStatus(Aux5, socketData.Status, socketData.Blink, SocketProcess.States.AUX_5);
                    SetStatus(Aux6, socketData.Status, socketData.Blink, SocketProcess.States.AUX_6);
                }

                if (socketData.LogText != null && _logInt > 0 && DateTime.Now >= _lastLog.AddMinutes(_logInt))
                {
                    _lastLog = DateTime.Now;
                    SocketProcess.WriteTextFile(_logPath, socketData.LogText);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

        }
        private static void SetStatus(Button button, SocketProcess.States status, SocketProcess.States blink, SocketProcess.States state)
        {
            button.FontAttributes = (status.HasFlag(state) ? FontAttributes.Bold : FontAttributes.None) |
                    (blink.HasFlag(state) ? FontAttributes.Italic : FontAttributes.None);
        }

        // BackgroundWorker

        readonly BackgroundWorker _backgroundWorker = new();
        private void InitializeBackgroundWorker()
        {
            TextDisplay.Text = "Connecting...";

            GetParms();

            _backgroundWorker.WorkerReportsProgress = true;
            _backgroundWorker.WorkerSupportsCancellation = true;
            _backgroundWorker.DoWork +=
                new DoWorkEventHandler(BackgroundWorker_DoWork);
            _backgroundWorker.RunWorkerCompleted +=
                    new RunWorkerCompletedEventHandler(
                BackgroundWorker_RunWorkerCompleted);
            _backgroundWorker.ProgressChanged +=
                    new ProgressChangedEventHandler(
                BackgroundWorker_ProgressChanged);
            _backgroundWorker.RunWorkerAsync();
        }
        private void BackgroundWorker_DoWork(object sender,
            DoWorkEventArgs e)
        {
            int vCnt = 0;
            bool holdKey = false;
            SocketProcess socketProcess = new(_ipAddr, _portNum);
            Thread.Sleep(250);
            while (true)
            {
                if (_key != "")
                {
                    holdKey = socketProcess.QueueKey(_key);
                    _key = "";
                }
                else
                {
                    SocketProcess.SocketData socketData = socketProcess.Update();

                    if (socketData.HasData)
                    {
                        vCnt = 0;
                        _backgroundWorker.ReportProgress(vCnt, socketData);
                    }
                    else if (vCnt == 100)
                    {
                        _backgroundWorker.ReportProgress(vCnt, socketData);
                    }
                    else if (_resetSocket || !socketProcess.Connected)
                    {
                        _resetSocket = false;
                        if (socketProcess.Connected)
                        {
                            socketProcess.QueueKey("Reset");
                            Thread.Sleep(250);
                        }
                        socketProcess.Reset(_ipAddr, _portNum);
                        Thread.Sleep(250);
                    }
                    else if (holdKey)
                    {
                        socketData.HasData = true;
                        socketData.DisplayText = "Please Wait...";
                        holdKey = false;
                        _backgroundWorker.ReportProgress(vCnt, socketData);
                    }
                    vCnt++;
                    Thread.Sleep(100);
                }
            }
        }
         private void BackgroundWorker_ProgressChanged(object sender,
            ProgressChangedEventArgs e)
        {
            SocketProcess.SocketData socketData = (SocketProcess.SocketData)e.UserState;
            if (socketData.HasData)
            {
                TextDisplay.FontAttributes = FontAttributes.Bold;
                UpdateDisplay(socketData);
            }
            else
            {
                TextDisplay.FontAttributes = FontAttributes.Italic;
            }
        }
        private void BackgroundWorker_RunWorkerCompleted(
        object sender, RunWorkerCompletedEventArgs e)
        {
        }
    }
}
