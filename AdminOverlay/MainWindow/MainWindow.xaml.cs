using AdminOverlay.Classes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AdminOverlay
{
    public partial class MainWindow : Window
    {
        private const int LogUpdateIntervalSeconds = 10;

        private OverlayWindow? _overlay;
        private LogReader _logReader;
        private DispatcherTimer? _timer;

        public MainWindow()
        {
            InitializeComponent();

            
            _logReader = new LogReader();
           
            txtBemenet.Text = Properties.Settings.Default.mentettAdminNev;
            logBemenet.Text = Properties.Settings.Default.mentettLogUtvonal;
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            await _logReader.ReadAndProcessNewLinesAsync();

            if (_overlay != null)
            {
                string displayReportCount = _logReader.reportCounter.ToString();

                string displayOnDutyMinutes = _logReader.GetDutyTimeStr();
                string displayOffDutyMinutes = _logReader.GetOffDutyTimeStr();

                _overlay.UpdateDisplayedStats(displayReportCount, displayOnDutyMinutes, displayOffDutyMinutes);
            }
        }

        private async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_logReader.AdminName))
            {
                MessageBox.Show("Nem lehet üres az adminnév!");
                return;
            }

            else if (_overlay == null)
            {
                BtnStart.IsEnabled = false;

                InitializationResult result = await _logReader.FirstReadAndProcessAllLogfilesAsync();

                switch (result)
                {
                    case InitializationResult.Success:

                        _timer = new DispatcherTimer();
                        _timer.Interval = TimeSpan.FromSeconds(LogUpdateIntervalSeconds);
                        _timer.Tick += Timer_Tick;
                        _timer.Start();


                        _overlay = new OverlayWindow();
                        _overlay.Show();

                        BtnStop.IsEnabled = true;
                        txtBemenet.IsEnabled = false;
                        logBemenet.IsEnabled = false;

                        break;

                    case InitializationResult.DirectoryNotFound:
                        MessageBox.Show("A megadott mappa nem található!");
                        BtnStart.IsEnabled = true;
                        break;

                    case InitializationResult.NoLogFilesFound:
                        MessageBox.Show("A mappában nem találhatóak 'console-*.log' fájlok!");
                        BtnStart.IsEnabled = true;
                        break;
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
            if (_logReader != null)
            {
                _logReader.AdminName = txtBemenet.Text;
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
            if (_logReader != null)
            {
                if (string.IsNullOrWhiteSpace(logBemenet.Text))
                {
                    
                    _logReader.LogDirectoryPath = @"C:\SeeMTA\mta\logs\";
                }
                else
                {
                    _logReader.LogDirectoryPath = logBemenet.Text;
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