using System;
using System.Runtime.InteropServices;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.Common
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
    internal interface IVirtualDesktopManager
    {
        [PreserveSig]
        int IsWindowOnCurrentVirtualDesktop([In] IntPtr TopLevelWindow, [Out] out int OnCurrentDesktop);

        [PreserveSig]
        int GetWindowDesktopId([In] IntPtr TopLevelWindow, [Out] out Guid CurrentDesktop);

        [PreserveSig]
        int MoveWindowToDesktop([In] IntPtr TopLevelWindow, [MarshalAs(UnmanagedType.LPStruct)][In] Guid CurrentDesktop);
    }

    [ComImport, Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
    internal class CVirtualDesktopManager
    {
    }

    public class VirtualDesktop
    {
        private IVirtualDesktopManager _manager = null;

        public VirtualDesktop()
        {
            try
            {
                _manager = (IVirtualDesktopManager)new CVirtualDesktopManager();
            }
            catch
            {
                Log.Error("VirtualDesktop feature not supported by OS");
            }
        }

        public bool Enabled()
        {
            return _manager != null;
        }

        public bool IsWindowOnCurrentVirtualDesktop(IntPtr TopLevelWindow)
        {
            int hr = _manager.IsWindowOnCurrentVirtualDesktop(TopLevelWindow, out int result);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                Log.Error("IsWindowOnCurrentVirtualDesktop() call failed");
            }

            return result != 0;
        }

        public Guid GetWindowDesktopId(IntPtr TopLevelWindow)
        {
            int hr = _manager.GetWindowDesktopId(TopLevelWindow, out Guid result);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                //Log.Error("GetWindowDesktopId() call failed");
            }

            return result;
        }

        public void MoveWindowToDesktop(IntPtr TopLevelWindow, Guid CurrentDesktop)
        {
            int hr = _manager.MoveWindowToDesktop(TopLevelWindow, CurrentDesktop);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                Log.Error("MoveWindowToDesktop() call failed");
            }
        }
    }
}
