using System.Collections.Generic;
using System.Linq;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common.Models
{
    public class DesktopDisplayMetrics
    {
        private List<Display> monitorResolutions = new List<Display>();

        public void AcquireMetrics()
        {
            var displays = Display.GetDisplays();

            displays.Sort(delegate (Display dp1, Display dp2)
                {
                    if (dp1.Position.Left != dp2.Position.Left)
                    {
                        return dp1.Position.Left.CompareTo(dp2.Position.Left);
                    }

                    if (dp1.Position.Top != dp2.Position.Top)
                    {
                        return dp1.Position.Top.CompareTo(dp2.Position.Top);
                    }

                    if (dp1.Position.Width != dp2.Position.Width)
                        return dp1.Position.Width.CompareTo(dp2.Position.Width);

                    if (dp1.Position.Height != dp2.Position.Height)
                        return dp1.Position.Height.CompareTo(dp2.Position.Height);

                    return 0;
                }
            );

            foreach (var display in displays)
            {
                monitorResolutions.Add(display);
            }
        }

        public List<Display> GetDisplays()
        {
            AcquireMetrics();
            return monitorResolutions;
        }

        private string BuildKey()
        {
            List<string> keySegments = new List<string>();
            foreach (var entry in monitorResolutions)
            {
                keySegments.Add(string.Format("{0}_Loc{1}x{2}_Res{3}x{4}", entry.DeviceName, entry.Position.Left, entry.Position.Top, entry.Position.Width, entry.Position.Height));
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
