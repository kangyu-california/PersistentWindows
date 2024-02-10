using System;
using System.Text;
using System.Runtime.InteropServices;

namespace PersistentWindows.Common.WinApiBridge
{
    /**
     * This is a subset of events from winuser.h.
     * See: https://docs.microsoft.com/en-us/windows/win32/winauto/event-constants
     */
    public enum User32Events : uint
    {
        EVENT_MIN = 0x00000001,
        EVENT_MAX = 0x7FFFFFFF,
        EVENT_SYSTEM_FOREGROUND = 0x0003,
        EVENT_SYSTEM_MENUSTART = 0x0004,
        EVENT_SYSTEM_MENUEND = 0x0005,
        EVENT_SYSTEM_MENUPOPUPSTART = 0x0006,
        EVENT_SYSTEM_MENUPOPUPEND = 0x0007,
        EVENT_SYSTEM_CAPTURESTART = 0x0008,
        EVENT_SYSTEM_CAPTUREEND = 0x0009,
        EVENT_SYSTEM_MOVESIZESTART = 0x000A,
        EVENT_SYSTEM_MOVESIZEEND = 0x000B,
        EVENT_SYSTEM_CONTEXTHELPSTART = 0x000C,
        EVENT_SYSTEM_CONTEXTHELPEND = 0x000D,
        EVENT_SYSTEM_DRAGDROPSTART = 0x000E,
        EVENT_SYSTEM_DRAGDROPEND = 0x000F,
        EVENT_SYSTEM_DIALOGSTART = 0x0010,
        EVENT_SYSTEM_DIALOGEND = 0x0011,
        EVENT_SYSTEM_SCROLLINGSTART = 0x0012,
        EVENT_SYSTEM_SCROLLINGEND = 0x0013,
        EVENT_SYSTEM_SWITCHSTART = 0x0014,
        EVENT_SYSTEM_SWITCHEND = 0x0015,
        EVENT_SYSTEM_MINIMIZESTART = 0x0016,
        EVENT_SYSTEM_MINIMIZEEND = 0x0017,
        EVENT_SYSTEM_DESKTOPSWITCH = 0x0020,
        EVENT_SYSTEM_SWITCHER_APPGRABBED = 0x0024,
        EVENT_SYSTEM_SWITCHER_APPOVERTARGET = 0x0025,
        EVENT_SYSTEM_SWITCHER_APPDROPPED = 0x0026,
        EVENT_SYSTEM_SWITCHER_CANCELLED = 0x0027,
        EVENT_SYSTEM_IME_KEY_NOTIFICATION = 0x0029,
        EVENT_SYSTEM_END = 0x00FF,

        EVENT_OBJECT_DESTROY = 0x8001,
        EVENT_OBJECT_REORDER = 0x8004,
        EVENT_OBJECT_LOCATIONCHANGE = 0x800B,
        EVENT_OBJECT_NAMECHANGE = 0x800C,

        WINEVENT_OUTOFCONTEXT = 0x0000,
        WINEVENT_SKIPOWNTHREAD = 0x0001,
        WINEVENT_SKIPOWNPROCESS = 0x0002,
        WINEVENT_INCONTEXT = 0x0004
    }

    public enum MouseAction : uint
    {
        MOUSEEVENTF_ABSOLUTE = 0x8000,

        MOUSEEVENTF_LEFTDOWN = 0x0002,
        MOUSEEVENTF_LEFTUP = 0x0004,
        MOUSEEVENTF_RIGHTDOWN = 0x0008,
        MOUSEEVENTF_RIGHTUP = 0x0010,

        MOUSEEVENTF_MOVE = 0x0001,
    }

