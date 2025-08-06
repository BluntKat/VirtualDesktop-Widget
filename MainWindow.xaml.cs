using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using WindowsDesktop;


namespace TaskbarWorkspaceWidget
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _updateTimer;
        private DispatcherTimer _positionTimer;

        public MainWindow()
        {
            InitializeComponent();
            PositionWindowNearTaskbar();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Pin this window to all desktops
            var hwnd = new WindowInteropHelper(this).Handle;
            VirtualDesktopInterop.PinWindow(hwnd);
            // Update indicator every 500ms
            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromMilliseconds(500);
            _updateTimer.Tick += (_, __) => UpdateIndicators();
            _updateTimer.Start();
            // Adjust position when taskbar auto hiding is enabled
            _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            _positionTimer.Tick += (_, __) => AdjustPosition();
            _positionTimer.Start();

            AdjustPosition();
            UpdateIndicators();
        }
        private void AdjustPosition() {
            var screenHeight = SystemParameters.PrimaryScreenHeight;
            var taskbarHeight = TaskbarHelper.GetTaskbarHeight();
            var autoHide = TaskbarHelper.IsTaskbarAutoHide();

            if (autoHide)
            {
                // Move to absolute bottom
                this.Top = screenHeight - this.Height;
            }
            else
            {
                // Stay above taskbar
                this.Top = screenHeight - taskbarHeight - this.Height;
            }

            // Center horizontally (optional)
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;

        }
        private void UpdateIndicators()
        {
            int total = VirtualDesktopInterop.GetDesktopCount();
            // Cause it's zero-based
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber() + 1;

            string indicators = string.Join(" ",
                Enumerable.Range(1, total)
                          .Select(i => i == current ? "●" : "○"));

            WorkspaceIndicators.Text = indicators;
        }

        private void PrevDesktop_Click(object sender, RoutedEventArgs e)
        {
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber();
            if (current > 1)
                VirtualDesktopInterop.GoToDesktopNumber(current - 1);
        }

        private void NextDesktop_Click(object sender, RoutedEventArgs e)
        {
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber();
            int total = VirtualDesktopInterop.GetDesktopCount();
            if (current < total)
                VirtualDesktopInterop.GoToDesktopNumber(current + 1);
        }

        private void PositionWindowNearTaskbar()
        {
            var taskbarPos = TaskbarHelper.GetTaskbarPosition();

            // Position the window based on taskbar position
            switch (taskbarPos)
            {
                case TaskbarHelper.TaskbarPosition.Bottom:
                    Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                    Top = SystemParameters.PrimaryScreenHeight - Height - TaskbarHelper.GetTaskbarHeight();
                    break;

                case TaskbarHelper.TaskbarPosition.Top:
                    Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
                    Top = TaskbarHelper.GetTaskbarHeight();
                    break;

                case TaskbarHelper.TaskbarPosition.Left:
                    Left = TaskbarHelper.GetTaskbarWidth();
                    Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
                    break;

                case TaskbarHelper.TaskbarPosition.Right:
                    Left = SystemParameters.PrimaryScreenWidth - Width - TaskbarHelper.GetTaskbarWidth();
                    Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;
                    break;
            }
        }
    }

    public static class TaskbarHelper
    {
        private const int ABM_GETSTATE = 4;
        private const int ABS_AUTOHIDE = 1;

        public enum TaskbarPosition
        {
            Left, Top, Right, Bottom, Unknown
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        private const uint ABM_GETTASKBARPOS = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left, top, right, bottom;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct APPBARDATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        public static bool IsTaskbarAutoHide()
        {
            APPBARDATA abd = new APPBARDATA();
            abd.cbSize = (uint) Marshal.SizeOf(abd);
            int state = (int)SHAppBarMessage(ABM_GETSTATE, ref abd);
            return (state & ABS_AUTOHIDE) == ABS_AUTOHIDE;
        }


        public static TaskbarPosition GetTaskbarPosition()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            if (result == IntPtr.Zero)
                return TaskbarPosition.Unknown;

            switch (data.uEdge)
            {
                case 0: return TaskbarPosition.Left;
                case 1: return TaskbarPosition.Top;
                case 2: return TaskbarPosition.Right;
                case 3: return TaskbarPosition.Bottom;
                default: return TaskbarPosition.Unknown;
            }
        }

        public static double GetTaskbarHeight()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            if (result == IntPtr.Zero)
                return 40; // default guess

            return Math.Abs(data.rc.bottom - data.rc.top);
        }

        public static double GetTaskbarWidth()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA));
            IntPtr result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);
            if (result == IntPtr.Zero)
                return 40; // default guess

            return Math.Abs(data.rc.right - data.rc.left);
        }
    }

   
}
