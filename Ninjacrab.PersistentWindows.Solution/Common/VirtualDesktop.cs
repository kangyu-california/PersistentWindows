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
        private static IVirtualDesktopManager _static_manager = null;

        public VirtualDesktop()
        {
            try
            {
                _static_manager = (IVirtualDesktopManager)new CVirtualDesktopManager();
            }
            catch
            {
                Log.Error("VirtualDesktop feature not supported by OS");
            }
        }

        public static bool Enabled()
        {
            return _static_manager != null;
        }

        public static bool IsWindowOnCurrentVirtualDesktop(IntPtr TopLevelWindow)
        {
            if (!Enabled())
                return true;

            int result = 1;
            int hr = _static_manager.IsWindowOnCurrentVirtualDesktop(TopLevelWindow, out result);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                Log.Error("IsWindowOnCurrentVirtualDesktop() call failed");
            }

            return result != 0;
        }

        public static Guid GetWindowDesktopId(IntPtr TopLevelWindow)
        {
            if (!Enabled())
                return Guid.Empty;

            int hr = _static_manager.GetWindowDesktopId(TopLevelWindow, out Guid result);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                //Log.Error("GetWindowDesktopId() call failed");
            }

            return result;
        }

        public static void MoveWindowToDesktop(IntPtr TopLevelWindow, Guid CurrentDesktop)
        {
            if (_static_manager == null)
                return;

            int hr = _static_manager.MoveWindowToDesktop(TopLevelWindow, CurrentDesktop);
            if (hr != 0)
            {
                //Marshal.ThrowExceptionForHR(hr);
                Log.Error("MoveWindowToDesktop() call failed");
            }
        }
    }
}
