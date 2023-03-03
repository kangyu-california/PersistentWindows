using System.Runtime.InteropServices;

namespace PersistentWindows.Common.WinApiBridge
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MonitorInfo
    {
        // size of a device name string
        private const int CCHDEVICENAME = 32;

        public int StructureSize;
        public RECT Monitor;
        public RECT WorkArea;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCHDEVICENAME)]
        public string DeviceName;
    }
}
