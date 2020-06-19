using System.Collections.Generic;
using System.Linq;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.Common.Models
{
    public class DesktopDisplayMetrics
    {
        private Dictionary<int, Display> monitorResolutions = new Dictionary<int, Display>();

        public void AcquireMetrics()
        {
            var displays = Display.GetDisplays();

            displays.Sort(delegate (Display dp1, Display dp2)
                {
                    if (dp1.Left != dp2.Left)
                    {
                        return dp1.Left.CompareTo(dp2.Left);
                    }

                    if (dp1.Top != dp2.Top)
                    {
                        return dp1.Top.CompareTo(dp2.Top);
                    }

                    return 0;
                }
            );

            int displayId = 0;
            foreach (var display in displays)
            {
                SetMonitor(displayId++, display);
            }
        }

        public void SetMonitor(int id, Display display)
        {
            monitorResolutions.Add(id, display);
        }

        private string BuildKey()
        {
            List<string> keySegments = new List<string>();
            foreach (var entry in monitorResolutions)
            {
                keySegments.Add(string.Format("{0}_Loc{1}x{2}_Res{3}x{4}", entry.Value.DeviceName, entry.Value.Left, entry.Value.Top, entry.Value.ScreenWidth, entry.Value.ScreenHeight));
            }

            string key = string.Join("__", keySegments);

            key = key.Replace('-', 'M'); //liteDb does not accept minus char
            return key;
        }

        public string Key
        {
            get
            {
                return BuildKey();
            }
        }

    }
}
