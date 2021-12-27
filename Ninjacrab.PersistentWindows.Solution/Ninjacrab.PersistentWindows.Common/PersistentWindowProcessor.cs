using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Diagnostics;
using System.Reflection;
using System.Drawing;
using Microsoft.Win32;

using LiteDB;

using Ninjacrab.PersistentWindows.Common.Diagnostics;
using Ninjacrab.PersistentWindows.Common.Models;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.Common
{
    public class PersistentWindowProcessor : IDisposable
    {
        // constant
        private const int RestoreLatency = 500; // default delay in milliseconds from display change to window restore
        private const int SlowRestoreLatency = 1000; // delay in milliseconds from power resume to window restore
        private const int MaxRestoreLatency = 2000; // max delay in milliseconds from final restore pass to restore finish
        private const int MinRestoreTimes = 2; // minimum restore passes
        private const int MaxRestoreTimes = 4; // maximum restore passes

        private const int CaptureLatency = 3000; // delay in milliseconds from window OS move to capture
        private const int UserMoveLatency = 1000; // delay in milliseconds from user move/minimize/unminimize/maximize to capture, must < CaptureLatency
        private const int MaxUserMoves = 4; // max user window moves per capture cycle
        private const int MinWindowOsMoveEvents = 12; // threshold of window move events initiated by OS per capture cycle
        private const int MaxSnapshots = 37; // 0-9, a-z, and final one for undo
        private const int MaxHistoryQueueLength = 40; // must be bigger than MaxSnapshots + 1

        private const int PauseRestoreTaskbar = 3500; //cursor idle time before dragging taskbar

        private bool initialized = false;

        // window position database
        private Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>> monitorApplications
            = new Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>(); //in-memory database
        private string persistDbName = null; //on-disk database name
        private Dictionary<string, POINT> lastCursorPos = new Dictionary<string, POINT>();
        private Dictionary<string, List<DeadAppPosition>> deadApps = new Dictionary<string, List<DeadAppPosition>>();
        private HashSet<IntPtr> allUserMoveWindows = new HashSet<IntPtr>();
        private IntPtr desktopWindow = User32.GetDesktopWindow();

        // windows that are not to be restored
        private HashSet<IntPtr> noRestoreWindows = new HashSet<IntPtr>(); //windows excluded from auto-restore
        private HashSet<IntPtr> noRestoreWindowsTmp = new HashSet<IntPtr>(); //user moved windows during restore

        // realtime fixing window location
        private IntPtr curMovingWnd = IntPtr.Zero;
        private Timer moveTimer; // when user move a window
        private Timer foregroundTimer; // when user bring a window to foreground
        private DateTime lastDisplayChangeTime = DateTime.Now;

        // control shared by capture and restore
        private LiteDatabase singletonLock; //prevent second PW inst from running

        // capture control
        private Timer captureTimer;
        public string curDisplayKey = null; // current display config name
        public string dbDisplayKey = null;
        private Dictionary<IntPtr, string> windowTitle = new Dictionary<IntPtr, string>(); // for matching running window with DB record
        private Queue<IntPtr> pendingMoveEvents = new Queue<IntPtr>(); // queue of window with possible position change for capture
        private HashSet<IntPtr> pendingActivateWindows = new HashSet<IntPtr>();
        private HashSet<string> normalSessions = new HashSet<string>(); //normal user sessions, for differentiating full screen game session or other transient session
        private bool userMove = false; //received window event due to user move
        private bool userMovePrev = false; //prev value of userMove
        private HashSet<IntPtr> tidyTabWindows = new HashSet<IntPtr>(); //tabbed windows bundled by tidytab
        private DateTime lastUnminimizeTime = DateTime.Now;
        private IntPtr lastUnminimizeWindow = IntPtr.Zero;
        private Dictionary<string, IntPtr> foreGroundWindow = new Dictionary<string, IntPtr>();
        public Dictionary<uint, string> processCmd = new Dictionary<uint, string>();

        // restore control
        private Timer restoreTimer;
        private Timer restoreFinishedTimer;
        public bool restoringFromMem = false; // automatic restore from memory in progress
        public bool restoringFromDB = false; // manual restore from DB in progress
        public bool restoringSnapshot = false;
        public bool dryRun = false; // only capturre, no actual restore
        public bool showDesktop = false; // show desktop when display changes
        public int fixZorder = 1; // 1 means restore z-order only for snapshot; 2 means restore z-order for all; 0 means no z-order restore at all
        public int fixZorderMethod = 9; // bit i represent restore method for pass i
        public bool pauseAutoRestore = false;
        public bool promptSessionRestore = false;
        public bool redrawDesktop = false;
        public bool enableOffScreenFix = true;
        public bool enhancedOffScreenFix = false;
        public bool fixUnminimizedWindow = true;
        public bool autoRestoreMissingWindows = false;
        public bool launchOncePerProcessId = true;
        private int restoreTimes = 0; //multiple passes need to fully restore
        private Object restoreLock = new object();
        private bool restoreHalted = false;
        public int haltRestore = 3; //seconds to wait to finish current halted restore and restart next one
        private HashSet<IntPtr> restoredWindows = new HashSet<IntPtr>();
        private HashSet<IntPtr> topmostWindowsFixed = new HashSet<IntPtr>();

        private Dictionary<string, string> realProcessFileName = new Dictionary<string, string>()
            {
                { "WindowsTerminal.exe", "wt.exe"},
            };

        private HashSet<string> ignoreProcess = new HashSet<string>();

        private string appDataFolder;
        public bool redirectAppDataFolder = false;

        // session control
        private bool sessionLocked = false; //requires password to unlock
        public bool sessionActive = true;
        private bool remoteSession = false;

        // restore time
        private Dictionary<string, DateTime> lastUserActionTime = new Dictionary<string, DateTime>();
        private Dictionary<string, DateTime> lastUserActionTimeBackup = new Dictionary<string, DateTime>();
        private Dictionary<string, Dictionary<int, DateTime>> snapshotTakenTime = new Dictionary<string, Dictionary<int, DateTime>>();
        public int snapshotId;

        private bool iconBusy = false;
        private bool taskbarReady = false;

        // callbacks
        public delegate void CallBack();
        public CallBack showRestoreTip;
        public CallBack hideRestoreTip;

        public delegate void CallBackBool(bool en);
        public CallBackBool enableRestoreMenu;
        public CallBackBool enableRestoreSnapshotMenu;

        private PowerModeChangedEventHandler powerModeChangedHandler;
        private EventHandler displaySettingsChangingHandler;
        private EventHandler displaySettingsChangedHandler;
        private SessionSwitchEventHandler sessionSwitchEventHandler;

        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private User32.WinEventDelegate winEventsCaptureDelegate;

        public System.Drawing.Icon icon = null;

        // running thread
        private HashSet<Thread> runningThreads = new HashSet<Thread>();

#if DEBUG
        private void DebugInterval()
        {
            ;
        }
#endif
        public bool Start(bool auto_restore_from_db = false)
        {
            string productName = System.Windows.Forms.Application.ProductName;
            appDataFolder = redirectAppDataFolder ? "." :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), productName);

#if DEBUG
            //avoid db path conflict with release version
            //appDataFolder = ".";
            appDataFolder = AppDomain.CurrentDomain.BaseDirectory;
#endif
            var dir = Directory.CreateDirectory(appDataFolder);

            try
            {
                string singletonLockName = $@"{appDataFolder}/{productName}.db.lock";
                singletonLock = new LiteDatabase(singletonLockName);
            }
            catch (Exception)
            {
                System.Windows.Forms.MessageBox.Show("Another instance is already running.", $"{productName}",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Exclamation);
                return false;
            }

            var db_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            persistDbName = $@"{appDataFolder}/{productName}.{db_version}.db";
            bool found_latest_db_file_version = false;
            if (File.Exists(persistDbName))
                found_latest_db_file_version = true;
            foreach (var file in dir.EnumerateFiles($@"{productName}*.db"))
            {
                var fname = file.Name;
                if (found_latest_db_file_version && !fname.Contains(db_version))
                {
                    // remove outdated db files
                    /*
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                    */
                }
                else if (!found_latest_db_file_version)
                {
                    //load outdated db
                    persistDbName = file.FullName;
                    break;
                }
            }

            curDisplayKey = GetDisplayKey();
            CaptureNewDisplayConfig(curDisplayKey);

#if DEBUG
            //TestSetWindowPos();

            var debugTimer = new Timer(state =>
            {
                DebugInterval();
            });
            debugTimer.Change(2000, 2000);
#endif            

            moveTimer = new Timer(state =>
            {
                if ((User32.GetKeyState(0x11) & 0x8000) != 0 //ctrl key pressed
                    && (User32.GetKeyState(0x10) & 0x8000) != 0) //shift key pressed
                {
                    Log.Event("ignore window {0}", GetWindowTitle(curMovingWnd));
                    noRestoreWindows.Add(curMovingWnd);
                }
                else
                {
                    noRestoreWindows.Remove(curMovingWnd);
                }
            }
            );

            foregroundTimer = new Timer(state =>
            {
                IntPtr hwnd = foreGroundWindow[curDisplayKey];

                if ((User32.GetKeyState(0x11) & 0x8000) != 0) //ctrl key pressed
                {
                    if ((User32.GetKeyState(0x5b) & 0x8000) != 0) //ctrl-left_window key pressed
                    {
                        //put activated window in background
                        var process = GetProcess(hwnd);
                        var processName = process.ProcessName;
                        if (processName.Equals("mstsc") || processName.Contains("vnc") || processName.Contains("rdp"))
                        {
                            User32.SetWindowPos(hwnd, new IntPtr(1), //bottom
                                0, 0, 0, 0,
                                0
                                | SetWindowPosFlags.DoNotActivate
                                | SetWindowPosFlags.IgnoreMove
                                | SetWindowPosFlags.IgnoreResize
                            );
                        }
                    }
                    else
                        ManualFixTopmostFlag(hwnd);
                }
            });

            captureTimer = new Timer(state =>
            {
                userMovePrev = userMove;
                userMove = false;

                if (!sessionActive)
                    return;

                if (restoringFromMem)
                    return;

                PostActivateWindows();

                Log.Trace("Capture timer expired");
                BatchCaptureApplicationsOnCurrentDisplays();
            });

            restoreTimer = new Timer(state => { TimerRestore(); });
            
            restoreFinishedTimer = new Timer(state =>
            {
                int numWindowRestored = restoredWindows.Count;
                int restorePass = restoreTimes;

                restoringFromDB = false;
                restoringFromMem = false;
                bool wasRestoringSnapshot = restoringSnapshot;
                restoringSnapshot = false;
                ResetState();
                Log.Trace("");
                Log.Trace("");
                string displayKey = GetDisplayKey();
                if (restoreHalted || !displayKey.Equals(curDisplayKey))
                {
                    restoreHalted = false;
                    topmostWindowsFixed.Clear();

                    Log.Error("Restore aborted for {0}", curDisplayKey);

                    // do restore again, while keeping previous capture time unchanged
                    curDisplayKey = displayKey;
                    if (normalSessions.Contains(curDisplayKey))
                    {
                        Log.Event("Restart restore for {0}", curDisplayKey);
                        restoringFromMem = true;
                        StartRestoreTimer();
                    }
                    else
                    {
                        Log.Event("no need to restore fresh session {0}", curDisplayKey);

                        //restore icon to idle
                        hideRestoreTip();
                        iconBusy = false;
                        sessionActive = true;
                        using (var persistDB = new LiteDatabase(persistDbName))
                        {
                            enableRestoreMenu(persistDB.CollectionExists(curDisplayKey));
                        }
                        enableRestoreSnapshotMenu(snapshotTakenTime.ContainsKey(curDisplayKey));
                    }
                }
                else
                {
                    BatchFixTopMostWindows();

                    if (redrawDesktop)
                        User32.RedrawWindow(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, User32.RedrawWindowFlags.Invalidate);

                    hideRestoreTip();
                    iconBusy = false;

                    Log.Event("Restore finished in pass {0} with {1} windows recovered for display setting {2}", restorePass, numWindowRestored, curDisplayKey);
                    sessionActive = true;
                    using (var persistDB = new LiteDatabase(persistDbName))
                    {
                        enableRestoreMenu(persistDB.CollectionExists(curDisplayKey));
                    }
                    enableRestoreSnapshotMenu(snapshotTakenTime.ContainsKey(curDisplayKey));

                    if (wasRestoringSnapshot || noRestoreWindowsTmp.Count > 0)
                        CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture: true);
                }

                noRestoreWindowsTmp.Clear();

            });


            winEventsCaptureDelegate = WinEventProc;

            // captures new window, user click, snap and minimize
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_SYSTEM_FOREGROUND,
                User32Events.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // captures user dragging
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_SYSTEM_MOVESIZESTART,
                User32Events.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // captures user restore window
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_SYSTEM_MINIMIZESTART,
                User32Events.EVENT_SYSTEM_MINIMIZEEND, //unminimize window
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // capture both system and user move action
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_OBJECT_LOCATIONCHANGE,
                User32Events.EVENT_OBJECT_LOCATIONCHANGE,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // capture window close
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_OBJECT_DESTROY,
                User32Events.EVENT_OBJECT_DESTROY,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            this.displaySettingsChangingHandler =
                (s, e) =>
                {
                    string displayKey = GetDisplayKey();
                    Log.Trace("");
                    Log.Info("Display settings changing {0}", displayKey);
                    {
                        lastDisplayChangeTime = DateTime.Now;

                        // undo disqualified capture time
                        if (lastUserActionTime.ContainsKey(curDisplayKey))
                        {
                            var lastCaptureTime = lastUserActionTime[curDisplayKey];
                            var diff = lastDisplayChangeTime - lastCaptureTime;
                            if (diff.TotalMilliseconds < 1000)
                            {
                                if (lastUserActionTimeBackup.ContainsKey(curDisplayKey))
                                {
                                    lastUserActionTime[curDisplayKey] = lastUserActionTimeBackup[curDisplayKey];
                                    Log.Error("undo capture of {0} at {1}", curDisplayKey, lastCaptureTime);
                                }
                            }
                        }

                        if (!restoringFromMem)
                        {
                            EndDisplaySession();
                        }
                    }
                };

            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    string displayKey = GetDisplayKey();
                    Log.Event("Display settings changed {0}", displayKey);

                    {
                        EndDisplaySession();

                        if (sessionLocked)
                        {
                            curDisplayKey = displayKey;
                            //wait for session unlock to start restore
                        }
                        else if (restoringFromMem)
                        {
                            if (!displayKey.Equals(curDisplayKey))
                            {
                                restoreHalted = true;
                                Log.Event("Restore halted due to new display setting change {0}", displayKey);
                            }
                        }
                        else
                        {
                            if (showDesktop)
                                ShowDesktop();

                            // change display on the fly
                            curDisplayKey = displayKey;

                            if (normalSessions.Contains(curDisplayKey))
                            {
                                if (promptSessionRestore)
                                {
                                    PromptSessionRestore();
                                }
                                restoringFromMem = true;
                                StartRestoreTimer();
                            }
                            else
                            {
                                Log.Error($"No need to restore {curDisplayKey} display session");
                            }
                        }
                    }
                };

            SystemEvents.DisplaySettingsChanged += this.displaySettingsChangedHandler;

            powerModeChangedHandler =
                (s, e) =>
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            Log.Event("System suspending");
                            {
                                sessionActive = false;
                                if (!sessionLocked)
                                {
                                    EndDisplaySession();
                                }
                            }
                            break;

                        case PowerModes.Resume:
                            Log.Event("System Resuming");
                            {
                                if (!sessionLocked)
                                {
                                    if (promptSessionRestore)
                                    {
                                        PromptSessionRestore();
                                    }
                                    // force restore in case OS does not generate display changed event
                                    restoringFromMem = true;
                                    StartRestoreTimer(milliSecond: SlowRestoreLatency);
                                }
                            }
                            break;
                    }
                };

            SystemEvents.PowerModeChanged += powerModeChangedHandler;

            sessionSwitchEventHandler = (sender, args) =>
            {
                switch (args.Reason)
                {
                    case SessionSwitchReason.SessionLock:
                        Log.Event("Session closing: reason {0}", args.Reason);
                        {
                            sessionLocked = true;
                            sessionActive = false;
                            EndDisplaySession();
                        }
                        break;
                    case SessionSwitchReason.SessionUnlock:
                        Log.Event("Session opening: reason {0}", args.Reason);
                        {
                            sessionLocked = false;
                            if (promptSessionRestore)
                            {
                                PromptSessionRestore();
                            }
                            // force restore in case OS does not generate display changed event
                            restoringFromMem = true;
                            StartRestoreTimer();
                        }
                        break;

                    case SessionSwitchReason.RemoteDisconnect:
                    case SessionSwitchReason.ConsoleDisconnect:
                        sessionActive = false;
                        Log.Trace("Session closing: reason {0}", args.Reason);
                        break;

                    case SessionSwitchReason.RemoteConnect:
                        remoteSession = true;
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        break;
                    case SessionSwitchReason.ConsoleConnect:
                        remoteSession = false;
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        break;
                }
            };

            SystemEvents.SessionSwitch += sessionSwitchEventHandler;

            initialized = true;

            using(var persistDB = new LiteDatabase(persistDbName))
            {
                bool db_exist = persistDB.CollectionExists(curDisplayKey);
                enableRestoreMenu(db_exist);
                if (db_exist && auto_restore_from_db)
                {
                    restoringFromDB = true;
                    StartRestoreTimer();
                }
            }

            return true;
        }

        public void SetIgnoreProcess(string ignore_process)
        {
            string[] ps = ignore_process.Split(';');
            foreach (var p in ps)
            {
                var s = p;
                if (s.EndsWith(".exe"))
                    s = s.Substring(0, s.Length - 4);
                ignoreProcess.Add(s);
            }
        }

        private void PromptSessionRestore()
        {
            if (pauseAutoRestore)
                return;

            sessionActive = false; // no new capture
            pauseAutoRestore = true;

            using (var dlg = new System.Windows.Forms.Form())
            {
                dlg.Size = new Size(300, 200);
                dlg.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
                dlg.TopMost = true;
                dlg.Icon = Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
                dlg.MinimizeBox = false;
                dlg.MaximizeBox = false;
                dlg.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
                dlg.Text = $"{System.Windows.Forms.Application.ProductName}";
                var button1 = new System.Windows.Forms.Button();
                button1.Text = "OK";
                // Set the position of the button on the form.
                button1.Location = new Point(110, 120);

                var label = new System.Windows.Forms.Label();
                label.Size = new Size(250, 50);
                label.Location = new Point(30, 50);
                label.Text = "Press OK to restore window layout";
                label.Font = new System.Drawing.Font(label.Font.Name, 10F);

                dlg.CancelButton = button1;
                dlg.Controls.Add(button1);
                dlg.Controls.Add(label);

                dlg.Activate();

                User32.SetWindowPos(
                    dlg.Handle,
                    new IntPtr(-1), // set dialog to topmost
                    0, //rect.Left,
                    0, //rect.Top,
                    0, //rect.Width,
                    0, //rect.Height,
                    0
                    | SetWindowPosFlags.DoNotActivate
                    | SetWindowPosFlags.IgnoreMove
                    | SetWindowPosFlags.IgnoreResize
                );

                dlg.ShowDialog();
            }

            pauseAutoRestore = false;
        }

        private bool IsFullScreen(IntPtr hwnd)
        {
            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            bool isFullScreen = false;
            if ((style & (long)WindowStyleFlags.MAXIMIZEBOX) == 0L)
            {
                RECT screenPosition = new RECT();
                User32.GetWindowRect(hwnd, ref screenPosition);

                string size = string.Format("Res{0}x{1}", screenPosition.Width, screenPosition.Height);
                if (curDisplayKey.Contains(size))
                    isFullScreen = true;

                if (!isFullScreen)
                {
                    List<Display> displays = GetDisplays();
                    foreach (var display in displays)
                    {
                        RECT screen = display.Position;
                        RECT intersect = new RECT();
                        if (!User32.IntersectRect(out intersect, ref screenPosition, ref screen))
                        {
                            //must intersect with all screens
                            isFullScreen = false;
                            break;
                        }

                        if (intersect.Equals(screen))
                            isFullScreen = true; //fully covers at least one screen
                    }
                }
            }

            return isFullScreen;
        }

        private string GetWindowTitle(IntPtr hwnd, bool use_cache = true)
        {
            if (use_cache && windowTitle.ContainsKey(hwnd))
                return windowTitle[hwnd];

            var length = User32.GetWindowTextLength(hwnd);
            if (length > 0)
            {
                length++;
                var title = new StringBuilder(length);
                User32.GetWindowText(hwnd, title, length);
                return title.ToString();
            }

            //return hwnd.ToString("X8");
            return "";
        }

        private bool IsMinimized(IntPtr hwnd)
        {
            bool result = User32.IsIconic(hwnd) || !User32.IsWindowVisible(hwnd);
            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            if ((style & (long)WindowStyleFlags.MINIMIZE) != 0L)
            {
                result = true;
            }

            return result;
        }

        private bool IsOffScreen(IntPtr hwnd)
        {
            if (IsMinimized(hwnd))
                return false;

            const int MinSize = 10;
            RECT rect = new RECT();
            User32.GetWindowRect(hwnd, ref rect);
            if (rect.Width <= MinSize || rect.Height <= MinSize)
                return false;

            POINT topLeft = new POINT(rect.Left + MinSize, rect.Top + MinSize);
            if (User32.MonitorFromPoint(topLeft, User32.MONITOR_DEFAULTTONULL) == IntPtr.Zero)
            {
                Log.Error("top left of Rect {0} is off-screen", rect.ToString());
                return true;
            }

            POINT middle = new POINT(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
            if (User32.MonitorFromPoint(middle, User32.MONITOR_DEFAULTTONULL) == IntPtr.Zero)
            {
                Log.Error("middle point ({0}, {1}) is off-screen", middle.X, middle.Y);
                return true;
            }

            return false;
        }

        private void FixOffScreenWindow(IntPtr hwnd)
        {
            var displayKey = GetDisplayKey();
            if (!normalSessions.Contains(displayKey))
            {
                Log.Error("Avoid recover invisible window \"{0}\"", GetWindowTitle(hwnd));
                return;
            }

            if (deadApps.ContainsKey(curDisplayKey))
            {
                var deadAppPos = deadApps[curDisplayKey];
                string className = GetWindowClassName(hwnd);
                if (!string.IsNullOrEmpty(className))
                {
                    uint processId = 0;
                    uint threadId = User32.GetWindowThreadProcessId(hwnd, out processId);
                    string procPath = GetProcExePath(processId);
                    int idx = deadAppPos.Count;
                    bool found = false;
                    foreach (var appPos in deadAppPos.Reverse<DeadAppPosition>())
                    {
                        --idx;

                        if (!className.Equals(appPos.ClassName))
                            continue;
                        if (!procPath.Equals(appPos.ProcessPath))
                            continue;

                        // found match
                        RECT r= appPos.ScreenPosition;
                        User32.MoveWindow(hwnd, r.Left, r.Top, r.Width, r.Height, true);
                        Log.Error("Recover invisible window \"{0}\"", GetWindowTitle(hwnd));
                        found = true;
                        break;
                    }

                    if (found)
                    {
                        deadApps[curDisplayKey].RemoveAt(idx);
                        return;
                    }
                }
            }

            RECT rect = new RECT();
            User32.GetWindowRect(hwnd, ref rect);

            IntPtr desktopWindow = User32.GetDesktopWindow();
            RECT rectDesk = new RECT();
            User32.GetWindowRect(desktopWindow, ref rectDesk);

            RECT intersection = new RECT();
            bool overlap = User32.IntersectRect(out intersection, ref rect, ref rectDesk);
            if (overlap && intersection.Equals(rectDesk))
            {
                //fix issue #47, Win+Shift+S create screen fully covers desktop
                ;
            }
            else
            {
                User32.MoveWindow(hwnd, rectDesk.Left + 100, rectDesk.Top + 100, rect.Width, rect.Height, true);
                Log.Error("Auto fix invisible window \"{0}\"", GetWindowTitle(hwnd));
            }
        }

        private void PostActivateWindows()
        {
            {
                try
                {
                    List<IntPtr> pendingWindows = new List<IntPtr>(pendingActivateWindows);
                    foreach (IntPtr hwnd in pendingWindows)
                    {
                        if (User32.IsWindow(hwnd))
                            ActivateWindow(hwnd);
                    }

                    pendingActivateWindows.Clear();
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }

            }
        }

        private void ManualFixTopmostFlag(IntPtr hwnd)
        {
            try
            {
                // ctrl click received (mannually fix topmost flag)
                {
                    RECT rect = new RECT();
                    User32.GetWindowRect(hwnd, ref rect);

                    IntPtr prevWnd = hwnd;
                    while (true)
                    {
                        prevWnd = User32.GetWindow(prevWnd, 3);
                        if (prevWnd == IntPtr.Zero)
                            break;

                        if (prevWnd == hwnd)
                            break;

                        if (!monitorApplications.ContainsKey(curDisplayKey) || !monitorApplications[curDisplayKey].ContainsKey(prevWnd))
                            continue;

                        RECT prevRect = new RECT();
                        User32.GetWindowRect(prevWnd, ref prevRect);

                        RECT intersection = new RECT();
                        if (User32.IntersectRect(out intersection, ref rect, ref prevRect))
                        {
                            if (IsWindowTopMost(prevWnd))
                            {
                                FixTopMostWindow(prevWnd);

                                User32.SetWindowPos(prevWnd, hwnd,
                                    0, 0, 0, 0,
                                    0
                                    | SetWindowPosFlags.DoNotActivate
                                    | SetWindowPosFlags.IgnoreMove
                                    | SetWindowPosFlags.IgnoreResize
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }

        private void ActivateWindow(IntPtr hwnd)
        {
            try
            {
                bool enable_offscreen_fix = enableOffScreenFix;
                {
                    if (pendingMoveEvents.Contains(hwnd))
                    {
                        //ignore window currently moving by user
                        if (!enhancedOffScreenFix)
                        {
                            enable_offscreen_fix = false;
                        }
                    }

                    if (!monitorApplications.ContainsKey(curDisplayKey))
                    {
                        return;
                    }

                    // fix off-screen new window
                    if (!monitorApplications[curDisplayKey].ContainsKey(hwnd))
                    {
                        if (!enable_offscreen_fix)
                            return;

                        bool isNewWindow = true;
                        foreach (var key in monitorApplications.Keys)
                        {
                            if (monitorApplications[key].ContainsKey(hwnd))
                            {
                                isNewWindow = false;
                                break;
                            }
                        }

                        if (isNewWindow && IsOffScreen(hwnd) && normalSessions.Contains(curDisplayKey))
                        {
                            FixOffScreenWindow(hwnd);
                        }
                        return;
                    }

                    if (IsMinimized(hwnd))
                        return; // minimize operation

                    if (noRestoreWindows.Contains(hwnd))
                        return;

                    // unminimize to previous location
                    ApplicationDisplayMetrics prevDisplayMetrics = monitorApplications[curDisplayKey][hwnd].Last<ApplicationDisplayMetrics>();
                    if (prevDisplayMetrics.IsMinimized)
                    {
                        if (prevDisplayMetrics.IsFullScreen)
                        {
                            //the window was minimized from full screen status
                            RestoreFullScreenWindow(hwnd, prevDisplayMetrics.ScreenPosition);
                        }
                        else if (!IsFullScreen(hwnd))
                        {
                            RECT screenPosition = new RECT();
                            User32.GetWindowRect(hwnd, ref screenPosition);

                            RECT rect = prevDisplayMetrics.ScreenPosition;
                            if (prevDisplayMetrics.WindowPlacement.ShowCmd == ShowWindowCommands.ShowMinimized
                               || prevDisplayMetrics.WindowPlacement.ShowCmd == ShowWindowCommands.Minimize
                               || rect.Left <= -25600)
                            {
                                Log.Error("no qualified position data to restore minimized window \"{0}\"", GetWindowTitle(hwnd));
                                return; // captured without previous history info, let OS handle it
                            }

                            if (screenPosition.Equals(rect))
                                return;

                            if (fixUnminimizedWindow && !tidyTabWindows.Contains(hwnd))
                            {
                                //restore minimized window only applies if screen resolution has changed since minimize
                                if (prevDisplayMetrics.CaptureTime < lastDisplayChangeTime)
                                {
                                    // windows ignores previous snap status when activated from minimized state
                                    var placement = prevDisplayMetrics.WindowPlacement;
                                    User32.SetWindowPlacement(hwnd, ref placement);
                                    User32.MoveWindow(hwnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                                    Log.Error("restore minimized window \"{0}\"", GetWindowTitle(hwnd));
                                    return;
                                }
                            }

                            if (!enable_offscreen_fix)
                                return;

                            if (IsOffScreen(hwnd))
                            {
                                IntPtr desktopWindow = User32.GetDesktopWindow();
                                User32.GetWindowRect(desktopWindow, ref rect);
                                User32.MoveWindow(hwnd, rect.Left + 200, rect.Top + 200, 400, 300, true);
                                Log.Error("fix invisible window \"{0}\"", GetWindowTitle(hwnd));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }


        private bool IsTopLevelWindow(IntPtr hwnd)
        {
            if (IsTaskBar(hwnd))
                return true;

            if (User32.GetAncestor(hwnd, 1) != desktopWindow)
                return false;

            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            return (style & (long)WindowStyleFlags.MINIMIZEBOX) != 0L
                || (style & (long)WindowStyleFlags.SYSMENU) != 0L;
        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            {
                switch (eventType)
                {
                    case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                    case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                    case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                        // only care about child windows that are moved by user
                        allUserMoveWindows.Add(hwnd);
                        break;

                    case User32Events.EVENT_OBJECT_DESTROY:
                        allUserMoveWindows.Remove(hwnd);
                        break;

                    default:
                        break;
                        //return;
                }
            }

            if (eventType == User32Events.EVENT_OBJECT_DESTROY)
            {
                if (idObject != 0)
                {
                    // ignore non-window object (caret etc)
                    return;
                }

                noRestoreWindows.Remove(hwnd);

                foreach (var key in monitorApplications.Keys)
                {
                    if (!monitorApplications[key].ContainsKey(hwnd))
                        continue;

                    if (monitorApplications[key][hwnd].Count > 0)
                    {
                        // save window size of closed app to restore off-screen window later
                        if (!deadApps.ContainsKey(key))
                        {
                            deadApps.Add(key, new List<DeadAppPosition>());
                        }
                        var appPos = new DeadAppPosition();
                        var lastMetric = monitorApplications[key][hwnd].Last();
                        appPos.ClassName = lastMetric.ClassName;
                        appPos.ScreenPosition = lastMetric.ScreenPosition;
                        string procPath = GetProcExePath(lastMetric.ProcessId);
                        appPos.ProcessPath = procPath;
                        deadApps[key].Add(appPos);

                        //limit list size
                        while (deadApps[key].Count > 50)
                        {
                            deadApps[key].RemoveAt(0);
                        }
                    }

                    monitorApplications[key].Remove(hwnd);
                }

                bool found = windowTitle.Remove(hwnd);

                if (sessionActive && found)
                {
                    StartCaptureTimer(); //update z-order
                }

                return;
            }


            /* need invisible window event to detect session cut-off
            // only track visible windows
            if (!window.Visible)
            {
                return;
            }
            */

            // auto track taskbar
            var title = GetWindowTitle(hwnd);
            if (string.IsNullOrEmpty(title) && !IsTaskBar(hwnd))
            {
                return;
            }

            try
            {
#if DEBUG
                RECT screenPosition = new RECT();
                User32.GetWindowRect(hwnd, ref screenPosition);
                if (title.Contains("Microsoft Visual Studio")
                    && (eventType == User32Events.EVENT_OBJECT_LOCATIONCHANGE
                        || eventType == User32Events.EVENT_SYSTEM_FOREGROUND))
                {
                    return;
                }

                Log.Trace("WinEvent received. Type: {0:x4}, Window: {1:x8}", (uint)eventType, hwnd.ToInt64());

                var process = GetProcess(hwnd);
                string log = string.Format("Received message of process {0} at ({1}, {2}) of size {3} x {4} with title: {5}",
                    process.ProcessName,
                    screenPosition.Left,
                    screenPosition.Top,
                    screenPosition.Width,
                    screenPosition.Height,
                    title
                    );
                Log.Trace(log);
#endif

                if (restoringFromMem)
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            if (restoringSnapshot)
                                return;
                            // let it trigger next restore
                            break;

                        case User32Events.EVENT_SYSTEM_MOVESIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                            noRestoreWindowsTmp.Add(hwnd);
                            break;

                        default:
                            // no capture during restore
                            return;
                    }

                    {
                        if (restoreTimes >= MinRestoreTimes)
                        {
                            // restore is not finished as long as window location keeps changing
                            StartRestoreTimer();
                        }
                    }
                }
                else if (sessionActive)
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_SYSTEM_FOREGROUND:
                            {
                                if (restoringFromDB)
                                {
                                    // immediately capture new window
                                    //StartCaptureTimer(milliSeconds: 0);
                                    DateTime now = DateTime.Now;
                                    CaptureWindow(hwnd, eventType, now, curDisplayKey);
                                }
                                else
                                {
                                    foreGroundWindow[curDisplayKey] = hwnd;
                                    foregroundTimer.Change(100, Timeout.Infinite);


                                    // Occasionaly OS might bring a window to foreground upon sleep
                                    // If the window move is initiated by OS (before sleep),
                                    // keep restart capture timer would eventually discard these moves
                                    // either by power suspend event handler calling CancelCaptureTimer()
                                    // or due to capture timer handler found too many window moves

                                    // If the window move is caused by user snapping window to screen edge,
                                    // delay capture by a few seconds should be fine.

                                    if (!pendingActivateWindows.Contains(hwnd))
                                        pendingActivateWindows.Add(hwnd);

                                    if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd))
                                        StartCaptureTimer(UserMoveLatency / 2);
                                    else
                                        StartCaptureTimer();

                                    userMove = true;
                                }
                            }

                            break;
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            {
                                if (!restoringFromDB)
                                {
                                    // If the window move is initiated by OS (before sleep),
                                    // keep restart capture timer would eventually discard these moves
                                    // either by power suspend event handler calling CancelCaptureTimer()
                                    // or due to capture timer handler found too many window moves

                                    // If the window move is caused by user snapping window to screen edge,
                                    // delay capture by a few seconds should be fine.

                                    if (!pendingActivateWindows.Contains(hwnd))
                                    {
                                        pendingMoveEvents.Enqueue(hwnd);
                                    }
                                    
                                    if (foreGroundWindow.ContainsKey(curDisplayKey) && foreGroundWindow[curDisplayKey] == hwnd)
                                    {
                                        StartCaptureTimer(UserMoveLatency / 4);
                                    }
                                    else
                                    {
                                        StartCaptureTimer();
                                    }
                                }
                            }

                            break;

                        case User32Events.EVENT_SYSTEM_MOVESIZESTART:
                            curMovingWnd = hwnd;
                            moveTimer.Change(250, Timeout.Infinite);
                            break;

                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                            lastUnminimizeTime = DateTime.Now;
                            lastUnminimizeWindow = hwnd;
                            tidyTabWindows.Remove(hwnd); //no longer hidden by tidytab

                            if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd))
                            {
                                //capture with slight delay inperceivable by user, required for full screen mode recovery 
                                StartCaptureTimer(UserMoveLatency / 4);
                                userMove = true;
                            }
                            break;

                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                            {
                                DateTime now = DateTime.Now;
                                var diff = now.Subtract(lastUnminimizeTime);
                                if (diff.TotalMilliseconds < 200)
                                {
                                    Log.Error($"window \"{windowTitle[hwnd]}\" is hidden by tidytab");
                                    tidyTabWindows.Add(hwnd);
                                    if (lastUnminimizeWindow != IntPtr.Zero)
                                        tidyTabWindows.Add(lastUnminimizeWindow);
                                }
                            }

                            goto case User32Events.EVENT_SYSTEM_MOVESIZEEND;
                        case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                            // immediately capture user moves
                            // only respond to move of captured window to avoid miscapture
                            if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd) || allUserMoveWindows.Contains(hwnd))
                            {
                                StartCaptureTimer(UserMoveLatency / 4);
                                userMove = true;
                            }
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void TrimQueue(string displayKey, IntPtr hwnd)
        {
            while (monitorApplications[displayKey][hwnd].Count > MaxHistoryQueueLength)
            {
                // limit length of capture history
                for (int i = 0; i < monitorApplications[displayKey][hwnd].Count; ++i)
                {
                    if (monitorApplications[displayKey][hwnd][i].SnapShotFlags != 0)
                        continue; //preserve snapshot record
                    monitorApplications[displayKey][hwnd].RemoveAt(i);
                    break; //remove one record at one time
                }
            }
        }

        private void RemoveInvalidCapture()
        {
            try
            {
                {
                    if (monitorApplications.ContainsKey(curDisplayKey))
                    foreach (var hwnd in monitorApplications[curDisplayKey].Keys)
                    {
                        for (int i = monitorApplications[curDisplayKey][hwnd].Count - 1; i >= 0; --i)
                        {
                            if (!monitorApplications[curDisplayKey][hwnd][i].IsValid)
                            {
                                monitorApplications[curDisplayKey][hwnd].RemoveAt(i);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public bool TakeSnapshot(int snapshotId)
        {
            if (String.IsNullOrEmpty(curDisplayKey))
                return false;

            normalSessions.Add(curDisplayKey);

            if (restoringSnapshot)
            {
                Log.Error("wait for snapshot {0} restore to finish", snapshotId);
                return false;
            }

            {
                CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture : true);

                foreach(var hwnd in monitorApplications[curDisplayKey].Keys)
                {
                    int count = monitorApplications[curDisplayKey][hwnd].Count;
                    if (count > 0)
                    {
                        for (var i = 0; i < count - 1; ++i)
                            monitorApplications[curDisplayKey][hwnd][i].SnapShotFlags &= ~(1ul << snapshotId);
                        monitorApplications[curDisplayKey][hwnd][count - 1].SnapShotFlags |= (1ul << snapshotId);
                        monitorApplications[curDisplayKey][hwnd][count - 1].IsValid = true;
                    }
                }

                if (!snapshotTakenTime.ContainsKey(curDisplayKey))
                    snapshotTakenTime[curDisplayKey] = new Dictionary<int, DateTime>();

                var now = DateTime.Now;
                snapshotTakenTime[curDisplayKey][snapshotId] = now;
                Log.Event("Snapshot {0} is captured", snapshotId);
            }

            return true;
        }

        public void RestoreSnapshot(int id)
        {
            if (restoringSnapshot)
            {
                Log.Error("wait for snapshot {0} restore to finish", snapshotId);
                return;
            }

            if (!snapshotTakenTime.ContainsKey(curDisplayKey)
                || !snapshotTakenTime[curDisplayKey].ContainsKey(id))
                return; //snapshot not taken yet

            if (id != MaxSnapshots - 1)
            {
                // MaxSnapshots - 1 is for undo snapshot restore
                CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture : true);
                snapshotTakenTime[curDisplayKey][MaxSnapshots - 1] = DateTime.Now;
            }

            CancelRestoreTimer();
            CancelRestoreFinishedTimer();
            ResetState();

            restoringSnapshot = true;
            snapshotId = id;
            restoringFromMem = true;
            StartRestoreTimer(milliSecond : 0 /*wait mouse settle still for taskbar restore*/);
            Log.Event("restore snapshot {0}", id);
        }

        private void CaptureCursorPos(string displayKey)
        {
            POINT cursorPos;
            User32.GetCursorPos(out cursorPos);
            lastCursorPos[displayKey] = cursorPos;
        }

        private void RestoreCursorPos(string displayKey)
        {
            POINT cursorPos = lastCursorPos[displayKey];
            User32.SetCursorPos(cursorPos.X, cursorPos.Y);
        }

        private IntPtr GetPrevZorderWindow(IntPtr hWnd)
        {
            if (!User32.IsWindow(hWnd))
                return IntPtr.Zero;

            if (IsMinimized(hWnd))
                return IntPtr.Zero;

            RECT rect = new RECT();
            User32.GetWindowRect(hWnd, ref rect);

            IntPtr fail_safe_result = IntPtr.Zero;
            IntPtr result = hWnd;

            do
            {
                IntPtr result_prev = result;
                result = User32.GetWindow(result, 3);
                if (result == IntPtr.Zero)
                    break;
                if (result == result_prev)
                    break;

                if (monitorApplications[curDisplayKey].ContainsKey(result))
                {
                    if (IsMinimized(result))
                        continue;

                    if (fail_safe_result == IntPtr.Zero)
                        fail_safe_result = result;
                }

                RECT prevRect = new RECT();
                User32.GetWindowRect(result, ref prevRect);

                RECT intersection = new RECT();
                if (User32.IntersectRect(out intersection, ref rect, ref prevRect))
                {
                    if (monitorApplications[curDisplayKey].ContainsKey(result))
                        break;
                }
            } while (true);

            if (result == IntPtr.Zero)
            {
                result = fail_safe_result;
                if (fail_safe_result != IntPtr.Zero)
                    Log.Trace("fail safe prev zorder of {0} is {1}", GetWindowTitle(hWnd), GetWindowTitle(fail_safe_result));
            }

            return result;
        }

        public bool IsWindowTopMost(IntPtr hWnd)
        {
            long exStyle = User32.GetWindowLong(hWnd, User32.GWL_EXSTYLE);
            return (exStyle & User32.WS_EX_TOPMOST) != 0;
        }

        // restore z-order might incorrectly put some window to topmost
        // workaround by put these windows behind HWND_NOTOPMOST
        private bool FixTopMostWindow(IntPtr hWnd)
        {
            if (!IsWindowTopMost(hWnd))
                return false;

            bool ok = User32.SetWindowPos(hWnd, new IntPtr(-2), //notopmost
                0, 0, 0, 0,
                0
                | SetWindowPosFlags.DoNotActivate
                | SetWindowPosFlags.IgnoreMove
                | SetWindowPosFlags.IgnoreResize
            );

            Log.Error("Fix topmost window {0} {1}", GetWindowTitle(hWnd), ok.ToString());

            if (IsWindowTopMost(hWnd))
            {
                ok = User32.SetWindowPos(hWnd, new IntPtr(1), //bottom
                    0, 0, 0, 0,
                    0
                    | SetWindowPosFlags.DoNotActivate
                    | SetWindowPosFlags.IgnoreMove
                    | SetWindowPosFlags.IgnoreResize
                );
                Log.Error("Second try to fix topmost window {0} {1}", GetWindowTitle(hWnd), ok.ToString());
            }

            return ok;
        }

        private void BatchFixTopMostWindows()
        {
            try
            {
                foreach (var hwnd in topmostWindowsFixed)
                {
                    FixTopMostWindow(hwnd);
                }

                topmostWindowsFixed.Clear();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private bool AllowRestoreZorder()
        {
            return fixZorder == 2 || (restoringSnapshot && fixZorder > 0);
        }

        private int RestoreZorder(IntPtr hWnd, IntPtr prev)
        {
            if (prev == IntPtr.Zero)
            {
                Log.Trace("avoid restore to top most for window {0}", GetWindowTitle(hWnd));
                return 0; // issue 21, avoiding restore to top z-order
            }

            if (!User32.IsWindow(prev))
            {
                return 0;
            }

            /*
            if (!prevWindow.Process.Responding)
                return 0;
            */

            bool nonTopMost = false;
            if (IsTaskBar(prev))
            {
                Log.Error("restore under taskbar for window {0}", GetWindowTitle(hWnd));
                nonTopMost = true;
            }

            bool ok = User32.SetWindowPos(
                hWnd,
                nonTopMost ? new IntPtr(-2) : prev,
                0, //rect.Left,
                0, //rect.Top,
                0, //rect.Width,
                0, //rect.Height,
                0
                | SetWindowPosFlags.DoNotActivate
                | SetWindowPosFlags.IgnoreMove
                | SetWindowPosFlags.IgnoreResize
            );

            Log.Event("Restore zorder {2} by repositioning window \"{0}\" under \"{1}\"",
                GetWindowTitle(hWnd),
                GetWindowTitle(prev),
                ok ? "succeeded" : "failed");

            return ok ? 1 : -1;
        }

        private bool CaptureWindow(IntPtr hWnd, User32Events eventType, DateTime now, string displayKey)
        {
            bool ret = false;

            if (!displayKey.Equals(curDisplayKey))
                return false; //abort capture if display changed too soon

            if (!monitorApplications.ContainsKey(displayKey))
            {
                monitorApplications.Add(displayKey, new Dictionary<IntPtr, List<ApplicationDisplayMetrics>>());
            }

            ApplicationDisplayMetrics curDisplayMetrics;
            ApplicationDisplayMetrics prevDisplayMetrics;
            if (IsWindowMoved(displayKey, hWnd, eventType, now, out curDisplayMetrics, out prevDisplayMetrics))
            {
#if DEBUG
                string log = string.Format("Captured {0,-8} at ({1}, {2}) of size {3} x {4} {5} visible:{6} minimized:{7}",
                    curDisplayMetrics,
                    curDisplayMetrics.ScreenPosition.Left,
                    curDisplayMetrics.ScreenPosition.Top,
                    curDisplayMetrics.ScreenPosition.Width,
                    curDisplayMetrics.ScreenPosition.Height,
                    curDisplayMetrics.Title,
                    User32.IsWindowVisible(hWnd),
                    curDisplayMetrics.IsMinimized
                    );
                Log.Trace(log);

                string log2 = string.Format("    WindowPlacement.NormalPosition at ({0}, {1}) of size {2} x {3}",
                    curDisplayMetrics.WindowPlacement.NormalPosition.Left,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Top,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Width,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Height
                    );
                Log.Trace(log2);
#endif

                if (eventType != 0)
                    curDisplayMetrics.IsValid = true;

                if (!monitorApplications[displayKey].ContainsKey(hWnd))
                {
                    monitorApplications[displayKey].Add(hWnd, new List<ApplicationDisplayMetrics>());
                }
                else
                {
                    TrimQueue(displayKey, hWnd);
                }

                monitorApplications[displayKey][hWnd].Add(curDisplayMetrics);
                ret = true;
            }

            return ret;
        }

        public static string GetDisplayKey()
        {
            DesktopDisplayMetrics metrics = new DesktopDisplayMetrics();
            metrics.AcquireMetrics();
            return metrics.Key;
        }

        private List<Display> GetDisplays()
        {
            DesktopDisplayMetrics metrics = new DesktopDisplayMetrics();
            return metrics.GetDisplays();
        }

        private void StartCaptureTimer(int milliSeconds = CaptureLatency)
        {
            // ignore defer timer request to capture user move ASAP
            if (userMove)
                return; //assuming timer has already started

            // restart capture timer
            captureTimer.Change(milliSeconds, Timeout.Infinite);
        }

        private void CancelCaptureTimer()
        {
            userMove = false;
            userMovePrev = false;

            // restart capture timer
            captureTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void StartRestoreTimer(int milliSecond = RestoreLatency, bool wait = false)
        {
            restoreTimer.Change(milliSecond, Timeout.Infinite);
            if (wait)
            {
                Thread.Sleep(milliSecond);
            }
        }

        private void CancelRestoreTimer()
        {
            restoreTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void StartRestoreFinishedTimer(int milliSecond)
        {
            restoreFinishedTimer.Change(milliSecond, Timeout.Infinite);
        }

        private void CancelRestoreFinishedTimer()
        {
            restoreFinishedTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void BatchCaptureApplicationsOnCurrentDisplays(bool saveToDB = false)
        {
            try
            {
                if (restoringFromMem)
                {
                    return;
                }

                string displayKey = GetDisplayKey();
                if (!displayKey.Equals(curDisplayKey))
                {
                    Log.Trace("Ignore capture request for non-current display setting {0}", displayKey);
                    return;
                }

                if (userMovePrev)
                {
                    normalSessions.Add(curDisplayKey);
                }

                CaptureApplicationsOnCurrentDisplays(displayKey, saveToDB : saveToDB); //implies auto delayed capture
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }

        private void CaptureNewDisplayConfig(string displayKey)
        {
            normalSessions.Add(displayKey);
            CaptureApplicationsOnCurrentDisplays(displayKey, immediateCapture : true);
        }

        private void EndDisplaySession()
        {
            CancelCaptureTimer();
            ResetState();
        }

        private void ResetState()
        {
            {
                // end of restore period
                //CancelRestoreTimer();
                restoreTimes = 0;
                restoredWindows.Clear();

            }
        }

        private void RecordLastUserActionTime(DateTime time, string displayKey)
        {
            try
            {
                // validate captured entry
                foreach (var hwnd in monitorApplications[displayKey].Keys)
                {
                    if (monitorApplications[displayKey][hwnd].Count > 0)
                        monitorApplications[displayKey][hwnd].Last().IsValid = true;
                }

                if (lastUserActionTime.ContainsKey(displayKey))
                    lastUserActionTimeBackup[displayKey] = lastUserActionTime[displayKey];
                lastUserActionTime[displayKey] = time;

                Log.Trace("Capture time {0}", time);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey, bool saveToDB = false, bool immediateCapture = false)
        {
            Log.Trace("");
            Log.Trace("Capturing windows for display setting {0}", displayKey);

            int pendingEventCnt = pendingMoveEvents.Count;
            pendingMoveEvents.Clear();

            if (saveToDB)
            {
                using (var persistDB = new LiteDatabase(persistDbName))
                {
                    var ids = new HashSet<int>(); //db entries that need update
                    foreach (var hwnd in monitorApplications[displayKey].Keys)
                    {
                        var displayMetrics = monitorApplications[displayKey][hwnd].Last<ApplicationDisplayMetrics>();
                        if (displayKey == dbDisplayKey && displayMetrics.Id > 0)
                            ids.Add(displayMetrics.Id);
                    }

                    var db = persistDB.GetCollection<ApplicationDisplayMetrics>(dbDisplayKey);
                    if (db.Count() > 0)
                        db.DeleteMany(_ => !ids.Contains(_.Id)); //remove invalid entries (destroyed window since last capture to db)
                        //db.DeleteAll();

                    var appWindows = CaptureWindowsOfInterest();
                    foreach (var hWnd in appWindows)
                    {
                        if (!monitorApplications[displayKey].ContainsKey(hWnd))
                            continue;
                        if (!IsTopLevelWindow(hWnd))
                            continue;

                        try
                        {
                            var curDisplayMetrics = monitorApplications[displayKey][hWnd].Last<ApplicationDisplayMetrics>();
                            windowTitle[hWnd] = curDisplayMetrics.Title;

                            if (processCmd.ContainsKey(curDisplayMetrics.ProcessId))
                                curDisplayMetrics.ProcessExePath = processCmd[curDisplayMetrics.ProcessId];
                            else
                            {
                                string procPath = GetProcExePath(curDisplayMetrics.ProcessId);
                                if (!String.IsNullOrEmpty(procPath))
                                {
                                    curDisplayMetrics.ProcessExePath = procPath;
                                }
                            }

                            if (displayKey != dbDisplayKey)
                                curDisplayMetrics.Id = 0; //reset db id

                            if (curDisplayMetrics.Id == 0)
                            {
                                db.Insert(curDisplayMetrics);
                                monitorApplications[displayKey][hWnd].Add(curDisplayMetrics);
                            }
                            else
                                db.Update(curDisplayMetrics);
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex.ToString());
                        }
                    }
                }
            }
            else if (!userMovePrev && !immediateCapture && pendingEventCnt > MinWindowOsMoveEvents)
            {
                // too many pending window moves, they are probably initiated by OS instead of user,
                // defer capture
                StartCaptureTimer();
                Log.Trace("defer capture");
            }
            else // lock(databaseLock)
            {
                var appWindows = CaptureWindowsOfInterest();
                DateTime now = DateTime.Now;
                int movedWindows = 0;

                foreach (var hwnd in appWindows)
                {
                    try
                    {
                        if (CaptureWindow(hwnd, 0, now, displayKey))
                        {
                            movedWindows++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }

                if (!userMovePrev && !immediateCapture && pendingEventCnt > 0 && movedWindows > MaxUserMoves)
                {
                    // whether these are user moves is still doubtful
                    // defer acknowledge of user action by one more cycle
                    StartCaptureTimer();
                    Log.Trace("further defer capture");
                }
                else if (displayKey.Equals(curDisplayKey))
                {
                    // confirmed user moves
                    RecordLastUserActionTime(time: DateTime.Now, displayKey : displayKey);
                    if (movedWindows > 0)
                        Log.Trace("{0} windows captured", movedWindows);
                }
                else
                {
                    Log.Error("reject obsolete request to capture {0}", displayKey);
                }
            }
        }

        private IEnumerable<IntPtr> CaptureWindowsOfInterest()
        {
            /*
            return SystemWindow.AllToplevelWindows
                                .Where(row =>
                                {
                                    return row.Parent.HWnd.ToInt64() == 0
                                    && row.Visible;
                                });
            */

            HashSet<IntPtr> result = new HashSet<IntPtr>();
            IntPtr topMostWindow = User32.GetTopWindow(desktopWindow);

            for (IntPtr hwnd = topMostWindow; hwnd != IntPtr.Zero; hwnd = User32.GetWindow(hwnd, 2))
            {
                // only track top level windows - but GetParent() isn't reliable for that check (because it can return owners)
                if (!IsTopLevelWindow(hwnd))
                    continue;

                if (IsTaskBar(hwnd))
                {
                    result.Add(hwnd);
                    if (!taskbarReady && GetRealTaskBar(hwnd) != IntPtr.Zero)
                    {
                        taskbarReady = true;

                        //show icon on taskbar
                        hideRestoreTip();
                    }
                    continue;
                }

                /*
                if (!User32.IsWindowVisible(hwnd))
                    continue;
                */
                var rect = new RECT();
                User32.GetWindowRect(hwnd, ref rect);
                if (rect.Width <= 1 && rect.Height <= 1)
                    continue;

                if (string.IsNullOrEmpty(GetWindowClassName(hwnd)))
                    continue;

                if (string.IsNullOrEmpty(GetWindowTitle(hwnd)))
                    continue;

                // workaround runtime overflow exception in release build
                //SystemWindow window = new SystemWindow(hwnd);
                //WindowStyleFlags style = window.Style;

                /*
                long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
                if ((style & (long)WindowStyleFlags.MINIMIZEBOX) == 0L)
                    continue;
                */

                /* full screen app such as mstsc may not have maximize box */
                /*
                if ((style & (long)WindowStyleFlags.MAXIMIZEBOX) == 0L)
                {
                        continue;
                }
                */

                if (noRestoreWindows.Contains(hwnd))
                    continue;

                result.Add(hwnd);
            }

            foreach (var hwnd in allUserMoveWindows)
            {
                if (noRestoreWindows.Contains(hwnd))
                    continue;

                result.Add(hwnd);
            }

            return result;
        }

        private IntPtr GetCoreAppWindow(IntPtr hwnd)
        {
            uint processId = 0;
            User32.GetWindowThreadProcessId(hwnd, out processId);

            IntPtr prevChild = IntPtr.Zero;
            int i = 0;
            while (true && i < 10)
            {
                IntPtr currChild;
                currChild = User32.FindWindowEx(hwnd, prevChild, null, null);
                if (currChild == IntPtr.Zero)
                    break;
                uint realProcessId = 0;
                User32.GetWindowThreadProcessId(currChild, out realProcessId);
                if (realProcessId != processId)
                {
                    hwnd = currChild;
                    break;
                }
                prevChild = currChild;
                ++i;
            }
            return hwnd;
        }

        private bool IsWindowMoved(string displayKey, IntPtr hwnd, User32Events eventType, DateTime time,
            out ApplicationDisplayMetrics curDisplayMetrics, out ApplicationDisplayMetrics prevDisplayMetrics)
        {
            bool moved = false;
            curDisplayMetrics = null;
            prevDisplayMetrics = null;

            if (!User32.IsWindow(hwnd))
            {
                return false;
            }

            bool isTaskBar = false;
            if (IsTaskBar(hwnd))
            {
                // capture task bar
                isTaskBar = true;
            }

            WindowPlacement windowPlacement = new WindowPlacement();
            User32.GetWindowPlacement(hwnd, ref windowPlacement);

            // compensate for GetWindowPlacement() failure to get real coordinate of snapped window
            RECT screenPosition = new RECT();
            User32.GetWindowRect(hwnd, ref screenPosition);

            bool isMinimized = IsMinimized(hwnd);

            IntPtr realHwnd = hwnd;
            string className = GetWindowClassName(hwnd);
            if (className.Equals("ApplicationFrameWindow"))
            {
                //retrieve info about windows core app hidden under top window
                realHwnd = GetCoreAppWindow(hwnd);
                className = GetWindowClassName(realHwnd);
            }
            uint processId = 0;
            uint threadId = User32.GetWindowThreadProcessId(realHwnd, out processId);

            bool isFullScreen = IsFullScreen(hwnd);

            curDisplayMetrics = new ApplicationDisplayMetrics
            {
                HWnd = hwnd,
                ProcessId = processId,

                // this function call is very CPU-intensive
                //ProcessName = window.Process.ProcessName,
                ProcessName = "",

                ClassName = className,
                Title = isTaskBar ? "$taskbar$" : GetWindowTitle(hwnd, use_cache: false),

                //full screen app such as mstsc may not have maximize box
                IsFullScreen = isFullScreen,
                IsMinimized = isMinimized,
                IsInvisible = !User32.IsWindowVisible(hwnd),

                CaptureTime = time,
                WindowPlacement = windowPlacement,
                NeedUpdateWindowPlacement = false,
                ScreenPosition = screenPosition,

                IsTopMost = IsWindowTopMost(hwnd),
                NeedClearTopMost = false,

                PrevZorderWindow = GetPrevZorderWindow(hwnd),
                NeedRestoreZorder = false,

                IsValid = false,

                SnapShotFlags = 0ul,
            };

            if (!monitorApplications[displayKey].ContainsKey(hwnd))
            {
                //newly created window or new display setting
                var process = GetProcess(realHwnd);
                if (process == null)
                    return false;
                curDisplayMetrics.ProcessName = process.ProcessName;
                curDisplayMetrics.WindowId = (uint)hwnd;

                if (!windowTitle.ContainsKey(hwnd))
                {
                    windowTitle[hwnd] = curDisplayMetrics.Title;
                }

                if (ignoreProcess.Count > 0)
                {
                    if (ignoreProcess.Contains(curDisplayMetrics.ProcessName))
                    {
                        noRestoreWindows.Add(hwnd);
                        return false;
                    }
                }

                moved = true;
            }
            else
            {
                // find last record that satisfies cut-off time
                int prevIndex = monitorApplications[displayKey][hwnd].Count - 1;
                if (eventType == 0 && restoringFromMem)
                {
                    for (; prevIndex >= 0; --prevIndex)
                    {
                        var metrics = monitorApplications[displayKey][hwnd][prevIndex];
                        if (!metrics.IsValid)
                        {
                            Log.Error("invalid capture data {0}", GetWindowTitle(hwnd));
                            continue;
                        }
                        if (metrics.CaptureTime <= time)
                            break;
                    }
                }

                if (prevIndex < 0)
                {
                    Log.Error("no previous record found for window {0}", GetWindowTitle(hwnd));
                    if (restoringFromMem)
                    {
                        //the window did not exist when snapshot was taken
                        User32.SetWindowPos(hwnd, new IntPtr(1), //bottom
                            0, 0, 0, 0,
                            0
                            | SetWindowPosFlags.DoNotActivate
                            | SetWindowPosFlags.IgnoreMove
                            | SetWindowPosFlags.IgnoreResize
                        );

                        return false;
                    }
                    return !restoringFromMem;
                }

                prevDisplayMetrics = monitorApplications[displayKey][hwnd][prevIndex];
                curDisplayMetrics.Id = prevDisplayMetrics.Id;
                curDisplayMetrics.ProcessName = prevDisplayMetrics.ProcessName;
                curDisplayMetrics.WindowId = prevDisplayMetrics.WindowId;

                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId
                    || prevDisplayMetrics.ClassName != curDisplayMetrics.ClassName)
                {
                    // key collision between dead window and new window with the same hwnd
                    Log.Error("Invalid entry");
                    monitorApplications[displayKey].Remove(hwnd);
                    moved = true;
                }
                else if (curDisplayMetrics.IsMinimized && !prevDisplayMetrics.IsMinimized)
                {
                    curDisplayMetrics.WindowPlacement = prevDisplayMetrics.WindowPlacement;
                    curDisplayMetrics.ScreenPosition = prevDisplayMetrics.ScreenPosition;

                    curDisplayMetrics.NeedUpdateWindowPlacement = true;

                    if (prevDisplayMetrics.IsFullScreen)
                        curDisplayMetrics.IsFullScreen = true; // flag that current state is minimized from full screen mode

                    // no need to save z-order as unminimize always bring window to top
                    return true;
                }
                else if (curDisplayMetrics.IsMinimized && prevDisplayMetrics.IsMinimized)
                {
                    return false;
                }
                else if (!prevDisplayMetrics.EqualPlacement(curDisplayMetrics))
                {
                    curDisplayMetrics.NeedUpdateWindowPlacement = true;
                    moved = true;
                }
                else if (!prevDisplayMetrics.ScreenPosition.Equals(curDisplayMetrics.ScreenPosition))
                {
                    moved = true;
                }

                if (fixZorder > 0)
                {
                    if (prevDisplayMetrics.IsTopMost != curDisplayMetrics.IsTopMost)
                    {
                        if (!prevDisplayMetrics.IsTopMost && curDisplayMetrics.IsTopMost)
                            curDisplayMetrics.NeedClearTopMost = true;

                        moved = true;
                    }

                    if (prevDisplayMetrics.PrevZorderWindow != curDisplayMetrics.PrevZorderWindow)
                    {
                        curDisplayMetrics.NeedRestoreZorder = true;
                        moved = true;
                    }
                }

            }

            return moved;
        }

        private void TimerRestore()
        {
            if (pauseAutoRestore && !restoringFromDB && !restoringSnapshot)
                return;

            if (!restoringFromMem && !restoringFromDB)
                return;

            Log.Trace("Restore timer expired");

            lock(restoreLock)
            BatchRestoreApplicationsOnCurrentDisplays();
        }

        private void BatchRestoreApplicationsOnCurrentDisplays()
        {
            if (restoreTimes == 0)
            {
                if (!iconBusy)
                {
                    // fix issue 22, avoid frequent restore tip activation due to fast display setting switch
                    iconBusy = true;
                    showRestoreTip();
                }
            }

            try
            {
                CancelRestoreFinishedTimer();
                string displayKey = GetDisplayKey();
                if (restoreHalted || !displayKey.Equals(curDisplayKey))
                {
                    // display resolution changes during restore
                    restoreHalted = true;
                    StartRestoreFinishedTimer(haltRestore * 1000);
                }
                else if (restoreTimes < MaxRestoreTimes)
                {
                    bool zorderFixed = false;

                    try
                    {
                        RemoveInvalidCapture();
                        zorderFixed = RestoreApplicationsOnCurrentDisplays(displayKey, IntPtr.Zero);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }

                    restoreTimes++;

                    bool slow_restore = remoteSession && !restoringSnapshot;
                    bool extra_restore = zorderFixed;
                    // force next restore, as Windows OS might not send expected message during restore
                    if (restoreTimes < (extra_restore ? MaxRestoreTimes : MinRestoreTimes))
                        StartRestoreTimer(milliSecond : slow_restore ? RestoreLatency : 0);
                    else
                        StartRestoreFinishedTimer(milliSecond: slow_restore ? MaxRestoreLatency : RestoreLatency);
                }
                else
                {
                    // immediately finish restore
                    StartRestoreFinishedTimer(0);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }

        private string GetWindowClassName(IntPtr hwnd)
        {
            int nChars = 4096;
            StringBuilder buf = new StringBuilder(nChars);
            int chars = User32.GetClassName(hwnd, buf, nChars);
            return buf.ToString();
        }

        private bool IsTaskBar(IntPtr hwnd)
        {
            if (!User32.IsWindowVisible(hwnd))
                return false;

            try
            {
                return GetWindowClassName(hwnd).Equals("Shell_TrayWnd");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return false;
        }


        private void RestoreFullScreenWindow(IntPtr hwnd, RECT rect)
        {
            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            if ((style & (long)WindowStyleFlags.CAPTION) == 0L)
            {
                return;
            }

            RECT intersect = new RECT();

            bool wrong_screen = false;
            RECT cur_rect = new RECT();
            User32.GetWindowRect(hwnd, ref cur_rect);
            if (!User32.IntersectRect(out intersect, ref cur_rect, ref rect))
                wrong_screen = true;

            if (wrong_screen)
            {
                User32.MoveWindow(hwnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                Log.Error("fix wrong screen for {0}", GetWindowTitle(hwnd));
            }

            RECT screenPosition = new RECT();
            User32.GetWindowRect(hwnd, ref screenPosition);

            // window caption center might be occupied by other controls 
            int centerx = screenPosition.Left + screenPosition.Width / 4;

            int centery = screenPosition.Top + 15;
            User32.SetCursorPos(centerx, centery);
            User32.SetActiveWindow(hwnd);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN | MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(150);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN | MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);

            Log.Error("restore full screen window {0}", GetWindowTitle(hwnd));
        }

        private void RestoreSnapWindow(IntPtr hwnd, RECT target_pos)
        {
            List<Display> displays = GetDisplays();
            foreach (var display in displays)
            {
                RECT screen = display.Position;
                RECT intersect = new RECT();
                if (User32.IntersectRect(out intersect, ref target_pos, ref screen))
                {
                    if (intersect.Equals(target_pos))
                        continue;
                    if (Math.Abs(intersect.Width - target_pos.Width) < 10
                        && Math.Abs(intersect.Height - target_pos.Height)  < 10)
                    {
                        User32.MoveWindow(hwnd, intersect.Left, intersect.Top, intersect.Width, intersect.Height, true);
                        Log.Error("restore snap window {0}", GetWindowTitle(hwnd));
                        break;
                    }
                }
            }
        }

        private bool MoveTaskBar(IntPtr hwnd, RECT targetRect)
        {
            // simulate mouse drag, assuming taskbar is unlocked
            /*
                ControlGetPos x, y, w, h, MSTaskListWClass1, ahk_class Shell_TrayWnd
                MouseMove x+1, y+1
                MouseClickDrag Left, x+1, y+1, targetX, targetY, 10
            */
            int targetX = targetRect.Left + targetRect.Width / 2;
            int targetY = targetRect.Top + targetRect.Height / 2;

            RECT sourceRect = new RECT();
            User32.GetWindowRect(hwnd, ref sourceRect);

            // avoid unnecessary move
            int centerx = sourceRect.Left + sourceRect.Width / 2;
            int centery = sourceRect.Top + sourceRect.Height / 2;
            int deltax = Math.Abs(centerx - targetX);
            int deltay = Math.Abs(centery - targetY);
            if (deltax + deltay < 300)
            {
                // taskbar center has no big change (such as different screen edge alignment)
                return false;
            }

            RECT intersect = new RECT();
            User32.IntersectRect(out intersect, ref sourceRect, ref targetRect);
            if (intersect.Equals(sourceRect) || intersect.Equals(targetRect))
                return false; //only taskbar size changes

            IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            //User32.GetWindowRect(hReBar, ref screenPosition);

            IntPtr hTaskBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
            //hTaskBar = User32.FindWindowEx(hTaskBar, IntPtr.Zero, "MSTaskListWClass", null);
            User32.GetWindowRect(hTaskBar, ref sourceRect);

            // try place cursor to head and then tail of taskbar to guarantee move success
            int dx;
            int dy;
            if (sourceRect.Width > sourceRect.Height)
            {
                switch (restoreTimes)
                {
                    case 0:
                        dx = 2;
                        break;
                    default:
                        dx = sourceRect.Width - restoreTimes * 2;
                        break;
                }
                dy = sourceRect.Height / 2;
            }
            else
            {
                dx = sourceRect.Width / 2;
                switch (restoreTimes)
                {
                    case 0:
                        dy = 2;
                        break;
                    default:
                        dy = sourceRect.Height - restoreTimes * 2;
                        break;
                }
            }

            User32.SetCursorPos(sourceRect.Left + dx, sourceRect.Top + dy);
            User32.SetActiveWindow(hTaskBar);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(PauseRestoreTaskbar); // wait to be activated
            User32.SetCursorPos(targetX, targetY);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(1000); // wait OS finish move

            // center curser
            IntPtr desktopWindow = User32.GetDesktopWindow();
            RECT rect = new RECT();
            User32.GetWindowRect(desktopWindow, ref rect);
            User32.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

            return true;
        }

        // recover height of horizontal taskbar (TODO), or width of vertical taskbar
        private bool RecoverTaskBarArea(IntPtr hwnd, RECT targetRect)
        {
            RECT sourceRect = new RECT();
            User32.GetWindowRect(hwnd, ref sourceRect);

            int deltaWidth = sourceRect.Width - targetRect.Width;
            if (Math.Abs(deltaWidth) < 10)
                return false;

            RECT intersect = new RECT();
            if (!User32.IntersectRect(out intersect, ref sourceRect, ref targetRect))
                return false;
            if (!intersect.Equals(sourceRect) && !intersect.Equals(targetRect))
                return false;

            List<Display> displays = GetDisplays();
            bool left_edge = false;
            foreach (var display in displays)
            {
                RECT screen = display.Position;
                if (User32.IntersectRect(out intersect, ref sourceRect, ref screen))
                {
                    if (Math.Abs(targetRect.Left - screen.Left) < 5)
                        left_edge = true;
                    break;
                }
            }

            Log.Error("restore width of taskbar window {0}", GetWindowTitle(hwnd));

            int start_y = sourceRect.Top + sourceRect.Height / 2;
            int start_x;
            int end_x;
            if (left_edge)
            {
                //taskbar is on left edge
                start_x = sourceRect.Left + sourceRect.Width - 1;
                end_x = targetRect.Left + targetRect.Width - 1;
            }
            else
            {
                //taskbar is on right edge
                start_x = sourceRect.Left;
                end_x = targetRect.Left;
            }

            // avoid cursor failure
            /*
            IntPtr desktopWindow = User32.GetDesktopWindow();
            User32.SetCursorPos(initial_x, start_y);
            User32.SetActiveWindow(desktopWindow);
            Thread.Sleep(PauseRestoreTaskbar); // wait for popup window from taskbar to disappear
            */

            IntPtr hTaskBar = GetRealTaskBar(hwnd);

            User32.SetCursorPos(start_x, start_y);
            User32.SetActiveWindow(hTaskBar);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(PauseRestoreTaskbar); // wait to be activated
            User32.SetCursorPos(end_x, start_y);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);

            //move mouse to hide resize shape
            // center curser
            IntPtr desktopWindow = User32.GetDesktopWindow();
            RECT rect = new RECT();
            User32.GetWindowRect(desktopWindow, ref rect);
            User32.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

            return true;
        }

        private static IntPtr GetRealTaskBar(IntPtr hwnd)
        {
            IntPtr hTaskBar = IntPtr.Zero;
            IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            if (hReBar != IntPtr.Zero)
            {
                IntPtr hTBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
                if (hTBar != IntPtr.Zero)
                    hTaskBar = User32.FindWindowEx(hTBar, IntPtr.Zero, "MSTaskListWClass", null);
            }

            return hTaskBar;
        }

        private bool RestoreApplicationsOnCurrentDisplays(string displayKey, IntPtr sWindow)
        {
            bool zorderFixed = false;

            if (!monitorApplications.ContainsKey(displayKey)
                || monitorApplications[displayKey].Count == 0)
            {
                // the display setting has not been captured yet
                return false;
            }

            Log.Info("");
            Log.Info("Restoring windows pass {0} for {1}", restoreTimes, displayKey);

            IEnumerable<IntPtr> sWindows;
            var arr = new IntPtr[1];
            if (sWindow != IntPtr.Zero)
            {
                arr[0] = sWindow;
                sWindows = arr;
            }
            else
            {
                sWindows = CaptureWindowsOfInterest();
            }

            // determine the time to be restored
            DateTime lastCaptureTime = DateTime.Now;
            if (lastUserActionTime.ContainsKey(displayKey))
            {
                if (restoringSnapshot)
                {
                    if (!snapshotTakenTime.ContainsKey(curDisplayKey)
                        || !snapshotTakenTime[curDisplayKey].ContainsKey(snapshotId))
                        return false;

                    lastCaptureTime = snapshotTakenTime[curDisplayKey][snapshotId];
                }
                else
                {
                    lastCaptureTime = lastUserActionTime[displayKey];
                }
            }

            HashSet<int> dbMatchWindow = new HashSet<int>(); // db entry (id) matches existing window
            HashSet<IntPtr> windowMatchDb = new HashSet<IntPtr>(); //existing window matches db

            ApplicationDisplayMetrics SearchDb(IEnumerable<ApplicationDisplayMetrics> results, RECT rect, bool invisible, bool ignoreInvisible = false)
            {
                ApplicationDisplayMetrics choice = null;
                int best_delta = Int32.MaxValue;
                foreach (var result in results)
                {
                    if (dbMatchWindow.Contains(result.Id))
                        continue; //id already matched (to another window) 
                    if (!ignoreInvisible && result.IsInvisible != invisible)
                        continue;

                    // match with the best similar db entry
                    int delta = Math.Abs(rect.Left - result.ScreenPosition.Left) +
                        Math.Abs(rect.Top - result.ScreenPosition.Top) +
                        Math.Abs(rect.Width - result.ScreenPosition.Width) +
                        Math.Abs(rect.Height - result.ScreenPosition.Height);
                    if (delta < best_delta)
                    {
                        choice = result;
                        best_delta = delta;
                    }
                }

    #if DEBUG
                if (choice != null)
                    Log.Trace("restore window position with matching process name {0}", choice.ProcessName);
    #endif
                return choice;
            }

            DateTime printRestoreTime = lastCaptureTime;
            if (restoringFromDB)
            using(var persistDB = new LiteDatabase(persistDbName))
            {
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(dbDisplayKey);
                for (int dbMatchLevel = 0; dbMatchLevel < 4; ++dbMatchLevel)
                foreach (var hWnd in sWindows)
                {
                    if (windowMatchDb.Contains(hWnd))
                        continue;
                    if (!User32.IsWindow(hWnd) || string.IsNullOrEmpty(GetWindowClassName(hWnd)))
                        continue;

                    if (!monitorApplications[displayKey].ContainsKey(hWnd))
                        continue;

                    if (!IsTopLevelWindow(hWnd))
                        continue;

                    bool invisible = !User32.IsWindowVisible(hWnd);

                    RECT rect = new RECT();
                    User32.GetWindowRect(hWnd, ref rect);

                    ApplicationDisplayMetrics curDisplayMetrics = null;
                    ApplicationDisplayMetrics oldDisplayMetrics = monitorApplications[displayKey][hWnd].Last<ApplicationDisplayMetrics>();

                    var processName = oldDisplayMetrics.ProcessName;
                    var className = GetWindowClassName(hWnd);
                    IntPtr realHwnd = hWnd;
                    bool isCoreAppWindow = false;
                    if (className.Equals("ApplicationFrameWindow"))
                    {
                        realHwnd = GetCoreAppWindow(hWnd);
                        className = GetWindowClassName(realHwnd);
                        if (realHwnd != hWnd)
                        {
                            isCoreAppWindow = true;
                        }
                    }
                    uint processId = 0;
                    uint threadId = User32.GetWindowThreadProcessId(realHwnd, out processId);

                    IEnumerable<ApplicationDisplayMetrics> results;

                    if (dbMatchLevel == 0)
                    {
                        results = db.Find(x => x.ClassName == className && x.ProcessId == processId && x.WindowId == oldDisplayMetrics.WindowId && x.ProcessName == processName);
                        curDisplayMetrics = SearchDb(results, rect, invisible);
                    }

                    if (windowTitle.ContainsKey(hWnd))
                    {
                        string title = windowTitle[hWnd];

                        if (curDisplayMetrics == null && dbMatchLevel == 1)
                        {
                            results = db.Find(x => x.ClassName == className && x.Title == title && x.ProcessName == processName);
                            curDisplayMetrics = SearchDb(results, rect, invisible);
                        }
                    }

                    if (curDisplayMetrics == null && dbMatchLevel == 2)
                    {
                        results = db.Find(x => x.ClassName == className && x.ProcessName == processName);
                        curDisplayMetrics = SearchDb(results, rect, invisible);
                    }

                    /*
                    if (curDisplayMetrics == null && dbMatchLevel == 3)
                    {
                        results = db.Find(x => x.ClassName == className && x.ProcessName == processName);
                        curDisplayMetrics = SearchDb(results, rect, invisible, ignoreInvisible:true);
                    }
                    */

                    if (curDisplayMetrics == null && !IsTaskBar(hWnd) && !isCoreAppWindow && dbMatchLevel == 3)
                    {
                        results = db.Find(x => x.ProcessName == processName);
                        curDisplayMetrics = SearchDb(results, rect, invisible);
                    }

                    if (curDisplayMetrics == null)
                    {
                        // no db data to restore
                        continue;
                    }

                    if (dbMatchWindow.Contains(curDisplayMetrics.Id))
                        continue; //avoid restore multiple times

                    dbMatchWindow.Add(curDisplayMetrics.Id);
                    windowMatchDb.Add(hWnd);

                    // update stale window/process id
                    curDisplayMetrics.HWnd = hWnd;
                    curDisplayMetrics.WindowId = (uint)hWnd;
                    curDisplayMetrics.ProcessId = processId;
                    curDisplayMetrics.ProcessName = processName;
                    curDisplayMetrics.ClassName = className;
                    curDisplayMetrics.IsValid = true;

                    printRestoreTime = curDisplayMetrics.CaptureTime;
                    curDisplayMetrics.CaptureTime = lastCaptureTime;

                    TrimQueue(displayKey, hWnd);
                    monitorApplications[displayKey][hWnd].Add(curDisplayMetrics);
                }
            }

            Log.Trace("Restore time {0}", printRestoreTime);
            if (restoreTimes == 0)
            {
                Log.Event("Start restoring window layout back to {0} for display setting {1}", printRestoreTime, curDisplayKey);
            }

            bool batchZorderFix = false;

            foreach (var hWnd in sWindows)
            {
                if (restoreHalted)
                    break;

                if (!User32.IsWindow(hWnd))
                    continue;

                if (!monitorApplications[displayKey].ContainsKey(hWnd))
                    continue;

                if (noRestoreWindowsTmp.Contains(hWnd))
                    continue;

                ApplicationDisplayMetrics curDisplayMetrics;
                ApplicationDisplayMetrics prevDisplayMetrics;
                if (!IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics))
                    continue;

#if DEBUG
                var process = GetProcess(hWnd);
                if (!process.Responding)
                    continue;
#endif

                RECT rect = prevDisplayMetrics.ScreenPosition;
                WindowPlacement windowPlacement = prevDisplayMetrics.WindowPlacement;

                if (IsTaskBar(hWnd))
                {
                    if (!dryRun)
                    {
                        bool changed_edge = MoveTaskBar(hWnd, rect);
                        bool changed_width = RecoverTaskBarArea(hWnd, rect);
                        if (changed_edge || changed_width)
                            restoredWindows.Add(hWnd);
                    }
                    continue;
                }

                if (!dryRun)
                {
                    if (prevDisplayMetrics.IsMinimized)
                    {
                        // first try to minimize
                        User32.ShowWindow(hWnd, User32.SW_SHOWMINNOACTIVE);

                        // second try
                        if (!IsMinimized(hWnd))
                            User32.SendMessage(hWnd, User32.WM_SYSCOMMAND, User32.SC_MINIMIZE, IntPtr.Zero);
                        Log.Error("keep minimized window {0}", GetWindowTitle(hWnd));
                        continue;
                    }
                }

                if (AllowRestoreZorder() && restoringFromMem && curDisplayMetrics.NeedClearTopMost)
                {
                    //Log.Error("Found topmost window {0}", GetWindowTitle(hWnd));
                    FixTopMostWindow(hWnd);
                    topmostWindowsFixed.Add(hWnd);
                }

                if (AllowRestoreZorder() && restoringFromMem && curDisplayMetrics.NeedRestoreZorder)
                {
                    zorderFixed = true; //force next pass for topmost flag fix and zorder check

                    if (((fixZorderMethod >> restoreTimes) & 1) == 1)
                        batchZorderFix = true;
                    else
                        RestoreZorder(hWnd, prevDisplayMetrics.PrevZorderWindow);
                }

                bool success = true;
                if (restoreTimes >= MinRestoreTimes || curDisplayMetrics.NeedUpdateWindowPlacement)
                {
                    // recover NormalPosition (the workspace position prior to snap)
                    if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize && !dryRun)
                    {
                        // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                        // the window thinks it's maximized, but does not eat all the real estate. So we'll temporarily unmaximize then
                        // re-apply that
                        windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                        User32.SetWindowPlacement(hWnd, ref windowPlacement);
                        windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                    }
                    else if (restoreTimes == 0 && prevDisplayMetrics.IsFullScreen && !prevDisplayMetrics.IsMinimized && windowPlacement.ShowCmd == ShowWindowCommands.Normal && !dryRun)
                    {
                        Log.Error("recover full screen window {0}", GetWindowTitle(hWnd));
                        windowPlacement.ShowCmd = ShowWindowCommands.Minimize;
                        User32.SetWindowPlacement(hWnd, ref windowPlacement);
                        windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                    }

                    if (!dryRun)
                    {
                        success &= User32.SetWindowPlacement(hWnd, ref windowPlacement);
                    }
#if DEBUG
                    Log.Info("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                        process.ProcessName,
                        windowPlacement.NormalPosition.Left,
                        windowPlacement.NormalPosition.Top,
                        windowPlacement.NormalPosition.Width,
                        windowPlacement.NormalPosition.Height,
                        success);
#endif
                }

                // recover previous screen position
                if (!dryRun)
                {
                    success &= User32.MoveWindow(hWnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                    if (prevDisplayMetrics.IsFullScreen && windowPlacement.ShowCmd == ShowWindowCommands.Normal && !prevDisplayMetrics.IsMinimized)
                    {
                        RestoreFullScreenWindow(hWnd, prevDisplayMetrics.ScreenPosition);
                    }
                    else if (restoreTimes >= MinRestoreTimes - 1)
                    {
                        RECT cur_rect = new RECT();
                        User32.GetWindowRect(hWnd, ref cur_rect);
                        if (!cur_rect.Equals(rect))
                        {
                            RestoreSnapWindow(hWnd, rect);
                        }
                    }
                    restoredWindows.Add(hWnd);

#if DEBUG
                    Log.Info("MoveWindow({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                        process.ProcessName,
                        rect.Left,
                        rect.Top,
                        rect.Width,
                        rect.Height,
                        success);
#endif
                }

                if (!success)
                {
                    string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    Log.Error(error);
                }
            }

            if (AllowRestoreZorder() && batchZorderFix)
            {
                try
                {
                    IntPtr hWinPosInfo = User32.BeginDeferWindowPos(sWindows.Count<IntPtr>());
                    foreach (var hWnd in sWindows)
                    {
                        if (!User32.IsWindow(hWnd))
                        {
                            continue;
                        }

                        if (!monitorApplications[displayKey].ContainsKey(hWnd))
                        {
                            continue;
                        }

                        if (IsMinimized(hWnd))
                            continue;

                        ApplicationDisplayMetrics curDisplayMetrics;
                        ApplicationDisplayMetrics prevDisplayMetrics;

                        // get previous value
                        IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics);
                        if (prevDisplayMetrics == null)
                            continue;

                        /*
                        var window = new SystemWindow(hWnd);
                        if (!window.Process.Responding)
                            continue;
                        */

                        IntPtr prevZwnd = prevDisplayMetrics.PrevZorderWindow;
                        /*
                        if (prevDisplayMetrics.PrevZorderWindow == IntPtr.Zero)
                            continue; //avoid topmost
                        */

                        if (prevZwnd != IntPtr.Zero)
                        try
                        {
                            if (!User32.IsWindow(prevZwnd))
                                continue;

                            /*
                            SystemWindow prevWindow = new SystemWindow(prevZwnd);
                            if (!prevWindow.IsValid())
                                continue;

                            if (!prevWindow.Process.Responding)
                                continue;
                            */
                        }
                        catch (Exception)
                        {
                            continue;
                        }

                        if (hWnd == prevZwnd)
                            prevZwnd = new IntPtr(1); //place at bottom to avoid dead loop

                        /*
                        if (restoreTimes > 0 && !curDisplayMetrics.NeedRestoreZorder)
                            continue;
                        */

                        hWinPosInfo = User32.DeferWindowPos(hWinPosInfo, hWnd, prevZwnd,
                            0, 0, 0, 0,
                            0
                            | User32.DeferWindowPosCommands.SWP_NOACTIVATE
                            | User32.DeferWindowPosCommands.SWP_NOMOVE
                            | User32.DeferWindowPosCommands.SWP_NOSIZE
                        );

                        if (hWinPosInfo == IntPtr.Zero)
                            break;
                    }

                    bool batchRestoreResult = false;
                    if (hWinPosInfo != IntPtr.Zero)
                    {
                        batchRestoreResult = User32.EndDeferWindowPos(hWinPosInfo);
                    }

                    if (!batchRestoreResult)
                        Log.Error("batch restore z-order failed");
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }

            // clear topmost
            foreach (var hWnd in sWindows)
            {
                if (restoreHalted)
                    continue;

                if (!User32.IsWindow(hWnd))
                {
                    continue;
                }

                if (!monitorApplications[displayKey].ContainsKey(hWnd))
                {
                    continue;
                }

                ApplicationDisplayMetrics curDisplayMetrics;
                ApplicationDisplayMetrics prevDisplayMetrics;
                if (!IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics))
                    continue;

                if (AllowRestoreZorder() && restoringFromMem && curDisplayMetrics.NeedClearTopMost)
                {
                    //Log.Error("Found topmost window {0}", GetWindowTitle(hWnd));
                    FixTopMostWindow(hWnd);
                    topmostWindowsFixed.Add(hWnd);
                    zorderFixed = true; //force next pass for topmost flag fix and zorder check
                }
            }

            Log.Trace("Restored windows position for display setting {0}", displayKey);

            if (restoringFromDB && restoreTimes == 0)
            using(var persistDB = new LiteDatabase(persistDbName))
            {
                HashSet<uint> dbMatchProcess = new HashSet<uint>(); // db entry (process id) matches existing window
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(dbDisplayKey);

                // launch missing process according to db
                var results = db.FindAll(); // find process not yet started
                var i = 0; //.bat file id
                bool yes_to_all = autoRestoreMissingWindows;
                foreach (var curDisplayMetrics in results)
                {
                    if (curDisplayMetrics.IsInvisible)
                        continue;

                    if (dbMatchWindow.Contains(curDisplayMetrics.Id))
                        continue;

                    if (launchOncePerProcessId)
                    {
                        if (dbMatchProcess.Contains(curDisplayMetrics.ProcessId))
                            continue;

                        dbMatchProcess.Add(curDisplayMetrics.ProcessId);
                    }

                    if (!yes_to_all)
                    {
                        var runProcessDlg = new LaunchProcess(curDisplayMetrics.ProcessName, curDisplayMetrics.Title);
                        runProcessDlg.TopMost = true;
                        runProcessDlg.Icon = icon;
                        runProcessDlg.ShowDialog();

                        bool no_to_all = runProcessDlg.buttonName.Equals("NoToAll");
                        if (no_to_all)
                            break;

                        var no_set = new HashSet<string>() { "No", "None" };
                        if (no_set.Contains(runProcessDlg.buttonName))
                            continue;

                        yes_to_all = runProcessDlg.buttonName.Equals("YesToAll");
                    }

                    if (!String.IsNullOrEmpty(curDisplayMetrics.ProcessExePath))
                    {
                        if (!dryRun)
                        {
                            try
                            {
                                string processPath = curDisplayMetrics.ProcessExePath;
                                foreach (var processName in realProcessFileName.Keys)
                                {
                                    if (processPath.Contains(processName))
                                    {
                                        processPath = processPath.Replace(processName, realProcessFileName[processName]);
                                        break;
                                    }
                                }

                                if (processPath.Contains(" ") && !processPath.Contains("\"") && !processPath.Contains(".exe "))
                                {
                                    processPath = $"\"{processPath}\"";
                                }

                                if (processPath.StartsWith("usr\\bin\\mintty.exe"))
                                {
                                    processPath = processPath.Replace("usr\\bin\\mintty.exe", "\"C:\\Program Files\\Git\\usr\\bin\\mintty.exe\"");
                                }

                                Log.Event("launch process {0}", processPath);
                                string batFile = Path.Combine(appDataFolder, $"pw_exec{i}.bat");
                                ++i;
                                File.WriteAllText(batFile, "start \"\" /B " + processPath);
                                //Process.Start(batFile);
                                //Process process = Process.Start("cmd.exe", "-c " + batFile);
                                Process process = Process.Start("explorer.exe", batFile);
                                Thread.Sleep(2000);
                                //File.Delete(batFile);
                                if (!process.HasExited)
                                    process.Kill();
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex.ToString());
                            }
                        }
                    }
                }
            }

            return zorderFixed;
        }


        private string GetProcExePath(uint proc_id)
        {
            IntPtr hProcess = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryInformation, false, proc_id);
            string pathToExe = string.Empty;

            int nChars = 4096;
            StringBuilder buf = new StringBuilder(nChars);

            bool success = Kernel32.QueryFullProcessImageName(hProcess, 0, buf, ref nChars);

            if (success)
            {
                pathToExe = buf.ToString();
            }
            /*
            else
            {
                // fail to get taskmgr process path, need admin privilege
                int error = Marshal.GetLastWin32Error();
                pathToExe = ("Error = " + error + " when calling GetProcessImageFileName");
            }
            */

            Kernel32.CloseHandle(hProcess);
            return pathToExe;
        }

        private Process GetProcess(IntPtr hwnd)
        {
            Process r = null;
            try
            {
                uint pid;
                User32.GetWindowThreadProcessId(hwnd, out pid);
                r = Process.GetProcessById((int)pid);
            }
            catch (Exception ex)
            {
                Log.Trace(ex.ToString());
            }
            return r;
        }

        void ShowDesktop()
        {
            Process process = new Process();
            process.StartInfo.FileName = "explorer.exe";
            process.StartInfo.Arguments = "shell:::{3080F90D-D7AD-11D9-BD98-0000947B0257}";
            process.StartInfo.UseShellExecute = true;
            // Start process and handlers
            process.Start();
            process.WaitForExit();
        }

        private List<IntPtr> GetWindows(string procName)
        {
            List<IntPtr> result = new List<IntPtr>();
            foreach (var hwnd in monitorApplications[curDisplayKey].Keys)
            {
                string pName = monitorApplications[curDisplayKey][hwnd].Last<ApplicationDisplayMetrics>().ProcessName;
                if (pName.Equals(procName))
                {
                    result.Add(hwnd);
                }
            }

            return result;
        }

        private void TestSetWindowPos()
        {
            IntPtr[] w = GetWindows("notepad").ToArray();
            if (w.Length < 2)
                return;

            bool ok = User32.SetWindowPos(
                w[0],
                w[1],
                0, //rect.Left,
                0, //rect.Top,
                0, //rect.Width,
                0, //rect.Height,
                0
                //| SetWindowPosFlags.DoNotRedraw
                //| SetWindowPosFlags.DoNotSendChangingEvent
                | SetWindowPosFlags.DoNotChangeOwnerZOrder
                | SetWindowPosFlags.DoNotActivate
                | SetWindowPosFlags.IgnoreMove
                | SetWindowPosFlags.IgnoreResize
            );
        }

        public void StopRunningThreads()
        {
            foreach(var thd in runningThreads)
            {
                if (thd.IsAlive)
                    thd.Abort();
            }
        }

#region IDisposable
        public virtual void Dispose(bool disposing)
        {
            StopRunningThreads();

            if (initialized)
            {
                SystemEvents.DisplaySettingsChanging -= this.displaySettingsChangingHandler;
                SystemEvents.DisplaySettingsChanged -= this.displaySettingsChangedHandler;
                SystemEvents.PowerModeChanged -= powerModeChangedHandler;
                SystemEvents.SessionSwitch -= sessionSwitchEventHandler;

                foreach (var handle in this.winEventHooks)
                {
                    User32.UnhookWinEvent(handle);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PersistentWindowProcessor()
        {
            Dispose(false);
        }
#endregion
    }

}
