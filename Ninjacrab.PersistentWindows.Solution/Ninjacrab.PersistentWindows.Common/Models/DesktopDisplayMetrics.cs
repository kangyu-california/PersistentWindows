using System.Collections.Generic;
using System.Linq;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.Common.Models
{
    public class DesktopDisplayMetrics
    {
        public static DesktopDisplayMetrics AcquireMetrics()
        {
            DesktopDisplayMetrics metrics = new DesktopDisplayMetrics();

            var displays = Display.GetDisplays();
            int displayId = 0;
            foreach (var display in displays)
            {
                metrics.SetMonitor(displayId++, display);
            }
            return metrics;
        }

        private Dictionary<int, Display> monitorResolutions = new Dictionary<int, Display>();

        public int NumberOfDisplays { get { return monitorResolutions.Count; } }

        public void SetMonitor(int id, Display display)
        {
            if (!monitorResolutions.ContainsKey(id) ||
                monitorResolutions[id].ScreenWidth != display.ScreenWidth ||
                monitorResolutions[id].ScreenHeight != display.ScreenHeight)
            {
                monitorResolutions.Add(id, display);
                BuildKey();
            }
        }

        private void BuildKey()
        {
            List<string> keySegments = new List<string>();
            foreach (var entry in monitorResolutions.OrderBy(row => row.Value.DeviceName))
            {
                keySegments.Add(string.Format("DeviceName{0}_Loc{1}x{2}_Res{3}x{4}", entry.Value.DeviceName, entry.Value.Left, entry.Value.Top, entry.Value.ScreenWidth, entry.Value.ScreenHeight));
            }
            key = string.Join(",", keySegments);
        }

        private string key;
        public string Key
        {
            get
            {
                return key;
            }
        }

        public override bool Equals(object obj)
        {
            var other = obj as DesktopDisplayMetrics;
            if (other == null)
            {
                return false;
            }
            return this.Key == other.key;
        }

        public override int GetHashCode()
        {
            return key.GetHashCode();
        }

        public int GetHashCode(DesktopDisplayMetrics obj)
        {
            return obj.key.GetHashCode();
        }
    }
}
