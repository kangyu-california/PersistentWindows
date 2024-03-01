
using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Drawing;
using System.Text;
using System.Timers;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common.Minimize2Tray
{
    public class MinimizeToTray : IDisposable
    {
        private static HashSet<IntPtr> _trayWindows = new HashSet<IntPtr>();
        private NotifyIcon _systemTrayIcon = null;
        private IntPtr _hwnd;
        private string _window_txt;
        private System.Timers.Timer _timer;

        static public void Create(IntPtr hwnd)
        {
            if (_trayWindows.Contains(hwnd))
                return;

            // clear ctrl state
            User32.GetAsyncKeyState(0x11);
            bool ctrl_key_pressed = (User32.GetAsyncKeyState(0x11) & 0x8000) != 0;
            if (!ctrl_key_pressed)
                return;

            _trayWindows.Add(hwnd);
            new MinimizeToTray(hwnd);
        }

        public MinimizeToTray(IntPtr hwnd)
        {
            User32.ShowWindow(hwnd, (int)ShowWindowCommands.Hide);
            CreateIconInSystemTray(hwnd);
            //User32.ShowWindowAsync(hwnd, (int)ShowWindowCommands.Minimize);
        }

        private static string GetWindowText(IntPtr hWnd)
        {
            var builder = new StringBuilder(User32.GetWindowTextLength(hWnd) + 1);
            User32.GetWindowText(hWnd, builder, builder.Capacity);
            var windowText = builder.ToString();
            return windowText;
        }

        private static string TruncateString(string str, int max_length)
        {
            return str.Substring(0, Math.Min(max_length, str.Length));
        }

        private void TimerCallBack(Object source, ElapsedEventArgs e)
        {
            _systemTrayIcon.Text = TruncateString(_window_txt, 63);
        }

        private void CreateIconInSystemTray(IntPtr hwnd)
        {
            //_systemTrayMenu = CreateSystemTrayMenu(hwnd);
            _hwnd = hwnd;
            _systemTrayIcon = CreateNotifyIcon();
            _systemTrayIcon.Icon = GetIcon(hwnd);
            _systemTrayIcon.Visible = true;
            _window_txt = GetWindowText(hwnd);
            int dash_idx = _window_txt.IndexOf('-');
            if (dash_idx > 0)
            {
                //rest of window txt is the real application name
                _systemTrayIcon.Text = TruncateString(_window_txt.Substring(dash_idx + 2), 63);
            }

            _timer = new System.Timers.Timer(500);
            _timer.Elapsed += TimerCallBack;
            _timer.AutoReset = false;
            _timer.Enabled = true;
        }
        private NotifyIcon CreateNotifyIcon()
        {
            var icon = new NotifyIcon();
            //icon.ContextMenuStrip = contextMenuStrip;
            icon.MouseClick += SystemTrayIconClick;
            return icon;
        }
        public static Icon GetIcon(IntPtr hWnd)
        {
            IntPtr icon;
            try
            {
                User32.SendMessageTimeout(hWnd, User32.WM_GETICON, User32.ICON_SMALL2, 0, User32.SMTO_ABORTIFHUNG, 100, out var result);
                icon = new IntPtr(result);

                if (icon == IntPtr.Zero)
                {
                    User32.SendMessageTimeout(hWnd, User32.WM_GETICON, User32.ICON_SMALL, 0, User32.SMTO_ABORTIFHUNG, 100, out result);
                    icon = new IntPtr(result);
                }

                if (icon == IntPtr.Zero)
                {
                    User32.SendMessageTimeout(hWnd, User32.WM_GETICON, User32.ICON_BIG, 0, User32.SMTO_ABORTIFHUNG, 100, out result);
                    icon = new IntPtr(result);
                }

                if (icon == IntPtr.Zero)
                {
                    icon = User32.GetClassLongPtr(hWnd, User32.GCLP_HICONSM);
                }

                if (icon == IntPtr.Zero)
                {
                    icon = User32.GetClassLongPtr(hWnd, User32.GCLP_HICON);
                }

                if (icon == IntPtr.Zero)
                {
                    icon = User32.LoadIcon(IntPtr.Zero, User32.IDI_APPLICATION);
                }
            }
            catch
            {
                icon = User32.LoadIcon(IntPtr.Zero, User32.IDI_APPLICATION);
            }

            return Icon.FromHandle(icon);
        }

        private void SystemTrayIconClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Dispose();
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayWindows.Remove(_hwnd);

                _systemTrayIcon.Visible = false;
                //User32.ShowWindowAsync(_hwnd, (int)ShowWindowCommands.Show);
                User32.ShowWindowAsync(_hwnd, (int)ShowWindowCommands.Restore);
                User32.SetForegroundWindow(_hwnd);

                _systemTrayIcon.MouseClick -= SystemTrayIconClick;
                //_systemTrayIcon.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~MinimizeToTray()
        {
            Dispose();
        }
    }
}
