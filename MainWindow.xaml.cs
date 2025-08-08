using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;


namespace TaskbarWorkspaceWidget
{
    public partial class MainWindow : Window
    {
        //Constants for overriding win recogn of the widget
        const int GWL_EXSTYLE = -20;
        const int WS_EX_TOOLWINDOW = 0x00000080;
        const int WS_EX_APPWINDOW = 0x00040000;
        // Those are for attaching to taskbar
        private const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        private const uint WINEVENT_OUTOFCONTEXT = 0;
        //Time dispatchers
        private DispatcherTimer _updateTimer;
        private DispatcherTimer _positionTimer;
        private DispatcherTimer _windowTitleTimer;

        public MainWindow()
        {
            InitializeComponent();
            PositionWindowNearTaskbar();
            StartWindowTitleUpdates();
            SnapToTaskbar();
            this.PreviewMouseWheel += MainWindow_PreviewMouseWheel;
        }

        //Some structs
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        //Focused window handling
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
        
        //Attach to taskbar
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(
            uint eventMin, uint eventMax,
            IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);


        [DllImport("user32.dll")]
        static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        private IntPtr _taskbarHook;
        private WinEventDelegate _taskbarEventDelegate;

        private void HookTaskbar()
        {
            _taskbarEventDelegate = new WinEventDelegate(TaskbarChanged);
            _taskbarHook = SetWinEventHook(
                EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero, _taskbarEventDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        private void UnhookTaskbar()
        {
            if (_taskbarHook != IntPtr.Zero)
            {
                UnhookWinEvent(_taskbarHook);
                _taskbarHook = IntPtr.Zero;
            }
        }

        private void TaskbarChanged(IntPtr hWinEventHook, uint eventType,
            IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // React only to Shell_TrayWnd moves/resizes
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (hwnd == taskbarHandle)
            {
                Dispatcher.Invoke(SnapToTaskbar);
            }
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            IntPtr handler = new WindowInteropHelper(this).Handle;
            int exStyle = GetWindowLong(handler, GWL_EXSTYLE);

            //Replace app window flag with tool window (this makes the tool not being shown on alt+tab)
            exStyle &= ~WS_EX_APPWINDOW;
            exStyle |= WS_EX_TOOLWINDOW;

            SetWindowLong(handler, GWL_EXSTYLE, exStyle);

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
            HookTaskbar();
            SnapToTaskbar();
            StartWindowTitleUpdates();
        }
        private void AdjustPosition()
        {
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
            int total = VirtualDesktopInterop.GetDesktopCount();
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber();

            int target = (current - 1 + total) % total;
            VirtualDesktopInterop.GoToDesktopNumber(target);
        }

        private void NextDesktop_Click(object sender, RoutedEventArgs e)
        {
            int total = VirtualDesktopInterop.GetDesktopCount();
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber();

            int target = (current + 1) % total;
            VirtualDesktopInterop.GoToDesktopNumber(target);
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
        private void MainWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            int total = VirtualDesktopInterop.GetDesktopCount();
            int current = VirtualDesktopInterop.GetCurrentDesktopNumber();

            if (e.Delta > 0) // Scroll up → previous
            {
                int target = (current - 1 + total) % total;
                VirtualDesktopInterop.GoToDesktopNumber(target);
            }
            else if (e.Delta < 0) // Scroll down → next
            {
                int target = (current + 1) % total;
                VirtualDesktopInterop.GoToDesktopNumber(target);
            }
        }
        private void StartWindowTitleUpdates()
        {
            _windowTitleTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _windowTitleTimer.Tick += (s, e) =>
            {
                var title = GetActiveWindowTitle();
                ActiveWindowTitle.Text = string.IsNullOrEmpty(title) ? "" : title;
            };
            _windowTitleTimer.Start();
        }

        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder Buff = new StringBuilder(nChars);
            IntPtr handle = GetForegroundWindow();

            if (GetWindowText(handle, Buff, nChars) > 0)
            {
                return Buff.ToString();
            }
            return string.Empty;
        }

        //Opacity handlers
        private void Window_MouseEnter(object sender, MouseEventArgs e)
        {
            this.Opacity = 1.0;
        }

        private void Window_MouseLeave(object sender, MouseEventArgs e)
        {
            this.Opacity = 0.4;
        }

        //Attach to taskbar method
        private void SnapToTaskbar()
        {
            // Taskbar handle
            IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
            if (taskbarHandle == IntPtr.Zero)
                return;

            if (!GetWindowRect(taskbarHandle, out RECT rect))
                return;

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Determine taskbar position
            bool isBottom = rect.Top > screenHeight / 2;
            bool isTop = rect.Bottom < screenHeight / 2;
            bool isLeft = rect.Right < screenWidth / 2;
            bool isRight = rect.Left > screenWidth / 2;

            if (isBottom)
            {
                this.Left = rect.Left + (rect.Right - rect.Left - this.Width) / 2;
                this.Top = rect.Top - this.Height - 5;
            }
            else if (isTop)
            {
                this.Left = rect.Left + (rect.Right - rect.Left - this.Width) / 2;
                this.Top = rect.Bottom + 5;
            }
            else if (isLeft)
            {
                this.Left = rect.Right + 5;
                this.Top = rect.Top + (rect.Bottom - rect.Top - this.Height) / 2;
            }
            else if (isRight)
            {
                this.Left = rect.Left - this.Width - 5;
                this.Top = rect.Top + (rect.Bottom - rect.Top - this.Height) / 2;
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
                abd.cbSize = (uint)Marshal.SizeOf(abd);
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
}
