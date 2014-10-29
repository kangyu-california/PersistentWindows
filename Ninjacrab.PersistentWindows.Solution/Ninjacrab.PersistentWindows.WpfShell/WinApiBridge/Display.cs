using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ManagedWinapi.Windows;

namespace Ninjacrab.PersistentWindows.WpfShell.WinApiBridge
{
    public class Display
    {
        public int ScreenWidth { get; internal set; }
        public int ScreenHeight { get; internal set; }
        public int Left { get; internal set; }
        public int Top { get; internal set; }
        public uint Flags { get; internal set; }

        public static List<Display> GetDisplays()
        {
            List<Display> displays = new List<Display>();

            User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                delegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    MonitorInfo monitorInfo = new MonitorInfo();
                    monitorInfo.StructureSize = Marshal.SizeOf(monitorInfo);
                    bool success = User32.GetMonitorInfo(hMonitor, ref monitorInfo);
                    if (success)
                    {
                        Display display = new Display();
                        display.ScreenWidth = monitorInfo.Monitor.Width;
                        display.ScreenHeight = monitorInfo.Monitor.Height;
                        display.Left = monitorInfo.Monitor.Left;
                        display.Top = monitorInfo.Monitor.Top;
                        display.Flags = monitorInfo.Flags;
                        displays.Add(display);
                    }
                    return true;
                }, IntPtr.Zero);
            return displays;
        }
    }
}
