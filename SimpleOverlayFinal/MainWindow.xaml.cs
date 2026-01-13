using System;
using System.Windows;
using System.Windows.Threading;

namespace SimpleOverlayFinal
{
    public partial class MainWindow : Window
    {
        private OverlayWindow? _overlay;
        private LogOlvaso _logOlvaso;
        private DispatcherTimer _timer;

        public MainWindow()
        {
            InitializeComponent();

            _logOlvaso = new LogOlvaso();

            
            _logOlvaso.BeolvasasMindenLogbol();

            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _logOlvaso.OlvasdAzUjSorokat();

            if (_overlay != null)
            {
                // Report
                string valosReportSzam = _logOlvaso.ReportSzamlalo.ToString();

                // OnDuty / OffDuty 
                // A LogOlvasobol segéd függvények
                string valosDuty = _logOlvaso.GetDutyTimeStr();
                string valosOffDuty = _logOlvaso.GetOffDutyTimeStr();

                _overlay.FrissitAdatok(valosReportSzam, valosDuty, valosOffDuty);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay == null)
            {
                _overlay = new OverlayWindow();
                _overlay.Show();
                
            }
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (_overlay != null)
            {
                _overlay.Close();
                _overlay = null;
            }
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }


        protected override void OnClosed(EventArgs e)
        {
            _overlay?.Close();
            base.OnClosed(e);
        }
    }
}