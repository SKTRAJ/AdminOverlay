using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace AdminOverlay
{
    public partial class OverlayWindow : Window
    {
        public OverlayWindow()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // Átkattinthatóság 
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);


            var screenWidth = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            this.Left = screenWidth - this.Width - 50;
            this.Top = (screenHeight / 2) - (this.Height / 2); // jobb közép az ablak
        }


        public void FrissitAdatok(string reportSzam, string dutyPerc, string offDutyPerc)
        {
            TxtReport.Text = reportSzam;
            TxtDuty.Text = dutyPerc;
            TxtOffDuty.Text = offDutyPerc;
        }

        #region Windows API
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TRANSPARENT = 0x00000020;
        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        #endregion
    }
}