    //copied from ManagedWinapi
    public enum WindowStyleFlags
    {
        //
        // Summary:
        //     WS_POPUP
        POPUP = int.MinValue,
        //
        // Summary:
        //     WS_POPUPWINDOW
        POPUPWINDOW = -2138570752,
        //
        // Summary:
        //     WS_OVERLAPPED
        OVERLAPPED = 0,
        //
        // Summary:
        //     WS_TILED
        TILED = 0,
        //
        // Summary:
        //     WS_TABSTOP
        TABSTOP = 65536,
        //
        // Summary:
        //     WS_MAXIMIZEBOX
        MAXIMIZEBOX = 65536,
        //
        // Summary:
        //     WS_GROUP
        GROUP = 131072,
        //
        // Summary:
        //     WS_MINIMIZEBOX
        MINIMIZEBOX = 131072,
        //
        // Summary:
        //     WS_THICKFRAME
        THICKFRAME = 262144,
        //
        // Summary:
        //     WS_SIZEBOX
        SIZEBOX = 262144,
        //
        // Summary:
        //     WS_SYSMENU
        SYSMENU = 524288,
        //
        // Summary:
        //     WS_HSCROLL
        HSCROLL = 1048576,
        //
        // Summary:
        //     WS_VSCROLL
        VSCROLL = 2097152,
        //
        // Summary:
        //     WS_DLGFRAME
        DLGFRAME = 4194304,
        //
        // Summary:
        //     WS_BORDER
        BORDER = 8388608,
        //
        // Summary:
        //     WS_CAPTION
        CAPTION = 12582912,
        //
        // Summary:
        //     WS_TILEDWINDOW
        TILEDWINDOW = 13565952,
        //
        // Summary:
        //     WS_OVERLAPPEDWINDOW
        OVERLAPPEDWINDOW = 13565952,
        //
        // Summary:
        //     WS_MAXIMIZE
        MAXIMIZE = 16777216,
        //
        // Summary:
        //     WS_CLIPCHILDREN
        CLIPCHILDREN = 33554432,
        //
        // Summary:
        //     WS_CLIPSIBLINGS
        CLIPSIBLINGS = 67108864,
        //
        // Summary:
        //     WS_DISABLED
        DISABLED = 134217728,
        //
        // Summary:
        //     WS_VISIBLE
        VISIBLE = 268435456,
        //
        // Summary:
        //     WS_MINIMIZE
        MINIMIZE = 536870912,
        //
        // Summary:
        //     WS_ICONIC
        ICONIC = 536870912,
        //
        // Summary:
        //     WS_CHILD
        CHILD = 1073741824,
        //
        // Summary:
        //     WS_CHILDWINDOW
        CHILDWINDOW = 1073741824
    }

    public class User32
    {
        #region delegates
        public delegate IntPtr MouseHookHandler(int nCode, uint wParam, IntPtr lParam);
        public delegate bool MonitorEnumDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        public delegate void WinEventDelegate(IntPtr hWinEventHook,
            User32Events eventType,
            IntPtr hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime);
        #endregion

        [DllImport("user32.dll")]
        public static extern IntPtr SetWinEventHook(
            User32Events eventMin,
            User32Events eventMax,
            IntPtr hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess,
            uint idThread,
            uint dwFlags);

        [DllImport("user32.dll")]
        public static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumDelegate lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

