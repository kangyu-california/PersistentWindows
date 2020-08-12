using System;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;
using ManagedWinapi.Windows;

namespace Ninjacrab.PersistentWindows.Common.Models
{
    public class ApplicationDisplayMetrics
    {
        // for LiteDB use only
        public int Id { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessExePath { get; set; }

        // general window info
        public IntPtr HWnd { get; set; }
        public string ClassName { get; set; }
        public string ProcessName { get; set; }
        public string Title { get; set; }
        public bool IsFullScreen { get; set; }
        public bool IsMinimized { get; set; }

        // for restore window position to display session end time
        public DateTime CaptureTime { get; set; }

        // window position
        public RECT2 ScreenPosition { get; set; }
        public RECT2 SnapPosition { get; set; } // for restore snap window from minimize state
        public WindowPlacement WindowPlacement { get; set; }
        public bool NeedUpdateWindowPlacement { get; set; } //non-persistent data used for tmp argument passing only

        // window z-order
        public bool IsTopMost { get; set; }
        public bool NeedClearTopMost { get; set; }
        public IntPtr PrevZorderWindow { get; set; }
        public bool NeedRestoreZorder { get; set; }

        public bool EqualPlacement(ApplicationDisplayMetrics other)
        {
            bool posEqual = this.WindowPlacement.NormalPosition.Equals(other.WindowPlacement.NormalPosition);
            bool minmaxStateEqual = this.WindowPlacement.ShowCmd == other.WindowPlacement.ShowCmd;
            bool minimizeStateEqual = this.IsMinimized == other.IsMinimized;
            return posEqual && minmaxStateEqual && minimizeStateEqual;
        }

        public override string ToString()
        {
            //return string.Format("{0}.{1} {2}", ProcessId, HWnd.ToString("X8"), ProcessName);
            return string.Format("{0}.{1:x8} {2}", ProcessId, HWnd.ToInt64(), ProcessName);
        }
    }
}
