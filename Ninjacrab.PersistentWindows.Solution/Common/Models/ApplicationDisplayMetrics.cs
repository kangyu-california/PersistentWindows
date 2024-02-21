using System;

using PersistentWindows.Common.WinApiBridge;
using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.Common.Models
{
    public class DeadAppPosition
    {
        public string ClassName { get; set; }
        public string Title { get; set; }
        public string ProcessPath { get; set; }
        public RECT ScreenPosition { get; set; }
    }

    public class ApplicationDisplayMetrics
    {
        // for LiteDB use only
        public int Id { get; set; }
        public Guid Guid { get; set; }
        public uint ProcessId { get; set; }
        public string ProcessExePath { get; set; }

        // general window info
        public IntPtr HWnd { get; set; }
        public uint WindowId { get; set; }
        public string ClassName { get; set; }
        public string ProcessName { get; set; }
        public string Title { get; set; }
        public string Dir { get; set; }
        public bool IsFullScreen { get; set; }
        public bool IsMinimized { get; set; }
        public bool IsInvisible { get; set; }

        // for restore window position to display session end time
        public DateTime CaptureTime { get; set; }

        // window position
        public RECT ScreenPosition { get; set; }
        public WindowPlacement WindowPlacement { get; set; }
        public bool NeedUpdateWindowPlacement { get; set; } //non-persistent data used for tmp argument passing only

        // window z-order
        public bool IsTopMost { get; set; }
        public bool NeedClearTopMost { get; set; }
        public IntPtr PrevZorderWindow { get; set; }
        public bool NeedRestoreZorder { get; set; }

        // for filter invalid entry
        public bool IsValid { get; set; }

        // for snapshot recovery
        public ulong SnapShotFlags { get; set; }

        public bool EqualPlacement(ApplicationDisplayMetrics other)
        {
            bool posEqual = this.WindowPlacement.NormalPosition.Equals(other.WindowPlacement.NormalPosition);
            bool minmaxStateEqual = this.WindowPlacement.ShowCmd == other.WindowPlacement.ShowCmd;
            bool allEqual = posEqual && minmaxStateEqual;
            return allEqual;
        }

        public override string ToString()
        {
            //return string.Format("{0}.{1} {2}", ProcessId, HWnd.ToString("X8"), ProcessName);
            return string.Format("{0}.{1:x8} {2}", ProcessId, HWnd.ToInt64(), ProcessName);
        }
    }
}