        [DllImport("user32.dll", SetLastError = true)]
        //public static extern IntPtr MonitorFromPoint(POINT pt, MonitorOptions dwFlags);
        public static extern IntPtr MonitorFromPoint(POINT pt, int dwFlags);
        public const int MONITOR_DEFAULTTONULL = 0;
        public const int MONITOR_DEFAULTTOPRIMARY = 1;
        public const int MONITOR_DEFAULTTONEAREST = 2;

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr WindowFromPoint(POINT pt);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName,int nMaxCount);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr hWnd, ref RECT lpRect);

        [DllImport("user32.dll")]
        public static extern bool GetClientRect(IntPtr hWnd, ref RECT lpRect);
        
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IntersectRect([Out] out RECT lprcDst, [In] ref RECT lprcSrc1, [In] ref RECT lprcSrc2);

        [DllImport("user32.dll")]
        public static extern bool PtInRect([In] ref RECT lprc, POINT pt);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsHungAppWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.SysInt)]
        public static extern IntPtr GetDesktopWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr GetTopWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.I4)]
        public static extern int MapWindowPoints(IntPtr from, IntPtr to, ref POINT points, uint num);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPlacement(IntPtr hWnd, [In] ref WindowPlacement lpwndpl);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy,
            SetWindowPosFlags uFlags);
        /// <summary>

        /// Retrieves a handle to a window that has the specified relationship (Z-Order or owner) to the specified window.
        /// </summary>
        /// <remarks>The EnumChildWindows function is more reliable than calling GetWindow in a loop. An application that
        /// calls GetWindow to perform this task risks being caught in an infinite loop or referencing a handle to a window
        /// that has been destroyed.</remarks>
        /// <param name="hWnd">A handle to a window. The window handle retrieved is relative to this window, based on the
        /// value of the uCmd parameter.</param>
        /// <param name="uCmd">The relationship between the specified window and the window whose handle is to be
        /// retrieved.</param>
        /// <returns>
        /// If the function succeeds, the return value is a window handle. If no window exists with the specified relationship
        /// to the specified window, the return value is NULL. To get extended error information, call GetLastError.
        /// </returns>
        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern IntPtr BeginDeferWindowPos(int nNumWindows);

        [DllImport("user32.dll")]
        public static extern IntPtr DeferWindowPos(IntPtr hWinPosInfo, IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy,
            DeferWindowPosCommands uFlags);

        public enum DeferWindowPosCommands : uint
        {
            SWP_DRAWFRAME = 0x0020,
            SWP_FRAMECHANGED = 0x0020,
            SWP_HIDEWINDOW = 0x0080,
            SWP_NOACTIVATE = 0x0010,
            SWP_NOCOPYBITS = 0x0100,
            SWP_NOMOVE = 0x0002,
            SWP_NOOWNERZORDER = 0x0200,
            SWP_NOREDRAW = 0x0008,
            SWP_NOREPOSITION = 0x0200,
            SWP_NOSENDCHANGING = 0x0400,
            SWP_NOSIZE = 0x0001,
            SWP_NOZORDER = 0x0004,
            SWP_SHOWWINDOW = 0x0040
        };

        [DllImport("user32.dll")]
        public static extern bool EndDeferWindowPos(IntPtr hWinPosInfo);

        public enum RedrawWindowFlags : uint
        {
            /// <summary>
            /// Invalidates the rectangle or region that you specify in lprcUpdate or hrgnUpdate.
            /// You can set only one of these parameters to a non-NULL value. If both are NULL, RDW_INVALIDATE invalidates the entire window.
            /// </summary>
            Invalidate = 0x1,

            /// <summary>Causes the OS to post a WM_PAINT message to the window regardless of whether a portion of the window is invalid.</summary>
            InternalPaint = 0x2,

            /// <summary>
            /// Causes the window to receive a WM_ERASEBKGND message when the window is repainted.
            /// Specify this value in combination with the RDW_INVALIDATE value; otherwise, RDW_ERASE has no effect.
            /// </summary>
            Erase = 0x4,

            /// <summary>
            /// Validates the rectangle or region that you specify in lprcUpdate or hrgnUpdate.
            /// You can set only one of these parameters to a non-NULL value. If both are NULL, RDW_VALIDATE validates the entire window.
            /// This value does not affect internal WM_PAINT messages.
            /// </summary>
            Validate = 0x8,

            NoInternalPaint = 0x10,

            /// <summary>Suppresses any pending WM_ERASEBKGND messages.</summary>
            NoErase = 0x20,

            /// <summary>Excludes child windows, if any, from the repainting operation.</summary>
            NoChildren = 0x40,

            /// <summary>Includes child windows, if any, in the repainting operation.</summary>
            AllChildren = 0x80,

            /// <summary>Causes the affected windows, which you specify by setting the RDW_ALLCHILDREN and RDW_NOCHILDREN values, to receive WM_ERASEBKGND and WM_PAINT messages before the RedrawWindow returns, if necessary.</summary>
            UpdateNow = 0x100,

            /// <summary>
            /// Causes the affected windows, which you specify by setting the RDW_ALLCHILDREN and RDW_NOCHILDREN values, to receive WM_ERASEBKGND messages before RedrawWindow returns, if necessary.
            /// The affected windows receive WM_PAINT messages at the ordinary time.
            /// </summary>
            EraseNow = 0x200,

            Frame = 0x400,

            NoFrame = 0x800
        }
        [DllImport("user32.dll")]
        public static extern bool RedrawWindow(IntPtr hWnd,
            IntPtr lprcUpdate, //[In] ref RECT lprcUpdate, 
            IntPtr hrgnUpdate, RedrawWindowFlags flags);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsZoomed(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindow(IntPtr hWnd, int cmd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ShowWindowAsync(IntPtr hWnd, int cmd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UpdateWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsTopLevelWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowA(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll")]
        public static extern void mouse_event(MouseAction dwFlags, int dx, int dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetCursorPos(out POINT lpPoint);
        // DON'T use System.Drawing.Point, the order of the fields in System.Drawing.Point isn't guaranteed to stay the same.

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetCursorPos(int x, int y);

        [DllImport("USER32.dll")]
        public static extern short GetKeyState(int nVirtKey);

        [DllImport("USER32.dll")]
        public static extern short GetAsyncKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern long GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern int SetWindowLong(IntPtr hWnd, int nIndex, uint dwNewLong);

        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOPMOST = 0x00000008L;

        public const int GWL_STYLE = -16;
        public const long WS_CAPTION = 0x00C00000L;

        [DllImport("user32.dll", EntryPoint = "GetClassLongPtr")]
        public static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll", EntryPoint = "GetClassLong")]
        public static extern uint GetClassLongPtr32(IntPtr hWnd, int nIndex);
        public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex) => IntPtr.Size > 4 ? GetClassLongPtr64(hWnd, nIndex) : new IntPtr(GetClassLongPtr32(hWnd, nIndex));

        [DllImport("user32.dll")]
        public static extern IntPtr LoadIcon(IntPtr hInstance, string lpIconName);

        [DllImport("user32.dll")]
        public static extern IntPtr GetAncestor(IntPtr hWnd, uint gaFlags); // I'm too lazy to write an enum for them
        public const int GetRoot = 2;
        public const int GetRootOwner = 3;

        public const int ICON_SMALL = 0;
        public const int ICON_BIG = 1;
        public const int ICON_SMALL2 = 2;
        public const int GCLP_HICON = -14;
        public const int GCLP_HICONSM = -34;
        public const string IDI_APPLICATION = "#32512";

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, StringBuilder lParam);
        public const int WM_COMMAND = 0x0111;
        public const int WM_SYSCOMMAND = 0x0112;
        public const int WM_GETICON = 0x7F;
        public const int SC_MINIMIZE = 0xF020;
        public const int SC_TOGGLE_TASKBAR_LOCK = 424;
        [DllImport("user32.dll")]
        public static extern int SendMessageTimeout(IntPtr handle, int uMsg, uint wParam, uint lParam, uint fuFlags, int uTimeout, out uint lpdwResult);
        // SendMessageTimeoutFlags
        public const uint SMTO_NORMAL = 0x0000;
        public const uint SMTO_BLOCK = 0x0001;
        public const uint SMTO_ABORTIFHUNG = 0x0002;
        public const uint SMTO_NOTIMEOUTIFNOTHUNG = 0x0008;

        [DllImport("user32.dll")]
        public static extern bool IsWindowOnCurrentVirtualDesktop(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern uint GetDpiForWindow(IntPtr hWnd);

        public static bool DpiSenstiveCall = false;
        [DllImport("user32.dll")]
        public static extern bool SetProcessDpiAwarenessContext(int dpi_awareness_cxt);
        [DllImport("user32.dll")]
        public static extern int SetThreadDpiAwarenessContext(int dpi_awareness_cxt);
        public const int DPI_AWARENESS_CONTEXT_UNAWARE = -1;
        public const int DPI_AWARENESS_CONTEXT_SYSTEM_AWARE = -2;
        public const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE = -3;
        public const int DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4;
        public const int DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED = -5;
        public static int SetThreadDpiAwarenessContextSafe(int dpi_awareness_cxt = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)
        {
            var os_version = Environment.OSVersion;
            if (os_version.Version.Major < 10)
                return 0;

            /*
            // windows 11 workaround for #289
            if (os_version.Version.Build > 22000)
                if (dpi_awareness_cxt == DPI_AWARENESS_CONTEXT_UNAWARE)
                    return 0;

            if (dpi_awareness_cxt == DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2)
                return 0;
            if (dpi_awareness_cxt == DPI_AWARENESS_CONTEXT_UNAWARE)
                //dpi_awareness_cxt = DPI_AWARENESS_CONTEXT_SYSTEM_AWARE;
                //dpi_awareness_cxt = DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED;
                dpi_awareness_cxt = DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE;
            */
            if (!DpiSenstiveCall)
                return 0;

            //valid API since win10 1607
            return SetThreadDpiAwarenessContext(dpi_awareness_cxt);
        }

        public const int WH_MOUSE = 7;
        public const int WH_MOUSE_LL = 14;

        public enum MouseMessages
        {
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_MOUSEMOVE = 0x0200,
            WM_MOUSEWHEEL = 0x020A,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEHOOKSTRUCT
        {
            public POINT pt;
            public IntPtr hwnd;
            public uint wHitTestCode;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(int idHook,
            MouseHookHandler lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, uint wParam, IntPtr lParam);

    }

    public class Kernel32
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool QueryFullProcessImageName([In]IntPtr hProcess, [In]int dwFlags, [Out]StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(
            ProcessAccessFlags processAccess,
            bool bInheritHandle,
            uint processId
        );
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x000000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
        }
    }

    public class Shell32
    {
        [DllImport("Shell32.dll", CharSet = CharSet.Auto)]
        public static extern UIntPtr SHAppBarMessage(int dwMessage, ref APP_BAR_DATA abd);

        public const int ABM_NEW = 0x00;
        public const int ABM_REMOVE = 0x01;
        public const int ABM_QUERYPOS = 0x02;
        public const int ABM_SETPOS = 0x03;
        public const int ABM_GETTASKBARPOS = 0x05;
        public const int ABM_SETAUTOHIDEBAR = 0x08;
        public const int ABM_SETSTATE = 0x0000000a;
        public const int ABE_LEFT = 0;
        public const int ABE_TOP = 1;
        public const int ABE_RIGHT = 2;
        public const int ABE_BOTTOM = 3;
        public const int ABS_AUTOHIDE = 0x01;
        public const int ABS_ALWAYSONTOP = 0x02;

        [StructLayout(LayoutKind.Sequential)]
        public struct APP_BAR_DATA
        {
            public uint cbSize;
            public IntPtr hWnd;
            public int uCallbackMessage;
            public int uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        public enum QUERY_USER_NOTIFICATION_STATE
        {
            QUNS_NOT_PRESENT = 1,
            QUNS_BUSY = 2,
            QUNS_RUNNING_D3D_FULL_SCREEN = 3,
            QUNS_PRESENTATION_MODE = 4,
            QUNS_ACCEPTS_NOTIFICATIONS = 5,
            QUNS_QUIET_TIME = 6
        };

        [DllImport("shell32.dll")]
        public static extern int SHQueryUserNotificationState(
             out QUERY_USER_NOTIFICATION_STATE pquns);
    }


}
