using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Ninjacrab.PersistentWindows.Common.WinApiBridge
{
    public class Display
    {
        public RECT Position;
        public uint Flags { get; internal set; }
        public String DeviceName { get; internal set; }

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
                        display.Position = monitorInfo.Monitor;
                        display.Flags = monitorInfo.Flags;

                        //int pos = monitorInfo.DeviceName.LastIndexOf("\\") + 1;
                        //display.DeviceName = monitorInfo.DeviceName.Substring(pos, monitorInfo.DeviceName.Length - pos);
                        display.DeviceName = "Display";

                        displays.Add(display);
                    }
                    return true;
                }, IntPtr.Zero);
            return displays;
        }
    }
}
