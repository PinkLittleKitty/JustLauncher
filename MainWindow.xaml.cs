using System;
using System.Configuration;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace JustLauncher
{
    public partial class MainWindow : Window
    {
        private bool isMaximized = false;
        private Rect normalBounds;

        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new HomePage();
            SourceInitialized += MainWindow_SourceInitialized;
            StateChanged += MainWindow_StateChanged;
        }

        private void MainWindow_SourceInitialized(object sender, EventArgs e)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            var source = HwndSource.FromHwnd(hwnd);
            source.AddHook(WndProc);
        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized && !isMaximized)
            {
                isMaximized = true;
                UpdateWindowAppearance();
            }
            else if (WindowState == WindowState.Normal && isMaximized)
            {
                isMaximized = false;
                UpdateWindowAppearance();
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;
            const int WM_NCHITTEST = 0x0084;
            
            switch (msg)
            {
                case WM_GETMINMAXINFO:
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    var monitor = MonitorFromWindow(hwnd, 2);
                    if (monitor != IntPtr.Zero)
                    {
                        var monitorInfo = new MONITORINFO();
                        monitorInfo.cbSize = Marshal.SizeOf(monitorInfo);
                        GetMonitorInfo(monitor, ref monitorInfo);
                        var workArea = monitorInfo.rcWork;
                        mmi.ptMaxPosition.x = workArea.left;
                        mmi.ptMaxPosition.y = workArea.top;
                        mmi.ptMaxSize.x = workArea.right - workArea.left;
                        mmi.ptMaxSize.y = workArea.bottom - workArea.top;
                    }
                    Marshal.StructureToPtr(mmi, lParam, true);
                    handled = true;
                    break;

                case WM_NCHITTEST:
                    var point = new Point(lParam.ToInt32() & 0xFFFF, lParam.ToInt32() >> 16);
                    point = PointFromScreen(point);
                    handled = HandleHitTest(point, out IntPtr result);
                    if (handled)
                        return result;
                    break;
            }
            return IntPtr.Zero;
        }

        private bool HandleHitTest(Point point, out IntPtr result)
        {
            result = (IntPtr)1;
            
            if (isMaximized)
                return false;

            const int resizeMargin = 8;
            var rect = new Rect(0, 0, ActualWidth, ActualHeight);

            if (point.X <= resizeMargin && point.Y <= resizeMargin)
            {
                result = (IntPtr)13;
                return true;
            }
            if (point.X >= rect.Width - resizeMargin && point.Y <= resizeMargin)
            {
                result = (IntPtr)14;
                return true;
            }
            if (point.X <= resizeMargin && point.Y >= rect.Height - resizeMargin)
            {
                result = (IntPtr)16;
                return true;
            }
            if (point.X >= rect.Width - resizeMargin && point.Y >= rect.Height - resizeMargin)
            {
                result = (IntPtr)17;
                return true;
            }

            if (point.X <= resizeMargin)
            {
                result = (IntPtr)10;
                return true;
            }
            if (point.X >= rect.Width - resizeMargin)
            {
                result = (IntPtr)11;
                return true;
            }
            if (point.Y <= resizeMargin)
            {
                result = (IntPtr)12;
                return true;
            }
            if (point.Y >= rect.Height - resizeMargin)
            {
                result = (IntPtr)15;
                return true;
            }

            return false;
        }

        private void UpdateWindowAppearance()
        {
            if (isMaximized)
            {
                MaximizeButton.Content = "\uE923";
                MainBorder.CornerRadius = new CornerRadius(0);
            }
            else
            {
                MaximizeButton.Content = "\uE922";
                MainBorder.CornerRadius = new CornerRadius(10);
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            string username = ConfigurationManager.AppSettings["LastUsedUsername"] ?? "Player";
            MainContent.Content = new PlayPage(username);
        }

        private void AccountsButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new AccountsPage();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            MainContent.Content = new SettingsPage();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
            }
            else if (e.LeftButton == MouseButtonState.Pressed)
            {
                if (isMaximized)
                {
                    var mousePos = PointToScreen(e.GetPosition(this));
                    var workingArea = SystemParameters.WorkArea;
                    
                    var relativeX = mousePos.X / workingArea.Width;
                    
                    isMaximized = false;
                    WindowState = WindowState.Normal;
                    
                    Width = normalBounds.Width;
                    Height = normalBounds.Height;
                    
                    Left = mousePos.X - (Width * relativeX);
                    Top = mousePos.Y - 16;
                    
                    UpdateWindowAppearance();
                }
                
                try
                {
                    DragMove();
                }
                catch
                {

                }
            }
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (isMaximized)
            {
                WindowState = WindowState.Normal;
                Left = normalBounds.Left;
                Top = normalBounds.Top;
                Width = normalBounds.Width;
                Height = normalBounds.Height;
                isMaximized = false;
            }
            else
            {
                normalBounds = new Rect(Left, Top, Width, Height);
                WindowState = WindowState.Maximized;
                isMaximized = true;
            }
            UpdateWindowAppearance();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}