using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Essentials;

namespace AquaLogic_xp
{
	public partial class MainPage : TabbedPage
	{

		public MainPage()
		{
            InitializeComponent();

            //Title = "AquaLogic PS8 - " + Assembly.GetExecutingAssembly().GetName().Version.ToString() + " beta";

            LoadSettings();

            InitializeSocketProcess();
        }

        protected void Main_Appeared(object sender, EventArgs e)
        {
            SaveSettings();
        }

        bool _cancelBW = false;

        private void Button_Click(object sender, EventArgs args)
        {
            Button button = (Button)sender;
            if (_socketProcess.QueueKey(button.StyleId))
            {
                TextDisplay.Text = "Please Wait...";
            }
        }
        private void Restart_Click(object sender, EventArgs args)
        {
            RestartUART();
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


        // Socket Control

        private SocketProcess _socketProcess;
        bool _cancelTimer = false;

        private void RestartUART()
        {
            _cancelTimer = true;
            if (_socketProcess != null)
            {
                _socketProcess.QueueKey("Reset");
                System.Threading.Thread.Sleep(100);
            }
            _cancelBW = false;
            InitializeSocketProcess();
        }

        int _vCnt = 0;
        private void InitializeSocketProcess()
        {
            _socketProcess = new(ipAddr.Text, Int32.Parse(portNum.Text));

            if (_socketProcess.Connected)
            {
                _cancelTimer = false;
                Device.StartTimer(TimeSpan.FromMilliseconds(100), () =>
                {
                    if (_cancelBW) { return false; }
                    Device.BeginInvokeOnMainThread(() =>
                    {
                        SocketProcess.SocketData socketData = _socketProcess.Update();

                        if (socketData.Valid)
                        {
                            UpdateDisplay(socketData);
                            _vCnt = 0;
                        }
                        else
                        {
                            _vCnt += 1;
                            if (_vCnt > 100)
                            {
                                socketData.DisplayText = "Communication\nError";
                                UpdateDisplay(socketData); _vCnt = 0;
                            }
                        }
                    });
                    return !_cancelTimer;
                });
            }
            else
            {
                TextDisplay.Text = "Connection\nError";
            }
        }

        private readonly string _logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "AquaLogic.csv");
        private DateTime _lastLog = DateTime.Now;
        private void UpdateDisplay(SocketProcess.SocketData socketData)
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

            int logInt = Int32.Parse(LogInt.Text);
            if (socketData.LogText != null && logInt > 0 && DateTime.Now >= _lastLog.AddMinutes(logInt))
            {
                _lastLog = DateTime.Now;
                SocketProcess.WriteTextFile(_logPath, socketData.LogText);
            }
        }
        private static void SetStatus(Button button, SocketProcess.States status, SocketProcess.States blink, SocketProcess.States state)
        {
            button.FontAttributes = (status.HasFlag(state) ? FontAttributes.Bold : FontAttributes.None) |
                    (blink.HasFlag(state) ? FontAttributes.Italic : FontAttributes.None);
        }
    }
}
