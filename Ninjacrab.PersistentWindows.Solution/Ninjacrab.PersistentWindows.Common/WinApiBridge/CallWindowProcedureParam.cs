using System;
using System.Runtime.InteropServices;

namespace Ninjacrab.PersistentWindows.Common.WinApiBridge
{
    [StructLayout(LayoutKind.Sequential)]
    public struct CallWindowProcedureParam
    {
        public IntPtr lparam;
        public IntPtr wparam;
        public WindowsMessage message;
        public IntPtr hwnd;
    }
}
