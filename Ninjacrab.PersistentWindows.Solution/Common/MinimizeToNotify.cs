
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Win32;

using LiteDB;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.Models;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common.Minimize2Tray
{
    public class MinimizeToTray : IDisposable
    {
        private NotifyIcon _systemTrayIcon = null;
        private IntPtr _hwnd;

        public MinimizeToTray(IntPtr hwnd)
        {
            User32.ShowWindow(hwnd, (int)ShowWindowCommands.Hide);
            Thread.Sleep(500);
            CreateIconInSystemTray(hwnd);
            //User32.ShowWindowAsync(hwnd, (int)ShowWindowCommands.Minimize);
        }

        public static string GetWindowText(IntPtr hWnd)
        {
            var builder = new StringBuilder(1024);
            User32.GetWindowText(hWnd, builder, builder.Capacity);
            var windowText = builder.ToString();
            return windowText;
        }

        private void CreateIconInSystemTray(IntPtr hwnd)
        {
            //_systemTrayMenu = CreateSystemTrayMenu(hwnd);
            _hwnd = hwnd;
            _systemTrayIcon = CreateNotifyIcon();
            _systemTrayIcon.Icon = GetIcon(hwnd);
            var windowText = GetWindowText(hwnd);
            _systemTrayIcon.Text = windowText.Length > 63 ? windowText.Substring(0, 60).PadRight(63, '.') : windowText;
            _systemTrayIcon.Visible = true;
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

        public void Dispose()
        {
            _systemTrayIcon.Visible = false;
            User32.ShowWindowAsync(_hwnd, (int)ShowWindowCommands.Show);
            User32.ShowWindowAsync(_hwnd, (int)ShowWindowCommands.Restore);
            User32.SetForegroundWindow(_hwnd);

            _systemTrayIcon.MouseClick -= SystemTrayIconClick;
            _systemTrayIcon.Dispose();
            GC.SuppressFinalize(this);
        }

        ~MinimizeToTray()
        {
            Dispose();
        }
    }
}
