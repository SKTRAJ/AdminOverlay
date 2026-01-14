using AdminOverlay.Classes;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace AdminOverlay
{
    public partial class MainWindow : Window
    {
        private OverlayWindow? _overlay;
        private LogOlvaso _logOlvaso;
        private DispatcherTimer? _timer;

        public MainWindow()
        {
            InitializeComponent();

            _logOlvaso = new LogOlvaso();

        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _logOlvaso.OlvasdAzUjSorokat();

            if (_overlay != null)
            {
                // Report
                string valosReportSzam = _logOlvaso.reportSzamlalo.ToString();

                // OnDuty / OffDuty 
                // A LogOlvasobol segéd függvények
                string valosDuty = _logOlvaso.GetDutyTimeStr();
                string valosOffDuty = _logOlvaso.GetOffDutyTimeStr();

                _overlay.FrissitAdatok(valosReportSzam, valosDuty, valosOffDuty);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (_logOlvaso.adminName == "")
            {
                MessageBox.Show("Nem lehet üres az adminnév!");
                return;
            }
            else if (_overlay == null)
            {
                _logOlvaso.BeolvasasMindenLogbol();

                _timer = new DispatcherTimer();
                _timer.Interval = TimeSpan.FromSeconds(1);
                _timer.Tick += Timer_Tick;
                _timer.Start();


                _overlay = new OverlayWindow();
                _overlay.Show();

            }
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            txtBemenet.IsEnabled = false;
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
                _logOlvaso.adminName = txtBemenet.Text;
            }
        }

        private void TxtBemenet_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Keyboard.ClearFocus();
            }
        }
    }
}