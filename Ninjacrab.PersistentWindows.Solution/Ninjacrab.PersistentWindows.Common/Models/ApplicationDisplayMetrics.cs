using System;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;
using ManagedWinapi.Windows;

namespace Ninjacrab.PersistentWindows.Common.Models
{
    public class ApplicationDisplayMetrics
    {
        public IntPtr HWnd { get; set; }
        public uint ProcessId { get; set; }
        public string ApplicationName { get; set; }
        public RECT ScreenPosition { get; set; }
        public WindowPlacement WindowPlacement { get; set; }
        // try recover sudden WindowPlacement change when ScreenPosition remains the same
        public bool RecoverWindowPlacement { get; set; }

        public static string GetKey(IntPtr hWnd, string applicationName)
        {
            // in release mode, ApplicatioName is "" to reduce runtime
#if DEBUG
            return string.Format("{0}-{1}", hWnd.ToString("X8"), applicationName);
#else
            return string.Format("{0}", hWnd.ToString("X8"));
#endif
        }
        public string Key
        {
            get { return GetKey(HWnd, ApplicationName); }
        }

        public bool EqualPlacement(ApplicationDisplayMetrics other)
        {
            /*
            return this.WindowPlacement.NormalPosition.Left == other.WindowPlacement.NormalPosition.Left
                && this.WindowPlacement.NormalPosition.Top == other.WindowPlacement.NormalPosition.Top
                && this.WindowPlacement.NormalPosition.Width == other.WindowPlacement.NormalPosition.Width
                && this.WindowPlacement.NormalPosition.Height == other.WindowPlacement.NormalPosition.Height;
            */
            /*
            bool posEqual = this.WindowPlacement.NormalPosition.Equals(other.WindowPlacement.NormalPosition);
            bool minmaxStateEqual = this.WindowPlacement.ShowCmd == other.WindowPlacement.ShowCmd;
            return posEqual && minmaxStateEqual;
            */
            return this.WindowPlacement.NormalPosition.Equals(other.WindowPlacement.NormalPosition);
            //return this.WindowPlacement.Equals(other.WindowPlacement);
        }

        public override string ToString()
        {
            return string.Format("{0}.{1} {2}", ProcessId, HWnd.ToString("X8"), ApplicationName);
        }
    }
}
