using AdminOverlay.Classes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AdminOverlay
{
    public partial class MainWindow : Window
    {
        private OverlayWindow? _overlay;
        private LogReader _logOlvaso;
        private DispatcherTimer? _timer;

        public MainWindow()
        {
            InitializeComponent();

            
            _logOlvaso = new LogReader();
           
            txtBemenet.Text = Properties.Settings.Default.mentettAdminNev;
            logBemenet.Text = Properties.Settings.Default.mentettLogUtvonal;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await _logOlvaso.ReadNewLineAsync();

            if (_overlay != null)
            {
                string valosReportSzam = _logOlvaso.reportCounter.ToString();

                string valosDuty = _logOlvaso.GetDutyTimeStr();
                string valosOffDuty = _logOlvaso.GetOffDutyTimeStr();

                _overlay.FrissitAdatok(valosReportSzam, valosDuty, valosOffDuty);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_logOlvaso.AdminName == "")
            {
                MessageBox.Show("Nem lehet üres az adminnév!");
                return;
            }

            else if (_overlay == null)
            {
                BtnStart.IsEnabled = false;

                if (await _logOlvaso.FirstReadAllLogfilesAsync())
                {
                    _timer = new DispatcherTimer();
                    _timer.Interval = TimeSpan.FromSeconds(10); // változonak kivenni és beállítható idő
                    _timer.Tick += Timer_Tick;
                    _timer.Start();


                    _overlay = new OverlayWindow();
                    _overlay.Show();

                    BtnStop.IsEnabled = true;
                    txtBemenet.IsEnabled = false;
                    logBemenet.IsEnabled = false;
                }

                

            }

        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer = null;
            }

            if (_overlay != null)
            {
                _overlay.Close();
                _overlay = null;
            }

            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
            txtBemenet.IsEnabled = true;
            logBemenet.IsEnabled = true;
        }


        protected override void OnClosed(EventArgs e)
        {
            _timer?.Stop();
            _overlay?.Close();
            base.OnClosed(e);
        }

        private void TxtBemenet_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_logOlvaso != null)
            {
                _logOlvaso.AdminName = txtBemenet.Text;
            }
        }

        private void TxtBemenet_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }

        private void logBemenet_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        { 
            if (_logOlvaso != null)
            {
                if (string.IsNullOrWhiteSpace(logBemenet.Text))
                {
                    
                    _logOlvaso.LogDirectoryPath = @"C:\SeeMTA\mta\logs\";
                }
                else
                {
                    _logOlvaso.LogDirectoryPath = logBemenet.Text;
                }
            }
        }


        private void logBemenet_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e) 
        {
            Properties.Settings.Default.mentettAdminNev = txtBemenet.Text;
            Properties.Settings.Default.mentettLogUtvonal = logBemenet.Text;

            Properties.Settings.Default.Save();
        }
    }
}