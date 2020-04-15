using System;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;
using ManagedWinapi.Windows;

namespace Ninjacrab.PersistentWindows.Common.Models
{
    public class ApplicationDisplayMetrics
    {
        // for LiteDB use only
        public int Id { get; set; }
        public bool DbMatchWindow { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessExePath { get; set; }

        // general window info
        public IntPtr HWnd { get; set; }
        public string ClassName { get; set; }
        public string ProcessName { get; set; }
        public string Title { get; set; }
        public bool IsTaskbar { get; set; }

        // for restore window position to display session end time
        public DateTime CaptureTime { get; set; }

        // window position
        public RECT2 ScreenPosition { get; set; }
        public WindowPlacement WindowPlacement { get; set; }
        public bool NeedUpdateWindowPlacement { get; set; } //non-persistent data used for tmp argument passing only

        public static string GetKey(IntPtr hWnd)
        {
            //return string.Format("{0}-{1}", hWnd.ToString("X8"), applicationName);
            return string.Format("{0}", hWnd.ToString("X8"));
        }
        public string Key
        {
            get { return GetKey(HWnd); }
        }

        public bool EqualPlacement(ApplicationDisplayMetrics other)
        {
            bool posEqual = this.WindowPlacement.NormalPosition.Equals(other.WindowPlacement.NormalPosition);
            bool minmaxStateEqual = this.WindowPlacement.ShowCmd == other.WindowPlacement.ShowCmd;
            return posEqual && minmaxStateEqual;
        }

        public override string ToString()
        {
            return string.Format("{0}.{1} {2}", ProcessId, HWnd.ToString("X8"), ProcessName);
        }
    }
}
