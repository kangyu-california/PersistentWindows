using System;
using Ninjacrab.PersistentWindows.WpfShell.WinApiBridge;

namespace Ninjacrab.PersistentWindows.WpfShell.Models
{
    public class ApplicationDisplayMetrics
    {
        public IntPtr HWnd { get; set; }
        public int ProcessId { get; set; }
        public string ApplicationName { get; set; }
        public WindowPlacement WindowPlacement { get; set; }

        public string Key
        {
            get { return string.Format("{0}-{1}", HWnd.ToInt64(), ApplicationName); }
        }
    }
}
