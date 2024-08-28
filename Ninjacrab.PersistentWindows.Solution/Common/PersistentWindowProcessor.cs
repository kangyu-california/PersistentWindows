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
using Microsoft.Win32;
using System.Xml;
using System.Runtime.Serialization;

using LiteDB;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.Models;
using PersistentWindows.Common.WinApiBridge;
using PersistentWindows.Common.Minimize2Tray;

namespace PersistentWindows.Common
{
    public class PersistentWindowProcessor : IDisposable
    {
        // constant
        private const int RestoreLatency = 500; // default delay in milliseconds from display change to window restore
        private const int SlowRestoreLatency = 1000; // delay in milliseconds from power resume to window restore
        private const int MaxRestoreLatency = 2000; // max delay in milliseconds from final restore pass to restore finish
        private const int MinRestoreTimes = 2; // minimum restore passes
        private const int MaxRestoreTimes = 5; // maximum restore passes

        public int UserForcedCaptureLatency = 0;
        public int UserForcedRestoreLatency = 0;
        private const int CaptureLatency = 3000; // delay in milliseconds from window OS move to capture
        private const int UserMoveLatency = 1000; // delay in milliseconds from user move/minimize/unminimize/maximize to capture, must < CaptureLatency
        private const int MaxUserMoves = 4; // max user window moves per capture cycle
        private const int MinWindowOsMoveEvents = 12; // threshold of window move events initiated by OS per capture cycle
        private const int MaxSnapshots = 38; // 0-9, a-z, ` (for redo last auto restore) and final one for undo last snapshot restore
        //[MaxSnapshots] is current capture, [MaxSnapshots + 1] is previous capture
        private const int MaxHistoryQueueLength = 41; // ideally bigger than MaxSnapshots + 2

        private const int PauseRestoreTaskbar = 3500; //cursor idle time before dragging taskbar

        private bool initialized = false;

        // window position database
        private Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>> monitorApplications
            = new Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>(); //in-memory database of live windows
        private Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>> deadApps
            = new Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>(); //database of killed windows
        //private long lastKilledWindowId = 0; //monotonically increasing unique id for every killed window
        private string persistDbName = null; //on-disk database name
        private Dictionary<string, POINT> lastCursorPos = new Dictionary<string, POINT>();
        private HashSet<IntPtr> allUserMoveWindows = new HashSet<IntPtr>();
        private HashSet<IntPtr> unResponsiveWindows = new HashSet<IntPtr>();
        private HashSet<IntPtr> noRecordWindows = new HashSet<IntPtr>();
        private static IntPtr desktopWindow = User32.GetDesktopWindow();
        private static IntPtr vacantDeskWindow = IntPtr.Zero;

        // windows that are not to be restored
        private static HashSet<IntPtr> noRestoreWindows = new HashSet<IntPtr>(); //windows excluded from auto-restore
        private static HashSet<IntPtr> noRestoreWindowsTmp = new HashSet<IntPtr>(); //user moved windows during restore

        // realtime fixing window location
        private IntPtr curMovingWnd = IntPtr.Zero;
        private Timer moveTimer; // when user move a window
        private Timer foregroundTimer; // when user bring a window to foreground
        private DateTime lastDisplayChangeTime = DateTime.Now;

        // control shared by capture and restore
        private LiteDatabase singletonLock; //prevent second PW inst from running

        // capture control
        private Timer captureTimer;
        private string curDisplayKey; // current display config name
        public string dbDisplayKey = null;
        private static Dictionary<IntPtr, string> windowTitle = new Dictionary<IntPtr, string>(); // for matching running window with DB record
        private Queue<IntPtr> pendingMoveEvents = new Queue<IntPtr>(); // queue of window with possible position change for capture
        private HashSet<string> normalSessions = new HashSet<string>(); //normal user sessions, for differentiating full screen game session or other transient session
        public bool manualNormalSession = false; //user need to manually take snapshot or save/restore from db to flag normal session
        private bool userMove = false; //received window event due to user move
        private bool userMovePrev = false; //prev value of userMove
        private HashSet<IntPtr> tidyTabWindows = new HashSet<IntPtr>(); //tabbed windows bundled by tidytab
        private DateTime lastKillTime = DateTime.Now;
        private DateTime lastUnminimizeTime = DateTime.Now;
        private IntPtr lastUnminimizeWindow = IntPtr.Zero;
        private IntPtr foreGroundWindow;
        private IntPtr realForeGroundWindow = IntPtr.Zero;
        public Dictionary<uint, string> processCmd = new Dictionary<uint, string>();
        private HashSet<IntPtr> fullScreenGamingWindows = new HashSet<IntPtr>();
        private IntPtr fullScreenGamingWindow = IntPtr.Zero;
        private bool exitFullScreenGaming = false;
        private POINT initCursorPos;
        private bool freezeCapture = false;
        public bool rejectScaleFactorChange = true;

        // restore control
        private Timer restoreTimer;
        private Timer restoreFinishedTimer;
        public bool restoringFromMem = false; // automatic restore from memory or snapshot
        public bool restoringFromDB = false; // manual restore from DB
        public bool autoInitialRestoreFromDB = false;
        public bool restoringSnapshot = false; // implies restoringFromMem
        public bool showDesktop = false; // show desktop when display changes
        public int fixZorder = 1; // 1 means restore z-order only for snapshot; 2 means restore z-order for all; 0 means no z-order restore at all
        public int fixZorderMethod = 5; // bit i represent restore method for pass i
        public bool fixTaskBar = true;
        public bool pauseAutoRestore = false;
        public bool promptSessionRestore = false;
        public bool redrawDesktop = false;
        public bool enableOffScreenFix = true;
        public bool enhancedOffScreenFix = false;
        public bool fixUnminimizedWindow = true;
        public bool autoRestoreMissingWindows = false;
        public bool autoRestoreLiveWindows = true; //for new display session, autorestore live windows using data from db (without resurrecting dead one)
        public bool launchOncePerProcessId = true;
        private int restoreTimes = 0; //multiple passes need to fully restore
        private Object restoreLock = new object();
        private Object dbLock = new object();
        private bool restoreHalted = false;
        public int haltRestore = 3000; //milliseconds to wait to finish current halted restore and restart next one
        private HashSet<IntPtr> restoredWindows = new HashSet<IntPtr>();
        private HashSet<IntPtr> topmostWindowsFixed = new HashSet<IntPtr>();

        public bool enableDualPosSwitch = true;
        private HashSet<IntPtr> dualPosSwitchWindows = new HashSet<IntPtr>();

        public bool enableMinimizeToTray = true;

        private Dictionary<string, string> realProcessFileName = new Dictionary<string, string>()
            {
                { "WindowsTerminal.exe", "wt.exe"},
            };

        private static HashSet<string> browserProcessNames = new HashSet<string>()
        {
            "chrome", "firefox", "msedge", "vivaldi", "opera", "brave", "360ChromeX"
        };

        public bool dumpDataWhenExit = true;
        private string windowPosDataFile = "window_pos.xml"; //for PW restart without PC reboot
        private string snapshotTimeFile = "snapshot_time.xml";

        private HashSet<string> ignoreProcess = new HashSet<string>();
        private HashSet<string> debugProcess = new HashSet<string>();
        private HashSet<IntPtr> debugWindows = new HashSet<IntPtr>();

        private static Dictionary<IntPtr, string> windowProcessName = new Dictionary<IntPtr, string>();
        private Process process;
        private ProcessPriorityClass processPriority;

        private string appDataFolder;
        public bool redirectAppDataFolder = false;

        // session control
        private bool sessionLocked = false; //requires password to unlock
        public bool sessionActive = true;
        private bool remoteSession = false;

        // restore time
        private Dictionary<string, Dictionary<int, DateTime>> snapshotTakenTime = new Dictionary<string, Dictionary<int, DateTime>>();
        public int snapshotId;

        private bool iconBusy = false;
        private bool taskbarReady = false;

        // callbacks
        public delegate void CallBack();
        public delegate void CallBackBool(bool en = true);

        public CallBack showRestoreTip;
        public CallBackBool hideRestoreTip;

        public CallBackBool enableRestoreSnapshotMenu;
        public delegate void CallBackBool2(bool en, bool en2);
        public CallBackBool2 enableRestoreMenu;
        public delegate void CallBackStr(string text);
        public CallBackStr changeIconText;

        private PowerModeChangedEventHandler powerModeChangedHandler;
        private EventHandler displaySettingsChangingHandler;
        private EventHandler displaySettingsChangedHandler;
        private SessionSwitchEventHandler sessionSwitchEventHandler;

        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private User32.WinEventDelegate winEventsCaptureDelegate;

        public static System.Drawing.Icon icon = null;

        private int leftButtonClicks = 0;

        // running thread
        private HashSet<Thread> runningThreads = new HashSet<Thread>();

        private VirtualDesktop vd = new VirtualDesktop();
        private Guid curVirtualDesktop;

#if DEBUG
        private void DebugInterval()
        {
            ;
        }
#endif

        private void DumpSnapshotTakenTime()
        {
            DataContractSerializer dcs2 = new DataContractSerializer(typeof(Dictionary<string, Dictionary<int, DateTime>>));
            StringBuilder sb2 = new StringBuilder();
            using (XmlWriter xw = XmlWriter.Create(sb2))
            {
                dcs2.WriteObject(xw, snapshotTakenTime);
            }
            string xml2 = sb2.ToString();
            File.WriteAllText(Path.Combine(appDataFolder, snapshotTimeFile), xml2, Encoding.Unicode);
        }

        private void WriteDataDumpCore()
        {
            DataContractSerializer dcs = new DataContractSerializer(typeof(Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>));
            StringBuilder sb = new StringBuilder();
            using (XmlWriter xw = XmlWriter.Create(sb))
            {
                dcs.WriteObject(xw, monitorApplications);
            }
            string xml = sb.ToString();
            File.WriteAllText(Path.Combine(appDataFolder, windowPosDataFile), xml, Encoding.Unicode);

            DumpSnapshotTakenTime();
        }
        public void WriteDataDump()
        {
            try
            {
                if (dumpDataWhenExit)
                    WriteDataDumpCore();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private void ReadDataDump()
        {
            string path = Path.Combine(appDataFolder, windowPosDataFile);
            if (!File.Exists(path))
                return;
            DataContractSerializer dcs = new DataContractSerializer(typeof(Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>));
            using (FileStream fs = File.OpenRead(path))
            using (XmlReader xr = XmlReader.Create(fs))
            {
                deadApps = (Dictionary<string, Dictionary<IntPtr, List<ApplicationDisplayMetrics>>>)dcs.ReadObject(xr);
            }
            //File.Delete(Path.Combine(appDataFolder, windowPosDataFile));

            string path2 = Path.Combine(appDataFolder, snapshotTimeFile);
            if (!File.Exists(path2))
                return;
            DataContractSerializer dcs2 = new DataContractSerializer(typeof(Dictionary<string, Dictionary<int, DateTime>>));
            using (FileStream fs = File.OpenRead(path2))
            using (XmlReader xr = XmlReader.Create(fs))
            {
                snapshotTakenTime = (Dictionary<string, Dictionary<int, DateTime>>)dcs2.ReadObject(xr);
            }
            //File.Delete(Path.Combine(appDataFolder, snapshotTimeFile));
        }

        private void ReadDataDumpSafe()
        {
            try
            {
                if (dumpDataWhenExit)
                    ReadDataDump();
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }
        }

        private void CleanupDisplayRegKey(string key)
        {
            if (key.Contains("__"))
                return; //multi-monitor config
            try
            {
                CleanupDisplayRegKeyCore(key);
            }
            catch(Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private bool IsRdpWindow(IntPtr hwnd)
        {
            bool r = false;
            try
            {
                if (User32.IsWindow(hwnd) && windowProcessName.ContainsKey(hwnd) && windowProcessName[hwnd].Equals("mstsc"))
                    r = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return r;
        }

        private void CleanupDisplayRegKeyCore(string key)
        {
            RegistryKey top = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers\Configuration", true);
            string[] configs = top.GetSubKeyNames();
            foreach (var config_name in configs)
            {
                RegistryKey config = top.OpenSubKey(config_name);
                string[] displays = config.GetSubKeyNames();
                if (displays.Length > 1)
                    continue;
                RegistryKey display = config.OpenSubKey(displays[0]);
                int width = (int)display.GetValue("PrimSurfSize.cx");
                int height = (int)display.GetValue("PrimSurfSize.cy");
                string pat = $"{width}x{height}";
                if (!key.Contains(pat))
                    continue;
                Log.Error($"remove display config {config_name} from registry corresponding to {key}");
                top.DeleteSubKeyTree(config_name);
            }
        }

        private bool SnapshotExists(string displayKey)
        {
            if (!snapshotTakenTime.ContainsKey(displayKey))
                return false;

            foreach (var id in snapshotTakenTime[displayKey].Keys)
            {
                // 26 + 10 maximum manual snapshots
                if (id < 36)
                    return true;
            }

            return false;
        }

        private bool RestoreExists(string displayKey)
        {
            if (!snapshotTakenTime.ContainsKey(displayKey))
                return false;

            foreach (var id in snapshotTakenTime[displayKey].Keys)
            {
                // 26 + 10 maximum manual snapshots
                // + last auto restore, + last snapshot restore
                if (id < MaxSnapshots)
                    return true;
            }

            return false;
        }

        public bool Start(bool auto_restore_from_db = false)
        {
            process = Process.GetCurrentProcess();
            processPriority = process.PriorityClass;

            string productName = System.Windows.Forms.Application.ProductName;
            appDataFolder = redirectAppDataFolder ? "." :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), productName);

#if DEBUG
            //avoid db path conflict with release version
            //appDataFolder = ".";
            appDataFolder = AppDomain.CurrentDomain.BaseDirectory;
#endif

            try
            {
                string singletonLockName = $@"{appDataFolder}/{productName}.db.lock";
                singletonLock = new LiteDatabase(singletonLockName);
            }
            catch (Exception)
            {
                User32.SetThreadDpiAwarenessContextSafe();

                System.Windows.Forms.MessageBox.Show("Another instance is already running.", productName,
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Exclamation,
                    System.Windows.Forms.MessageBoxDefaultButton.Button1,
                    System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly
                );

                return false;
            }

            var db_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            persistDbName = $@"{appDataFolder}/{productName}.{db_version}.db";
            bool found_latest_db_file_version = false;
            if (File.Exists(persistDbName))
                found_latest_db_file_version = true;

            var dir_info = new DirectoryInfo(appDataFolder);
            foreach (var file in dir_info.EnumerateFiles($@"{productName}*.db"))
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

            ReadDataDumpSafe();
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
            }
            );

            foregroundTimer = new Timer(state =>
            {
                if (!sessionActive) //disable foreground event handling
                    return;

                IntPtr hwnd = foreGroundWindow;
                if (!User32.IsWindow(hwnd))
                    return;

                if (noRestoreWindows.Contains(hwnd))
                    return;

                bool ctrl_key_pressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
                bool alt_key_pressed = (User32.GetKeyState(0x12) & 0x8000) != 0;
                bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;

                int leftClicks = leftButtonClicks;
                leftButtonClicks = 0;

                if (realForeGroundWindow == vacantDeskWindow)
                {
                    if (leftClicks != 1)
                        return;

                    if (!ctrl_key_pressed && !alt_key_pressed)
                    {
                        //restore window to previous background position
                        SwitchForeBackground(hwnd, secondBackGround:shift_key_pressed);
                    }
                    else if (ctrl_key_pressed && !alt_key_pressed)
                    {
                        //restore to previous background zorder with current size/pos
                        SwitchForeBackground(hwnd, updateBackgroundPos: true, secondBackGround:shift_key_pressed);
                    }
                    else if (!ctrl_key_pressed && alt_key_pressed && !shift_key_pressed)
                    {
                        FgWindowToBottom();
                    }
                }
                else if (!ctrl_key_pressed && !shift_key_pressed)
                {
                    if (fullScreenGamingWindows.Contains(hwnd))
                        return;

                    ActivateWindow(hwnd); //window could be active on alt-tab
                    if (IsFullScreen(hwnd) || IsRdpWindow(hwnd))
                    {
                        if (User32.IsWindowVisible(HotKeyWindow.commanderWnd))
                        {
                            RECT hkwinPos = new RECT();
                            User32.GetWindowRect(HotKeyWindow.commanderWnd, ref hkwinPos);

                            RECT fgwinPos = new RECT();
                            User32.GetWindowRect(hwnd, ref fgwinPos);

                            RECT intersect = new RECT();
                            bool overlap = User32.IntersectRect(out intersect, ref hkwinPos, ref fgwinPos);
                            if (overlap)
                            {
                                User32.ShowWindow(HotKeyWindow.commanderWnd, (int)ShowWindowCommands.Hide);
                            }
                        }
                    }

                    if (!alt_key_pressed)
                    {
                        /*
                        if (pendingMoveEvents.Count > 1)
                            return;
                        */

                        //restore window to previous foreground position
                        SwitchForeBackground(hwnd, toForeground: true);
                    }
                }

                if (freezeCapture || !monitorApplications.ContainsKey(curDisplayKey) || !monitorApplications[curDisplayKey].ContainsKey(hwnd))
                    return;

                if (normalSessions.Contains(curDisplayKey))
                    CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture:true);
            });

            captureTimer = new Timer(state =>
            {
                userMovePrev = userMove;
                userMove = false;

                if (!sessionActive)
                    return;

                if (restoringFromMem)
                    return;

                if (fullScreenGamingWindow == foreGroundWindow)
                {
                    fullScreenGamingWindows.Add(fullScreenGamingWindow);
                    fullScreenGamingWindow = IntPtr.Zero;
                    return;
                }

                if (fullScreenGamingWindows.Contains(foreGroundWindow))
                    return;

                Log.Trace("Capture timer expired");
                BatchCaptureApplicationsOnCurrentDisplays();
            });

            restoreTimer = new Timer(state => { TimerRestore(); });

            restoreFinishedTimer = new Timer(state =>
            {
                int numWindowRestored = restoredWindows.Count;
                int restorePass = restoreTimes;

                unResponsiveWindows.Clear();
                noRecordWindows.Clear();

                bool wasRestoringFromDB = restoringFromDB;
                restoringFromDB = false;
                autoInitialRestoreFromDB = false;
                restoringFromMem = false;
                bool wasRestoringSnapshot = restoringSnapshot;
                restoringSnapshot = false;
                exitFullScreenGaming = false;
                ResetState();

                Log.Trace("");
                Log.Trace("");
                bool checkUpgrade = true;
                string displayKey = GetDisplayKey();
                if (restoreHalted || !displayKey.Equals(curDisplayKey))
                {
                    restoreHalted = false;
                    topmostWindowsFixed.Clear();

                    Log.Error("Restore aborted for {0}", curDisplayKey);

                    curDisplayKey = displayKey;
                    if (fullScreenGamingWindows.Contains(foreGroundWindow) || !normalSessions.Contains(curDisplayKey))
                    {
                        Log.Event("no need to restore fresh session {0}", curDisplayKey);
                        User32.GetCursorPos(out initCursorPos);

                        checkUpgrade = false;

                        //restore icon to idle
                        hideRestoreTip();
                        iconBusy = false;
                        sessionActive = true;
                    }
                    else
                    {
                        // do restore again, while keeping previous capture time unchanged
                        Log.Event("Restart restore for {0}", curDisplayKey);
                        restoringFromMem = true;
                        StartRestoreTimer();
                        return;
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

                    if (!wasRestoringSnapshot && !wasRestoringFromDB)
                    {
                        WriteDataDump();

                        if (!snapshotTakenTime.ContainsKey(curDisplayKey))
                            snapshotTakenTime[curDisplayKey] = new Dictionary<int, DateTime>();
                        if (snapshotTakenTime[curDisplayKey].ContainsKey(MaxSnapshots))
                            snapshotTakenTime[curDisplayKey][MaxSnapshots - 2] = snapshotTakenTime[curDisplayKey][MaxSnapshots];
                    }

                    if (wasRestoringSnapshot || noRestoreWindowsTmp.Count > 0)
                        CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture: true);
                    else
                        StartCaptureTimer();
                }

                bool db_exist = false;
                try
                {
                    lock(dbLock)
                    using (var persistDB = new LiteDatabase(persistDbName))
                    {
                        db_exist = persistDB.CollectionExists(curDisplayKey);
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e.ToString());
                }

                enableRestoreMenu(db_exist, checkUpgrade);
                freezeCapture = false;

                bool snapshot_exist = SnapshotExists(curDisplayKey);
                enableRestoreSnapshotMenu(snapshot_exist);
                //changeIconText(null);

                noRestoreWindowsTmp.Clear();

                process.PriorityClass = processPriority;

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
                    if (!freezeCapture)
                    {
                        lastDisplayChangeTime = DateTime.Now;
                        EndDisplaySession();
                        freezeCapture = true;
                    }
                };
            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    string displayKey = GetDisplayKey();
                    Log.Event("Display settings changed {0}", displayKey);

                    // rewind disqualified capture time
                    UndoCapture(lastDisplayChangeTime);

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
                                CleanupDisplayRegKey(displayKey);
                            }
                        }
                        else
                        {
                            if (showDesktop)
                                ShowDesktop();

                            // change display on the fly
                            Shell32.QUERY_USER_NOTIFICATION_STATE pquns;
                            int error = Shell32.SHQueryUserNotificationState(out pquns);
                            if (normalSessions.Contains(displayKey))
                            {
                                curDisplayKey = displayKey;
                                if (promptSessionRestore)
                                {
                                    PromptSessionRestore();
                                }
                                if (autoRestoreLiveWindows && !monitorApplications.ContainsKey(displayKey))
                                {
                                    CaptureApplicationsOnCurrentDisplays(displayKey, immediateCapture: true);
                                    Log.Event("auto restore from db");
                                    restoringFromDB = true;
                                    autoInitialRestoreFromDB = true;
                                    dbDisplayKey = curDisplayKey;
                                    StartRestoreTimer();
                                }
                                else
                                {
                                    restoringFromMem = true;
                                    StartRestoreTimer(milliSecond: UserForcedRestoreLatency > 0 ? UserForcedRestoreLatency : RestoreLatency);
                                }
                            }
                            else if (error == 0 && pquns.HasFlag(Shell32.QUERY_USER_NOTIFICATION_STATE.QUNS_RUNNING_D3D_FULL_SCREEN))
                            {
                                if (IsNewWindow(foreGroundWindow))
                                {
                                    fullScreenGamingWindow = foreGroundWindow;
                                    Log.Event($"enter full-screen gaming mode {displayKey} {GetWindowTitle(foreGroundWindow)}");
                                }
                                else
                                    Log.Event($"re-enter full-screen gaming mode");

                                StartRestoreFinishedTimer(0);
                            }
                            else
                            {
                                StartRestoreFinishedTimer(0);
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
                            UndoCapture(DateTime.Now);
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
            remoteSession = System.Windows.Forms.SystemInformation.TerminalServerSession;
            bool sshot_exist = SnapshotExists(curDisplayKey);
            enableRestoreSnapshotMenu(sshot_exist);
            Log.Event($"Display config is {curDisplayKey}");
            using (var persistDB = new LiteDatabase(persistDbName))
            {
                bool db_exist = persistDB.CollectionExists(curDisplayKey);
                enableRestoreMenu(db_exist, true);
                normalSessions.Add(curDisplayKey);
                var collectionNames = persistDB.GetCollectionNames();
                foreach (var item in collectionNames)
                {
                    normalSessions.Add(item);
                }
                if (db_exist && auto_restore_from_db)
                {
                    restoringFromDB = true;
                    dbDisplayKey = curDisplayKey;
                    StartRestoreTimer();
                }
                else if (db_exist && autoRestoreLiveWindows && !RestoreExists(curDisplayKey))
                {
                    Log.Event("auto restore from db");
                    restoringFromDB = true;
                    autoInitialRestoreFromDB = true;
                    dbDisplayKey = curDisplayKey;
                    StartRestoreTimer();
                }
            }

            return true;
        }

        public List<String> GetDbCollections()
        {
            using (var persistDB = new LiteDatabase(persistDbName))
            {
                var collectionNames = persistDB.GetCollectionNames();
                var lst = new List<String>();
                foreach (var item in collectionNames)
                {
                    lst.Add(item);
                }
                lst.Sort(delegate (String s, String t)
                {
                    return s.CompareTo(t);
                });
                return lst;
            }
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

        public void SetDebugProcess(string debug_process)
        {
            string[] ps = debug_process.Split(';');
            foreach (var p in ps)
            {
                var s = p;
                if (s.EndsWith(".exe"))
                    s = s.Substring(0, s.Length - 4);
                debugProcess.Add(s);
            }
        }

        private void PromptSessionRestore()
        {
            if (pauseAutoRestore)
                return;

            sessionActive = false; // no new capture
            pauseAutoRestore = true;

            User32.SetThreadDpiAwarenessContextSafe();

            System.Windows.Forms.MessageBox.Show("Proceed to restore windows",
                System.Windows.Forms.Application.ProductName,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information,
                System.Windows.Forms.MessageBoxDefaultButton.Button1,
                System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly
            );

            pauseAutoRestore = false;
        }

        private bool IsNewWindow(IntPtr hwnd)
        {
            if (noRestoreWindows.Contains(hwnd))
                return false;

            foreach (var key in monitorApplications.Keys)
            {
                if (monitorApplications[key].ContainsKey(hwnd))
                    return false;
            }
            return true;
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

        private static string GetWindowTitle(IntPtr hwnd, bool use_cache = true)
        {
            if (use_cache && windowTitle.ContainsKey(hwnd))
                return windowTitle[hwnd];

            var length = User32.GetWindowTextLength(hwnd);
            if (length > 0)
            {
                length++;
                var title = new StringBuilder(length);
                User32.GetWindowText(hwnd, title, length);
                var t = title.ToString();
                t = t.Trim();
                return t;
            }

            //return hwnd.ToString("X8");
            return "";
        }

        private static bool IsMinimized(IntPtr hwnd)
        {
            bool result = User32.IsIconic(hwnd) || !User32.IsWindowVisible(hwnd);
            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            if ((style & (long)WindowStyleFlags.MINIMIZE) != 0L)
            {
                result = true;
            }

            return result;
        }

        private bool IsRectOffScreen(RECT rect)
        {
            const int MinSize = 10;
            POINT topLeft = new POINT(rect.Left + MinSize, rect.Top + MinSize);
            if (User32.MonitorFromPoint(topLeft, User32.MONITOR_DEFAULTTONULL) != IntPtr.Zero)
            {
                return false;
            }
            Log.Error("top left {0} is off-screen", topLeft.ToString());

            POINT topRight = new POINT(rect.Left + rect.Width - MinSize, rect.Top + MinSize);
            if (User32.MonitorFromPoint(topRight, User32.MONITOR_DEFAULTTONULL) != IntPtr.Zero)
            {
                return false;
            }

            Log.Error("top right {0} is off-screen", topRight.ToString());
            return true;
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

            bool offscreen = IsRectOffScreen(rect);
            if (offscreen)
                Log.Error("{0} is off-screen, Rect = {1}", GetWindowTitle(hwnd), rect.ToString());

            return offscreen;
        }

        public void CenterWindow(IntPtr hwnd)
        {
            IntPtr desktopWindow = User32.GetDesktopWindow();
            RECT target_rect = new RECT();
            User32.GetWindowRect(desktopWindow, ref target_rect);
            User32.MoveWindow(hwnd, target_rect.Left + target_rect.Width / 4, target_rect.Top + target_rect.Height / 4, target_rect.Width / 2, target_rect.Height / 2, true);
        }

        public bool RecallLastPositionKilledWindow(IntPtr hwnd)
        {
            IntPtr kid = FindMatchingKilledWindow(hwnd);
            if (kid == IntPtr.Zero)
                return false;

            var d = deadApps[curDisplayKey][kid].Last<ApplicationDisplayMetrics>();
            var r = d.ScreenPosition;

            User32.MoveWindow(hwnd, r.Left, r.Top, r.Width, r.Height, true);
            User32.SetForegroundWindow(hwnd);
            Log.Error("Recover last closing location \"{0}\"", GetWindowTitle(hwnd));

            return true;
        }

        public void RecallLastPosition(IntPtr hwnd)
        {
            int cnt = monitorApplications[curDisplayKey][hwnd].Count;
            if (cnt < 2)
                return;
            var d = monitorApplications[curDisplayKey][hwnd][cnt - 1];
            var r = d.ScreenPosition;
            User32.MoveWindow(hwnd, r.Left, r.Top, r.Width, r.Height, true);
            User32.SetForegroundWindow(hwnd);
            Log.Error("Restore last location \"{0}\"", GetWindowTitle(hwnd));
        }

        private void InheritKilledWindow(IntPtr hwnd, IntPtr kid)
        {
            uint pid;
            User32.GetWindowThreadProcessId(hwnd, out pid);

            foreach (var display_key in deadApps.Keys)
            {
                if (deadApps[display_key].ContainsKey(kid))
                {
                    //update new process id
                    for (int i = 0; i < deadApps[display_key][kid].Count; i++)
                    {
                        deadApps[display_key][kid][i].ProcessId = pid;
                    }

                    IntPtr dead_hwnd = kid;

                    if (!monitorApplications.ContainsKey(display_key))
                        monitorApplications[display_key] = new Dictionary<IntPtr, List<ApplicationDisplayMetrics>>();

                    //replace prev zorder reference of dead_hwnd with hwnd in monitorApplication
                    foreach (var hw in monitorApplications[display_key].Keys)
                    {
                        for (int i = 0; i < monitorApplications[display_key][hw].Count; i++)
                        {
                            if (monitorApplications[display_key][hw][i].PrevZorderWindow == dead_hwnd)
                                monitorApplications[display_key][hw][i].PrevZorderWindow = hwnd;
                        }
                    }

                    monitorApplications[display_key][hwnd] = deadApps[display_key][kid];
                    deadApps[display_key].Remove(kid);

                    //replace prev zorder reference in deadApps as well
                    foreach (var kd in deadApps[display_key].Keys)
                    {
                        for (int i = 0; i < deadApps[display_key][kd].Count; i++)
                        {
                            if (deadApps[display_key][kd][i].PrevZorderWindow == dead_hwnd)
                                deadApps[display_key][kd][i].PrevZorderWindow = hwnd;
                        }
                    }
                }
            }
        }

        private IntPtr FindMatchingKilledWindow(IntPtr hwnd)
        {
            if (!deadApps.ContainsKey(curDisplayKey))
                return IntPtr.Zero;

            string className = GetWindowClassName(hwnd);
            if (string.IsNullOrEmpty(className))
                return IntPtr.Zero;

            if (!windowProcessName.ContainsKey(hwnd))
                return IntPtr.Zero;

            string procName = windowProcessName[hwnd];
            string title = GetWindowTitle(hwnd);
            if (className.Equals("ApplicationFrameWindow"))
            {
                //retrieve info about windows core app hidden under top window
                IntPtr realHwnd = GetCoreAppWindow(hwnd);
                className = GetWindowClassName(realHwnd);
                title = GetWindowTitle(realHwnd);
            }

            if (!string.IsNullOrEmpty(className))
            {
                IntPtr dflt_kid = IntPtr.Zero;

                var deadAppPos = deadApps[curDisplayKey];
                foreach (var kid in deadAppPos.Keys)
                {
                    var appPos = deadAppPos[kid].Last<ApplicationDisplayMetrics>();

                    if (!className.Equals(appPos.ClassName))
                        continue;
                    if (!procName.Equals(appPos.ProcessName))
                        continue;

                    // match position first
                    RECT r = appPos.ScreenPosition;
                    RECT rect = new RECT();
                    User32.GetWindowRect(hwnd, ref rect);
                    if (rect.Equals(r))
                        return kid;

                    // lastly match title
                    if (title.Equals(appPos.Title))
                        return kid;

                    if (dflt_kid == IntPtr.Zero)
                        dflt_kid = kid;
                }

                if (dflt_kid != IntPtr.Zero)
                    return dflt_kid;
            }

            return IntPtr.Zero;
        }

        private void FixOffScreenWindow(IntPtr hwnd)
        {
            var displayKey = GetDisplayKey();
            if (!normalSessions.Contains(displayKey))
            {
                Log.Error("Avoid recover invisible window \"{0}\"", GetWindowTitle(hwnd));
                return;
            }

            if (RecallLastPositionKilledWindow(hwnd))
                return;

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
            else if (!IsCoreUiWindow(hwnd))
            {
                User32.MoveWindow(hwnd, rectDesk.Left + 100, rectDesk.Top + 100, rect.Width, rect.Height, true);
                Log.Error("Auto fix invisible window \"{0}\"", GetWindowTitle(hwnd));
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

        //return true if action is taken
        private void ActivateWindow(IntPtr hwnd)
        {
            if (IsBrowserWindow(hwnd))
            {
                IntPtr topHwnd = User32.GetAncestor(hwnd, User32.GetAncestorRoot);
                if (hwnd == topHwnd)
                    HotKeyWindow.BrowserActivate(topHwnd, in_restore : restoringFromMem);
            }
            else
               HotKeyWindow.BrowserActivate(hwnd, false);

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
                    var diff = prevDisplayMetrics.CaptureTime.Subtract(lastUnminimizeTime);
                    if (diff.TotalMilliseconds > 0 && diff.TotalMilliseconds < 400)
                    {
                        //discard fast capture of unminimize action
                        int last_elem_idx = monitorApplications[curDisplayKey][hwnd].Count - 1;
                        if (last_elem_idx == 0)
                            return;
                        monitorApplications[curDisplayKey][hwnd].RemoveAt(last_elem_idx);
                        var lastMetrics = monitorApplications[curDisplayKey][hwnd].Last<ApplicationDisplayMetrics>();
                        if (!lastMetrics.IsFullScreen)
                        {
                            monitorApplications[curDisplayKey][hwnd].Add(prevDisplayMetrics);
                            return;
                        }

                        prevDisplayMetrics = lastMetrics;
                    }

                    RECT target_rect = prevDisplayMetrics.ScreenPosition;
                    if (prevDisplayMetrics.IsFullScreen)
                    {
                        //the window was minimized from full screen status
                        //it is possible that minimize status have not been captured yet

                        //restore fullscreen window only applies if screen resolution has changed since minimize/normalize
                        if (prevDisplayMetrics.CaptureTime < lastDisplayChangeTime)
                            RestoreFullScreenWindow(hwnd, target_rect);
                        return;
                    }

                    if (prevDisplayMetrics.IsMinimized)
                    {
                        if (!IsFullScreen(hwnd) || IsWrongMonitor(hwnd, target_rect))
                        {
                            RECT screenPosition = new RECT();
                            User32.GetWindowRect(hwnd, ref screenPosition);

                            if (prevDisplayMetrics.WindowPlacement.ShowCmd == ShowWindowCommands.ShowMinimized
                               || prevDisplayMetrics.WindowPlacement.ShowCmd == ShowWindowCommands.Minimize
                               || target_rect.Left <= -25600)
                            {
                                Log.Error("no qualified position data to restore minimized window \"{0}\"", GetWindowTitle(hwnd));
                                return; // captured without previous history info, let OS handle it
                            }

                            if (screenPosition.Equals(target_rect))
                                return;

                            if (fixUnminimizedWindow && !tidyTabWindows.Contains(hwnd))
                            {
                                //restore minimized window only applies if screen resolution has changed since minimize
                                if (prevDisplayMetrics.CaptureTime < lastDisplayChangeTime)
                                {
                                    long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
                                    if ((style & (long)WindowStyleFlags.CAPTION) == 0L)
                                    {
                                        return;
                                    }

                                    // windows ignores previous snap status when activated from minimized state
                                    var placement = prevDisplayMetrics.WindowPlacement;
                                    if (placement.ShowCmd == ShowWindowCommands.Maximize)
                                    {
                                        //restore normal first
                                        placement.ShowCmd = ShowWindowCommands.ShowNoActivate;
                                        User32.SetWindowPlacement(hwnd, ref placement);
                                        placement.ShowCmd = ShowWindowCommands.Maximize;
                                        Log.Error("pre-restore minimized max window \"{0}\"", GetWindowTitle(hwnd));
                                    }
                                    User32.SetWindowPlacement(hwnd, ref placement);
                                    User32.MoveWindow(hwnd, target_rect.Left, target_rect.Top, target_rect.Width, target_rect.Height, true);
                                    Log.Error("restore minimized window \"{0}\"", GetWindowTitle(hwnd));
                                    return;
                                }
                            }

                            if (!enable_offscreen_fix)
                                return;

                            if (IsOffScreen(hwnd))
                            {
                                CenterWindow(hwnd);
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


        private static bool IsTopLevelWindow(IntPtr hwnd)
        {
            if (IsTaskBar(hwnd))
                return true;

            if (User32.GetAncestor(hwnd, User32.GetAncestorParent) != desktopWindow)
                return false;

            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            return (style & (long)WindowStyleFlags.MINIMIZEBOX) != 0L
                || (style & (long)WindowStyleFlags.SYSMENU) != 0L;
        }

        private bool CaptureProcessName(IntPtr hwnd)
        {
            if (!windowProcessName.ContainsKey(hwnd))
            {
                string processName;
                var process = GetProcess(hwnd);
                if (process == null)
                {
                    windowProcessName.Add(hwnd, "unrecognized_process");
                }
                else
                {
                    try
                    {
                        processName = process.ProcessName;
                        if (!windowProcessName.ContainsKey(hwnd))
                            windowProcessName.Add(hwnd, processName);
                    }
                    catch(Exception ex)
                    {
                        Log.Error(ex.ToString());
                        //process might have been terminated
                        return false;
                    }
                }
            }

            return true;

        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                WinEventProcCore(hWinEventHook, eventType, hwnd, idObject, idChild, dwEventThread, dwmsEventTime);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private void WinEventProcCore(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!initialized)
                return;

            {
                switch (eventType)
                {
                    case User32Events.EVENT_SYSTEM_MENUSTART:
                    case User32Events.EVENT_SYSTEM_MENUEND:
                        if (idObject == 0 || idObject == -1)
                        {
                            //TODO:
                            //context sensitive menu
                        }
                        break;
                    case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                    case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                    case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                        // only care about child windows that are moved by user
                        //normalSessions.Add(curDisplayKey);
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
                debugWindows.Remove(hwnd);
                if (fullScreenGamingWindows.Contains(hwnd))
                {
                    fullScreenGamingWindows.Remove(hwnd);
                    fullScreenGamingWindow = IntPtr.Zero;
                    exitFullScreenGaming = true;
                }
                dualPosSwitchWindows.Remove(hwnd);

                bool found_history = false;
                foreach (var display_config in monitorApplications.Keys)
                {
                    if (!monitorApplications[display_config].ContainsKey(hwnd))
                        continue;

                    if (monitorApplications[display_config][hwnd].Count > 0)
                    {
                        found_history = true;

                        // save window size of closed app to restore off-screen window later
                        if (!deadApps.ContainsKey(display_config))
                        {
                            deadApps.Add(display_config, new Dictionary<IntPtr, List<ApplicationDisplayMetrics>>());
                        }

                        // for matching new window with killed one
                        monitorApplications[display_config][hwnd].Last().ProcessName = windowProcessName[hwnd];

                        deadApps[display_config][hwnd] = monitorApplications[display_config][hwnd];

                        windowTitle.Remove((IntPtr)monitorApplications[display_config][hwnd].Last().WindowId);
                        windowTitle.Remove(hwnd);

                        //limit deadApp size
                        while (deadApps[display_config].Count > 1024)
                        {
                            var keys = deadApps[display_config].Keys;
                            foreach (var kid in keys)
                            {
                                deadApps[display_config].Remove(kid);
                                break;
                            }
                        }
                    }

                    monitorApplications[display_config].Remove(hwnd);
                }

                if (found_history)
                {
                    lastKillTime = DateTime.Now;
                }

                windowProcessName.Remove(hwnd);

                bool found = windowTitle.Remove(hwnd);
                if (sessionActive && found)
                {
                    StartCaptureTimer(); //update z-order
                }

                return;
            }


            /* need invisible window event to detect session cut-off
            if (!User32.IsWindowVisible(hwnd))
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

            if (!CaptureProcessName(hwnd))
                return;

            if (ignoreProcess.Count > 0 || debugProcess.Count > 0)
            {
                string processName;
                processName = windowProcessName[hwnd];
                if (ignoreProcess.Contains(processName))
                    return;
                if (debugProcess.Contains(processName))
                {
                    debugWindows.Add(hwnd);
                }
            }

#if DEBUG
            if (title.Contains("Microsoft Visual Studio")
                && (eventType == User32Events.EVENT_OBJECT_LOCATIONCHANGE
                    || eventType == User32Events.EVENT_SYSTEM_FOREGROUND))
            {
                return;
            }

            /*
            if (debugProcess.Count > 0 && !debugWindows.Contains(hwnd))
            {
                return;
            }
            */
#endif

            // suppress capture for taskbar operation
            bool ctrl_key_pressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
            bool alt_key_pressed = (User32.GetKeyState(0x12) & 0x8000) != 0;
            bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
            if (ctrl_key_pressed && alt_key_pressed)
                return;

            try
            {
                if (debugWindows.Contains(hwnd))
                {
                    Log.Trace("WinEvent received. Type: {0:x4}, Window: {1:x8}", (uint)eventType, hwnd.ToInt64());

                    RECT screenPosition = new RECT();
                    User32.GetWindowRect(hwnd, ref screenPosition);
                    var process = GetProcess(hwnd);
                    string log = string.Format("Received message of process {0} at ({1}, {2}) of size {3} x {4} with title: {5}",
                        (process == null) ? "" : process.ProcessName,
                        screenPosition.Left,
                        screenPosition.Top,
                        screenPosition.Width,
                        screenPosition.Height,
                        title
                        );
                    Log.Trace(log);
                }

                if (restoringFromMem)
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            if (restoringSnapshot)
                                return;
                            // let it trigger next restore
                            break;

                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                        case User32Events.EVENT_SYSTEM_MOVESIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                            noRestoreWindowsTmp.Add(hwnd);
                            break;

                        default:
                            // no capture during restore
                            return;
                    }

                    if (eventType == User32Events.EVENT_OBJECT_LOCATIONCHANGE)
                    {
                        if ((remoteSession || restoreTimes >= MinRestoreTimes) && !restoringSnapshot)
                        {
                            // restore is not finished as long as window location keeps changing
                            CancelRestoreFinishedTimer();
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
                                var cur_vdi = VirtualDesktop.GetWindowDesktopId(hwnd);
                                if (cur_vdi != Guid.Empty)
                                    curVirtualDesktop = cur_vdi;

                                if (restoringFromDB)
                                {
                                    // immediately capture new window
                                    //StartCaptureTimer(milliSeconds: 0);
                                    DateTime now = DateTime.Now;
                                    CaptureWindow(hwnd, eventType, now, curDisplayKey);
                                }
                                else
                                {
                                    if (IsTaskBar(hwnd))
                                        break;

                                    if ((User32.GetKeyState(1) & 0x8000) != 0)
                                        ++leftButtonClicks;

                                    realForeGroundWindow = hwnd;
                                    if (hwnd != vacantDeskWindow)
                                        foreGroundWindow = hwnd;

                                    foregroundTimer.Change(100, Timeout.Infinite);

                                    // Occasionaly OS might bring a window to foreground upon sleep
                                    // If the window move is initiated by OS (before sleep),
                                    // keep restart capture timer would eventually discard these moves
                                    // either by power suspend event handler calling CancelCaptureTimer()
                                    // or due to capture timer handler found too many window moves

                                    // If the window move is caused by user snapping window to screen edge,
                                    // delay capture by a few seconds should be fine.

                                    if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd))
                                        StartCaptureTimer(UserMoveLatency / 2);
                                    else
                                    {
                                        StartCaptureTimer();

                                        //speed up initial capture
                                        POINT cursorPos;
                                        User32.GetCursorPos(out cursorPos);
                                        if (!cursorPos.Equals(initCursorPos))
                                            userMove = true;
                                    }

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
                                    {
                                        if (hwnd != foreGroundWindow)
                                            pendingMoveEvents.Enqueue(hwnd);
                                    }

                                    if (foreGroundWindow == hwnd)
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
                            break;

                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                            lastUnminimizeTime = DateTime.Now;
                            lastUnminimizeWindow = hwnd;
                            tidyTabWindows.Remove(hwnd); //no longer hidden by tidytab

                            if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd))
                            {
                                //treat unminimized window as foreground
                                realForeGroundWindow = hwnd;
                                if (hwnd != vacantDeskWindow)
                                    foreGroundWindow = hwnd;
                                foregroundTimer.Change(100, Timeout.Infinite);

                                //capture with slight delay inperceivable by user, required for full screen mode recovery 
                                StartCaptureTimer(UserMoveLatency / 4);
                                Log.Trace("{0} {1}", eventType, GetWindowTitle(hwnd));
                                userMove = true;
                            }

                            break;

                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                            {
                                DateTime now = DateTime.Now;
                                var diff = now.Subtract(lastUnminimizeTime);
                                if (diff.TotalMilliseconds < 200)
                                {
                                    Log.Error($"window \"{title}\" is hidden by tidytab");
                                    tidyTabWindows.Add(hwnd);
                                    if (lastUnminimizeWindow != IntPtr.Zero)
                                        tidyTabWindows.Add(lastUnminimizeWindow);
                                }
                                foreGroundWindow = IntPtr.Zero;
                            }

                            if (enableMinimizeToTray)
                                MinimizeToTray.Create(hwnd);

                            goto case User32Events.EVENT_SYSTEM_MOVESIZEEND;
                        case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                            if (eventType == User32Events.EVENT_SYSTEM_MOVESIZEEND)
                            {
                                moveTimer.Change(100, Timeout.Infinite);
                                if (!shift_key_pressed && !alt_key_pressed)
                                {
                                    if (ctrl_key_pressed)
                                        dualPosSwitchWindows.Add(hwnd);
                                    else
                                        dualPosSwitchWindows.Remove(hwnd);
                                }
                            }

                            // immediately capture user moves
                            // only respond to move of captured window to avoid miscapture
                            if (monitorApplications.ContainsKey(curDisplayKey) && monitorApplications[curDisplayKey].ContainsKey(hwnd) || allUserMoveWindows.Contains(hwnd))
                            {
                                StartCaptureTimer(UserMoveLatency / 4);
                                Log.Trace("{0} {1}", eventType, GetWindowTitle(hwnd));
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
            if (restoringSnapshot || restoringFromDB)
                return;

            if (monitorApplications.ContainsKey(curDisplayKey))
            {
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
                CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture: true);

                foreach (var hwnd in monitorApplications[curDisplayKey].Keys)
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

            WriteDataDump();

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
                CaptureApplicationsOnCurrentDisplays(curDisplayKey, immediateCapture: true);
                snapshotTakenTime[curDisplayKey][MaxSnapshots - 1] = DateTime.Now;
            }

            CancelRestoreTimer();
            CancelRestoreFinishedTimer();
            ResetState();

            restoringSnapshot = true;
            snapshotId = id;
            restoringFromMem = true;
            StartRestoreTimer(milliSecond: 0 /*wait mouse settle still for taskbar restore*/);
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

            IntPtr result = hWnd;
            IntPtr fail_safe_result = IntPtr.Zero;

            do
            {
                IntPtr result_prev = result;
                result = User32.GetWindow(result, 3);
                if (result == IntPtr.Zero)
                    break;
                if (result == result_prev)
                    break;
                if (result == HotKeyWindow.commanderWnd)
                    continue;

                if (monitorApplications[curDisplayKey].ContainsKey(result))
                {
                    if (IsMinimized(result))
                        continue;

                    if (IsFullScreen(result))
                        continue;

                    RECT prevRect = new RECT();
                    User32.GetWindowRect(result, ref prevRect);

                    RECT intersection = new RECT();
                    if (User32.IntersectRect(out intersection, ref rect, ref prevRect))
                        break;
                    fail_safe_result = result;
                }
            } while (true);

            if (result == IntPtr.Zero)
            {
                result = fail_safe_result;
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
            if (hWnd == HotKeyWindow.commanderWnd)
                return false;

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
            if (restoringFromDB)
                return false;

            return fixZorder == 2 || (restoringSnapshot && fixZorder > 0);
        }

        public static IntPtr GetForegroundWindow(bool strict = false)
        {
            if (strict)
                return User32.GetForegroundWindow();

            IntPtr topMostWindow = User32.GetTopWindow(desktopWindow);
            for (IntPtr hwnd = topMostWindow; hwnd != IntPtr.Zero; hwnd = User32.GetWindow(hwnd, 2))
            {
                // only track top level windows - but GetParent() isn't reliable for that check (because it can return owners)
                if (!IsTopLevelWindow(hwnd))
                    continue;

                if (noRestoreWindows.Contains(hwnd))
                    continue;

                if (IsTaskBar(hwnd))
                    continue;

                if (string.IsNullOrEmpty(GetWindowClassName(hwnd)))
                    continue;

                if (string.IsNullOrEmpty(GetWindowTitle(hwnd)))
                    continue;

                if (IsMinimized(hwnd))
                    continue;

                if (User32.IsWindow(hwnd))
                    return hwnd;
            }

            return IntPtr.Zero;
        }

        public void FgWindowToBottom()
        {
            IntPtr hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero)
                return;

            User32.SetWindowPos(hwnd, new IntPtr(1), //bottom
                0, 0, 0, 0,
                0
                | SetWindowPosFlags.DoNotActivate
                | SetWindowPosFlags.IgnoreMove
                | SetWindowPosFlags.IgnoreResize
            );
            Log.Event("Bring foreground window {0} to bottom", GetWindowTitle(hwnd));
        }

        public void SwitchForeBackground(IntPtr hwnd, bool toForeground=false, bool updateBackgroundPos=false, bool secondBackGround = false)
        {
            if (hwnd == IntPtr.Zero || IsTaskBar(hwnd))
                return;

            if (!enableDualPosSwitch)
                return;
            if (!dualPosSwitchWindows.Contains(hwnd))
                return;

            if (!monitorApplications.ContainsKey(curDisplayKey) || !monitorApplications[curDisplayKey].ContainsKey(hwnd))
                return;

            int prevIndex = monitorApplications[curDisplayKey][hwnd].Count - 1;
            var cur_metrics = monitorApplications[curDisplayKey][hwnd][prevIndex];
            if (cur_metrics.IsMinimized)
                return;

            IntPtr front_hwnd = cur_metrics.PrevZorderWindow;
            if (toForeground && IsTaskBar(front_hwnd))
                return; //already foreground

            IntPtr firstBackgroundWindow = IntPtr.Zero;

            for (; prevIndex >= 0; --prevIndex)
            {
                var metrics = monitorApplications[curDisplayKey][hwnd][prevIndex];
                if (!metrics.IsValid)
                {
                    continue;
                }

                IntPtr prevZwnd = metrics.PrevZorderWindow;
                if (prevZwnd != front_hwnd && (prevZwnd == IntPtr.Zero || prevZwnd != firstBackgroundWindow))
                {
                    if (toForeground)
                    {
                        if (metrics.IsFullScreen)
                            return;
                    }
                    else
                    {
                        if (secondBackGround)
                        {
                            firstBackgroundWindow = prevZwnd;
                            secondBackGround = false;
                            continue;
                        }
                        if (IsTaskBar(front_hwnd) && IsTaskBar(prevZwnd))
                            return; //#266, ignore taskbar (as prev-zwindow) change due to window maximize

                        RestoreZorder(hwnd, prevZwnd);
                        if (IsWindowTopMost(hwnd) && !metrics.IsTopMost)
                            FixTopMostWindow(hwnd);

                        if (updateBackgroundPos)
                        {
                            //update with current size/pos
                            monitorApplications[curDisplayKey][hwnd][prevIndex].ScreenPosition = cur_metrics.ScreenPosition;
                            monitorApplications[curDisplayKey][hwnd][prevIndex].WindowPlacement = cur_metrics.WindowPlacement;
                            break;
                        }
                    }

                    restoringFromMem = true;
                    RestoreApplicationsOnCurrentDisplays(curDisplayKey, hwnd, metrics.CaptureTime);
                    restoringFromMem = false;

                    break;
                }
            }
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

            bool ok = User32.SetWindowPos(
                hWnd,
                prev,
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
                if (debugWindows.Contains(hWnd))
                {
                    string log = string.Format("Captured {0,-8} at {1} '{2}' fullscreen:{3} minimized:{4}",
                        curDisplayMetrics,
                        curDisplayMetrics.ScreenPosition.ToString(),
                        curDisplayMetrics.Title,
                        curDisplayMetrics.IsFullScreen,
                        curDisplayMetrics.IsMinimized
                        );
                    Log.Event(log);

                    string log2 = string.Format("    WindowPlacement.NormalPosition at {0}",
                        curDisplayMetrics.WindowPlacement.NormalPosition.ToString());
                    Log.Event(log2);
                }

                bool new_window = !monitorApplications[displayKey].ContainsKey(hWnd);
                if (eventType != 0 || new_window)
                    curDisplayMetrics.IsValid = true;

                if (new_window)
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

        public string GetDisplayKey()
        {
            User32.SetThreadDpiAwarenessContextSafe(User32.DPI_AWARENESS_CONTEXT_UNAWARE);
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
            if (UserForcedCaptureLatency > 0)
            {
                captureTimer.Change(UserForcedCaptureLatency, Timeout.Infinite);
                return;
            }

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

        public void StartRestoreTimer(int milliSecond = RestoreLatency)
        {
            restoreTimer.Change(milliSecond, Timeout.Infinite);
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
                foreach (var hwnd in fullScreenGamingWindows)
                {
                    if (IsFullScreen(hwnd))
                        return;
                }

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

                if (saveToDB || (userMovePrev && !manualNormalSession))
                {
                    normalSessions.Add(curDisplayKey);
                    Log.Trace("normal session {0} due to user move", curDisplayKey, userMovePrev);
                }

                CaptureApplicationsOnCurrentDisplays(displayKey, saveToDB: saveToDB); //implies auto delayed capture
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }

        private void CaptureNewDisplayConfig(string displayKey)
        {
            if (!manualNormalSession)
                normalSessions.Add(displayKey);
            CaptureApplicationsOnCurrentDisplays(displayKey, immediateCapture: true);
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

                if (!snapshotTakenTime.ContainsKey(displayKey))
                    snapshotTakenTime[displayKey] = new Dictionary<int, DateTime>();
                if (snapshotTakenTime[displayKey].ContainsKey(MaxSnapshots))
                    snapshotTakenTime[displayKey][MaxSnapshots + 1] = snapshotTakenTime[displayKey][MaxSnapshots];
                snapshotTakenTime[displayKey][MaxSnapshots] = time;

                Log.Trace("Capture time {0}", time);
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private ApplicationDisplayMetrics GetLastValidMetrics(IntPtr hwnd)
        {
            if (!monitorApplications.ContainsKey(curDisplayKey))
                return null;
            if (!monitorApplications[curDisplayKey].ContainsKey(hwnd))
                return null;
            var dm = monitorApplications[curDisplayKey][hwnd].Last<ApplicationDisplayMetrics>();
            if (dm != null && dm.IsValid)
                return dm;
            return null;
        }

        private void UndoCapture(DateTime ref_time)
        {
            // rewind disqualified capture time
            if (snapshotTakenTime.ContainsKey(curDisplayKey) && snapshotTakenTime[curDisplayKey].ContainsKey(MaxSnapshots))
            {
                var lastCaptureTime = snapshotTakenTime[curDisplayKey][MaxSnapshots];
                var diff = ref_time - lastCaptureTime;
                if (diff.TotalMilliseconds < CaptureLatency)
                {
                    if (snapshotTakenTime[curDisplayKey].ContainsKey(MaxSnapshots + 1))
                    {
                        snapshotTakenTime[curDisplayKey][MaxSnapshots] = snapshotTakenTime[curDisplayKey][MaxSnapshots + 1];
                        Log.Error("undo capture of {0} at {1}", curDisplayKey, lastCaptureTime);
                    }
                }
            }
        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey, bool saveToDB = false, bool immediateCapture = false)
        {
            User32.SetThreadDpiAwarenessContextSafe(User32.DPI_AWARENESS_CONTEXT_UNAWARE);

            Log.Trace("");
            Log.Trace("Capturing windows for display setting {0}", displayKey);

            int pendingEventCnt = pendingMoveEvents.Count;
            pendingMoveEvents.Clear();

            var time_from_last_kill_window = DateTime.Now.Subtract(lastKillTime);
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

                            if (IsTopLevelWindow(hWnd))
                            {
                                curDisplayMetrics.Guid = VirtualDesktop.GetWindowDesktopId(hWnd);
                            }

                            if (curDisplayMetrics.ClassName.Equals("CabinetWClass"))
                                curDisplayMetrics.Dir = GetExplorerFolderPath(hWnd);

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

                    processCmd.Clear();
                }
            }
            else if (initialized && (time_from_last_kill_window.TotalMilliseconds < 200
                || (!userMovePrev && !immediateCapture && pendingEventCnt > MinWindowOsMoveEvents)))
            {
                // too many pending window moves, they are probably initiated by OS instead of user,
                // defer capture
                StartCaptureTimer();
                Log.Trace("defer capture");
            }
            else lock(restoreLock)
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
                    if (movedWindows > 0)
                    {
                        // confirmed user moves
                        RecordLastUserActionTime(time: DateTime.Now, displayKey: displayKey);
                        Log.Trace("{0} windows captured\n", movedWindows);
                    }
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

                if (noRestoreWindows.Contains(hwnd))
                    continue;

                if (IsTaskBar(hwnd))
                {
                    result.Add(hwnd);
                    if (!taskbarReady && GetRealTaskBar(hwnd) != IntPtr.Zero)
                    {
                        taskbarReady = true;
                        vacantDeskWindow = User32.FindWindowEx(desktopWindow, IntPtr.Zero, "Progman", "Program Manager");
                        //vacantDeskWindow = User32.FindWindowEx(vacantDeskWindow, IntPtr.Zero, "SHELLDLL_DefView", "");
                        //vacantDeskWindow = User32.FindWindowEx(vacantDeskWindow, IntPtr.Zero, "SysListView32", "FolderView");
                        //show icon on taskbar
                        hideRestoreTip();
                    }
                    continue;
                }

                if (string.IsNullOrEmpty(GetWindowClassName(hwnd)))
                    continue;

                if (string.IsNullOrEmpty(GetWindowTitle(hwnd)))
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
            IntPtr coreHwnd;
            coreHwnd = User32.FindWindowEx(hwnd, IntPtr.Zero, "Windows.UI.Core.AppWindow", null);
            if (coreHwnd != IntPtr.Zero)
                return coreHwnd;
            coreHwnd = User32.FindWindowEx(hwnd, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);
            if (coreHwnd != IntPtr.Zero)
                return coreHwnd;
            return hwnd;
        }

        // detect scale factor change back to 100% from 125%, 150%, 175%, 200%, 225%, 250% etc in User32.GetWindowRect()
        private bool IsScaleFactorChanged(int x, int y, int nx, int ny)
        {
            if (!rejectScaleFactorChange)
                return false;

            if (nx <= x || ny <= y)
                return false;

            double rx = nx * 4 / (double)x;
            double rem = rx - Math.Round(rx);
            if (Math.Abs(rem) > 0.005)
                return false; //not multiples of 25%

            double ry = ny * 4 / (double)y;
            if (Math.Abs(ry - rx) > 0.005)
                return false; //different aspect ratio

            return true;
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

            if (hwnd == HotKeyWindow.commanderWnd)
                return false;

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
            if (!CaptureProcessName(realHwnd))
                return false;

            bool isFullScreen = IsFullScreen(hwnd);

            curDisplayMetrics = new ApplicationDisplayMetrics
            {
                HWnd = hwnd,
                ProcessId = processId,

                // this function call is very CPU-intensive
                //ProcessName = window.Process.ProcessName,
                ProcessName = windowProcessName[realHwnd],
                ClassName = className,
                Title = isTaskBar ? "$taskbar$" : GetWindowTitle(realHwnd, use_cache: false),

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

            if (!windowTitle.ContainsKey(realHwnd))
            {
                if (noRestoreWindows.Contains(hwnd))
                    return false;

                IntPtr kid = FindMatchingKilledWindow(hwnd);
                if (kid != IntPtr.Zero)
                {
                    InheritKilledWindow(hwnd, kid);
                    Log.Error($"Inherit position data from killed window 0x{kid.ToString("X")} for {curDisplayMetrics.Title}");
                }

                //newly created window or new display setting
                curDisplayMetrics.WindowId = (uint)realHwnd;

                //if (!windowTitle.ContainsKey(hwnd))
                {
                    windowTitle[hwnd] = curDisplayMetrics.Title;
                    windowTitle[realHwnd] = curDisplayMetrics.Title;
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
            else if (!monitorApplications[displayKey].ContainsKey(hwnd))
            {
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
                    noRecordWindows.Add(hwnd);

                    if (restoringFromMem && monitorApplications[displayKey][hwnd].Count < 2)
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
                //curDisplayMetrics.ProcessName = prevDisplayMetrics.ProcessName;
                curDisplayMetrics.WindowId = prevDisplayMetrics.WindowId;

                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId
                    && prevDisplayMetrics.ClassName == curDisplayMetrics.ClassName)
                {
                    Log.Error("Window with title {0},{1}, process changed from {2} to {3}",
                        GetWindowTitle(hwnd), curDisplayMetrics.Title,
                        prevDisplayMetrics.ProcessId, curDisplayMetrics.ProcessId
                        );
                    windowTitle[hwnd] = curDisplayMetrics.Title;
                    moved = true;
                }
                else if (curDisplayMetrics.IsMinimized && !prevDisplayMetrics.IsMinimized)
                {
                    //minimize start
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
                    if (sessionActive)
                    {
                        //Log.Error("reject minimized window move {0}", GetWindowTitle(hwnd));
                        return false; //do not capture unexpected minimized window movement (by the app or OS)
                    }

                    //remain minimized
                    if (prevDisplayMetrics.IsFullScreen)
                    {
                        return false;
                    }

                    /* minimized mstsc window has null client rect too
                    var rect = new RECT();
                    User32.GetClientRect(hwnd, out rect);
                    if (rect.Width <= 0 || rect.Height <= 0)
                        return false;
                    */
                }

                if (!prevDisplayMetrics.EqualPlacement(curDisplayMetrics))
                {
                    curDisplayMetrics.NeedUpdateWindowPlacement = true;
                    moved = true;
                }
                else if (!prevDisplayMetrics.ScreenPosition.Equals(curDisplayMetrics.ScreenPosition))
                {
                    if (IsScaleFactorChanged(prevDisplayMetrics.ScreenPosition.Width, prevDisplayMetrics.ScreenPosition.Height,
                            curDisplayMetrics.ScreenPosition.Width, curDisplayMetrics.ScreenPosition.Height))
                    {
                        Log.Error($"Reject unexpected scale factor change for {GetWindowTitle(hwnd)}");
                        return false;
                    }
                    moved = true;
                }
                else if (!curDisplayMetrics.IsMinimized && prevDisplayMetrics.IsMinimized)
                {
                    //minimize end
                    moved = true;
                }

                if (restoringFromDB)
                {
                    if (IsTopLevelWindow(hwnd))
                    {
                        Guid curVd = VirtualDesktop.GetWindowDesktopId(hwnd);
                        if (curVd != Guid.Empty && prevDisplayMetrics.Guid != Guid.Empty)
                        {
                            if (curVd != prevDisplayMetrics.Guid)
                                return true;
                        }
                    }
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

            if (restoringFromDB || restoringSnapshot)
                normalSessions.Add(curDisplayKey);

            Log.Trace("Restore timer expired");

            process.PriorityClass = ProcessPriorityClass.High;

            lock (restoreLock)
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
                    StartRestoreFinishedTimer(haltRestore);
                }
                else if (restoreTimes < MaxRestoreTimes)
                {
                    bool extra_restore = false;

                    try
                    {
                        RemoveInvalidCapture();
                        extra_restore = RestoreApplicationsOnCurrentDisplays(displayKey, IntPtr.Zero, DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }

                    restoreTimes++;

                    bool slow_restore = remoteSession && !restoringSnapshot;
                    // force next restore, as Windows OS might not send expected message during restore
                    if (restoreTimes < (extra_restore ? MaxRestoreTimes : restoringSnapshot ? 1 : MinRestoreTimes))
                        StartRestoreTimer();
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

        private static string GetWindowClassName(IntPtr hwnd)
        {
            int nChars = 4096;
            StringBuilder buf = new StringBuilder(nChars);
            int chars = User32.GetClassName(hwnd, buf, nChars);
            return buf.ToString();
        }

        private bool IsCoreUiWindow(IntPtr hwnd)
        {
            string class_name = GetWindowClassName(hwnd);
            if (string.IsNullOrEmpty(class_name) || hwnd == GetCoreAppWindow(hwnd))
                return false;
            return true;
        }

        private static bool IsTaskBar(IntPtr hwnd)
        {
            if (!User32.IsWindow(hwnd))
                return false;

            if (!User32.IsWindowVisible(hwnd))
                return false;

            try
            {
                string class_name = GetWindowClassName(hwnd);
                return class_name.Equals("Shell_TrayWnd") || class_name.Equals("Shell_SecondaryTrayWnd");
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return false;
        }

        private bool IsCursorOnTaskbar()
        {
            POINT cursorPos;
            User32.GetCursorPos(out cursorPos);
            IntPtr wndUnderCursor = User32.WindowFromPoint(cursorPos);
            while (wndUnderCursor != IntPtr.Zero)
            {
                if (IsTaskBar(wndUnderCursor))
                    return true;
                wndUnderCursor = User32.GetParent(wndUnderCursor);
            }
            return false;
        }

        private bool IsWrongMonitor(IntPtr hwnd, RECT target_rect)
        {
            RECT cur_rect = new RECT();
            User32.GetWindowRect(hwnd, ref cur_rect);

            // #140, need extra check for wrong screen
            POINT middle = new POINT();
            middle.X = (cur_rect.Left + cur_rect.Right) / 2;
            middle.Y = (cur_rect.Top + cur_rect.Bottom) / 2;
            return !User32.PtInRect(ref target_rect, middle);
        }

        private void RestoreFullScreenWindow(IntPtr hwnd, RECT target_rect)
        {
            long style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            if ((style & (long)WindowStyleFlags.CAPTION) == 0L)
            {
                return;
                /*
                style |= (long)WindowStyleFlags.CAPTION;
                User32.SetWindowLong(hwnd, User32.GWL_STYLE, style);
                User32.ShowWindow(hwnd, User32.SW_RESTORE);
                Log.Error("restore caption style for {0}", GetWindowTitle(hwnd));
                */
            }

            bool wrong_screen = false;
            RECT cur_rect = new RECT();
            User32.GetWindowRect(hwnd, ref cur_rect);
            RECT intersect = new RECT();
            if (!User32.IntersectRect(out intersect, ref cur_rect, ref target_rect))
                wrong_screen = true;

            // #140, need extra check for wrong screen
            if (IsWrongMonitor(hwnd, target_rect))
                wrong_screen = true;

            if (wrong_screen)
            {
                User32.MoveWindow(hwnd, target_rect.Left, target_rect.Top, target_rect.Width, target_rect.Height, true);
                Log.Error("fix wrong screen for {0}", GetWindowTitle(hwnd));
            }

            RECT screenPosition = new RECT();
            User32.GetWindowRect(hwnd, ref screenPosition);

            // window caption center might be occupied by other controls 
            int centerx = screenPosition.Left + screenPosition.Width / 8;

            int centery = screenPosition.Top + 15;
            //User32.SetActiveWindow(hwnd);
            User32.SetCursorPos(centerx, centery);
            int double_clck_interval = System.Windows.Forms.SystemInformation.DoubleClickTime / 2;
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN | MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(double_clck_interval);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN | MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(double_clck_interval);

            style = User32.GetWindowLong(hwnd, User32.GWL_STYLE);
            if ((style & (long)WindowStyleFlags.CAPTION) == 0L)
            {
                Log.Error("restore full screen window {0}", GetWindowTitle(hwnd));
                return;
            }

            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN | MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
            Log.Error("double restore full screen window {0}", GetWindowTitle(hwnd));
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
                        && Math.Abs(intersect.Height - target_pos.Height) < 10)
                    {
                        User32.MoveWindow(hwnd, intersect.Left, intersect.Top, intersect.Width, intersect.Height, true);
                        Log.Error("restore snap window {0}", GetWindowTitle(hwnd));
                        break;
                    }
                }
            }
        }

        private void HideWindow(IntPtr hWnd)
        {
            User32.SendMessage(hWnd, User32.WM_SYSCOMMAND, User32.SC_MINIMIZE, null);
            uint style = (uint)User32.GetWindowLong(hWnd, User32.GWL_STYLE);
            style &= ~(uint)WindowStyleFlags.VISIBLE;
            User32.SetWindowLong(hWnd, User32.GWL_STYLE, style);
        }

        private void CenterCursor()
        {
            // center cursor
            IntPtr desktopWindow = User32.GetDesktopWindow();
            RECT rect = new RECT();
            User32.GetWindowRect(desktopWindow, ref rect);
            User32.SetCursorPos(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
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

            Log.Event($"move taskbar to {targetRect}");

            //IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            //User32.GetWindowRect(hReBar, ref screenPosition);

            //IntPtr hTaskBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
            //hTaskBar = User32.FindWindowEx(hTaskBar, IntPtr.Zero, "MSTaskListWClass", null);
            IntPtr hTaskBar = GetRealTaskBar(hwnd);
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

            CenterCursor();

            return true;
        }

        // recover height of horizontal taskbar or width of vertical taskbar
        private bool RecoverTaskBarArea(IntPtr hwnd, RECT targetRect)
        {
            if (remoteSession)
            {
                //attempt to restore taskbar size only once for rdp session
                if (restoreTimes != 2)
                    return false;
            }

            RECT sourceRect = new RECT();
            User32.GetWindowRect(hwnd, ref sourceRect);

            int deltaWidth = sourceRect.Width - targetRect.Width;
            int deltaHeight = sourceRect.Height - targetRect.Height;
            if (Math.Abs(deltaWidth) < 10 && Math.Abs(deltaHeight) < 10)
                return false;

            RECT intersect = new RECT();
            if (!User32.IntersectRect(out intersect, ref sourceRect, ref targetRect))
                return false;
            if (!intersect.Equals(sourceRect) && !intersect.Equals(targetRect))
                return false;

            List<Display> displays = GetDisplays();
            bool top_edge = false;
            bool left_edge = false;
            foreach (var display in displays)
            {
                RECT screen = display.Position;
                if (User32.IntersectRect(out intersect, ref sourceRect, ref screen))
                {
                    if (deltaWidth != 0 && Math.Abs(targetRect.Left - screen.Left) < 5)
                        left_edge = true;
                    if (deltaHeight != 0 && Math.Abs(targetRect.Top - screen.Top) < 5)
                        top_edge = true;
                    break;
                }
            }

            int start_y;
            int start_x;
            int end_x = -25600;
            int end_y = -25600;
            if (deltaWidth != 0)
            {
                //restore width
                Log.Error("restore width of taskbar window {0}", GetWindowTitle(hwnd));

                start_y = sourceRect.Top + sourceRect.Height / 2;
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
            }
            else
            {
                //restore height
                Log.Error("restore height of taskbar window {0}", GetWindowTitle(hwnd));

                start_x = sourceRect.Left + sourceRect.Width / 2;
                if (top_edge)
                {
                    //taskbar is on top edge
                    start_y = sourceRect.Top + sourceRect.Height - 1;
                    end_y = targetRect.Top + targetRect.Height - 1;
                }
                else
                {
                    //taskbar is on bottom edge
                    start_y = sourceRect.Top;
                    end_y = targetRect.Top;
                }
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
            if (deltaWidth != 0)
                User32.SetCursorPos(end_x, start_y);
            else
                User32.SetCursorPos(start_x, end_y);

            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);

            //move mouse to hide resize shape
            CenterCursor();

            return true;
        }

        private static IntPtr GetRealTaskBar(IntPtr hwnd)
        {
            IntPtr hTaskBar = IntPtr.Zero;
            IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            if (hReBar == IntPtr.Zero)
            {
                hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "WorkerW", null);
                if (hReBar != IntPtr.Zero)
                    hTaskBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskListWClass", null);
            }
            else
            {
                IntPtr hTBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
                if (hTBar != IntPtr.Zero)
                    hTaskBar = User32.FindWindowEx(hTBar, IntPtr.Zero, "MSTaskListWClass", null);
            }

            return hTaskBar;
        }

        private bool RestoreApplicationsOnCurrentDisplays(string displayKey, IntPtr sWindow, DateTime time)
        {
            bool extra_restore = false;

            if (!monitorApplications.ContainsKey(displayKey)
                || monitorApplications[displayKey].Count == 0)
            {
                // the display setting has not been captured yet
                return false;
            }

            User32.SetThreadDpiAwarenessContextSafe(User32.DPI_AWARENESS_CONTEXT_UNAWARE);

            Log.Info("");
            Log.Info("Restoring windows pass {0} for {1}", restoreTimes, displayKey);

            DateTime lastCaptureTime = time;

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

                // determine the time to be restored
                if (restoringSnapshot)
                {
                    if (!snapshotTakenTime.ContainsKey(curDisplayKey)
                        || !snapshotTakenTime[curDisplayKey].ContainsKey(snapshotId))
                        return false;

                    lastCaptureTime = snapshotTakenTime[curDisplayKey][snapshotId];
                }
                else if (snapshotTakenTime.ContainsKey(curDisplayKey)
                        && snapshotTakenTime[curDisplayKey].ContainsKey(MaxSnapshots))
                {
                    lastCaptureTime = snapshotTakenTime[displayKey][MaxSnapshots];
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
            if (restoringFromDB) using (var persistDB = new LiteDatabase(persistDbName))
            {
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(dbDisplayKey);
                for (int dbMatchLevel = 0; dbMatchLevel < 4; ++dbMatchLevel) foreach (var hWnd in sWindows)
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

                    if (windowTitle.ContainsKey(hWnd))
                    {
                        string title = windowTitle[hWnd];
                        if (dbMatchLevel == 0)
                        {
                            results = db.Find(x => x.ClassName == className && x.Title == title && x.ProcessId == processId && x.WindowId == oldDisplayMetrics.WindowId && x.ProcessName == processName);
                            curDisplayMetrics = SearchDb(results, rect, invisible);
                        }

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

                    if (windowTitle.ContainsKey(hWnd))
                    {
                        if (curDisplayMetrics == null && dbMatchLevel == 2)
                        {
                            string title = windowTitle[hWnd];
                            results = db.Find(x => x.Title == title && x.ProcessName == processName);
                            curDisplayMetrics = SearchDb(results, rect, invisible);
                        }
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
                    curDisplayMetrics.WindowId = (uint)realHwnd;
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
            if (sWindow == IntPtr.Zero)
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
                ApplicationDisplayMetrics prevDisplayMetrics = null;
                if (!IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics))
                    continue;
                if (prevDisplayMetrics == null)
                    continue;

                if (User32.IsHungAppWindow(hWnd) && !IsTaskBar(hWnd))
                {
                    Process process = GetProcess(hWnd);
                    if (process != null && !process.Responding)
                    {
                        Log.Error("restore unresponsive window {0}", GetWindowTitle(hWnd));
                        unResponsiveWindows.Add(hWnd);
                        //continue;
                    }
                }

                /*
                if (restoringFromDB)
                {
                    if (vd.Enabled() && IsTopLevelWindow(hWnd))
                    {
                        Guid curVd = vd.GetWindowDesktopId(hWnd);
                        if (curVd != Guid.Empty && prevDisplayMetrics.Guid != Guid.Empty)
                        {
                            if (curVd != prevDisplayMetrics.Guid)
                                vd.MoveWindowToDesktop(hWnd, prevDisplayMetrics.Guid);
                        }
                    }
                }
                */

                RECT rect = prevDisplayMetrics.ScreenPosition;
                WindowPlacement windowPlacement = prevDisplayMetrics.WindowPlacement;

                if (restoringFromDB)
                {
                    if (curDisplayKey != dbDisplayKey)
                    {
                        if (IsRectOffScreen(prevDisplayMetrics.WindowPlacement.NormalPosition))
                        {
                            Log.Error("skip restore {0} due to off-screen target position, Rect = {1}", GetWindowTitle(hWnd), rect.ToString());
                            continue;
                        }
                    }
                }

                if (IsTaskBar(hWnd))
                {
                    if (!fixTaskBar && !restoringFromDB && !restoringSnapshot)
                        continue;

                    if (fullScreenGamingWindows.Count > 0 || exitFullScreenGaming)
                        continue;

                    int taskbarMovable = (int)Registry.GetValue(@"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarSizeMove", 1);
                    if (taskbarMovable == 0)
                    {
                        User32.SendMessage(hWnd, User32.WM_COMMAND, User32.SC_TOGGLE_TASKBAR_LOCK, null);
                    }
                    bool changed_edge = MoveTaskBar(hWnd, rect);
                    bool changed_width = RecoverTaskBarArea(hWnd, rect);
                    if (changed_edge || changed_width)
                        restoredWindows.Add(hWnd);
                    if (taskbarMovable == 0)
                    {
                        User32.SendMessage(hWnd, User32.WM_COMMAND, User32.SC_TOGGLE_TASKBAR_LOCK, null);
                    }

                    continue;
                }

                //changeIconText($"Restore {GetWindowTitle(hWnd)}");

                if (prevDisplayMetrics.IsMinimized)
                {
                    if (prevDisplayMetrics.IsInvisible && User32.IsWindowVisible(hWnd))
                    {
                        // #239 IsWindowsMoved() detected difference in screen position
                        if (hWnd == HotKeyWindow.commanderWnd)
                        {
                            User32.ShowWindow(hWnd, (int)ShowWindowCommands.Hide);
                            continue;
                        }
                        HideWindow(hWnd);
                        Log.Error("keep invisible window {0}", GetWindowTitle(hWnd));
                        continue;
                    }
                    if (prevDisplayMetrics.IsInvisible || restoreTimes > 0)
                    {
                        bool action_taken = false;
                        if (!IsMinimized(hWnd))
                        {
                            User32.SendMessage(hWnd, User32.WM_SYSCOMMAND, User32.SC_MINIMIZE, null);
                            action_taken = true;
                        }

                        // second try
                        if (!IsMinimized(hWnd))
                        {
                            action_taken = true;
                            User32.ShowWindow(hWnd, (int)ShowWindowCommands.ShowMinNoActive);
                        }

                        if (action_taken)
                            Log.Error("keep minimized window {0}", GetWindowTitle(hWnd));
                        continue;
                    }
                }

                if (AllowRestoreZorder() && curDisplayMetrics.NeedClearTopMost)
                {
                    FixTopMostWindow(hWnd);
                    topmostWindowsFixed.Add(hWnd);
                }

                if (sWindow == IntPtr.Zero) //z-order for batch restore
                if (AllowRestoreZorder() && curDisplayMetrics.NeedRestoreZorder)
                {
                    extra_restore = true; //force next pass for topmost flag fix and zorder check

                    if (((fixZorderMethod >> restoreTimes) & 1) == 1)
                        batchZorderFix = true;
                    else
                        RestoreZorder(hWnd, prevDisplayMetrics.PrevZorderWindow);
                }

                bool success = true;

                bool need_move_window = true;
                bool restore_fullscreen = false;
                if (prevDisplayMetrics.IsFullScreen && !prevDisplayMetrics.IsMinimized)
                {
                    if (curDisplayMetrics.IsMinimized)
                    {
                        Log.Error("restore minimized window to full screen {0}", GetWindowTitle(hWnd));
                        need_move_window = false;
                        restore_fullscreen = true;
                        User32.ShowWindow(hWnd, (int)ShowWindowCommands.Normal);
                        IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics);
                        rect = prevDisplayMetrics.ScreenPosition;
                        windowPlacement = prevDisplayMetrics.WindowPlacement;
                    }
                }

                if (curDisplayMetrics.NeedUpdateWindowPlacement)
                {
                    // recover NormalPosition (the workspace position prior to snap)
                    if (prevDisplayMetrics.IsMinimized)
                    {
                        //restore minimized window button to correct taskbar
                        windowPlacement.ShowCmd = ShowWindowCommands.ShowNoActivate;
                        User32.SetWindowPlacement(hWnd, ref windowPlacement);
                        windowPlacement.ShowCmd = ShowWindowCommands.ShowMinNoActive;
                    }
                    else if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                    {
                        //restore maximized window to correct monitor
                        windowPlacement.ShowCmd = ShowWindowCommands.ShowNoActivate;
                        User32.SetWindowPlacement(hWnd, ref windowPlacement);
                        windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                    }
                    else if (prevDisplayMetrics.IsFullScreen)
                    {
                        Log.Error("recover full screen window {0}", GetWindowTitle(hWnd));
                        long style = User32.GetWindowLong(hWnd, User32.GWL_STYLE);
                        if (IsRdpWindow(hWnd) && ((style & (long)WindowStyleFlags.CAPTION)) != 0L)
                        {
                            //already has caption bar, bypass normal window move and go directly to mouse double click simulation
                            need_move_window = false;
                            restore_fullscreen = true;
                        }
                        else
                        {
                            need_move_window = true;
                            restore_fullscreen = true;

                            windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                            if (debugWindows.Contains(hWnd))
                            Log.Event("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                                prevDisplayMetrics.ProcessName,
                                windowPlacement.NormalPosition.Left,
                                windowPlacement.NormalPosition.Top,
                                windowPlacement.NormalPosition.Width,
                                windowPlacement.NormalPosition.Height,
                                success);
                        }
                    }

                    if (need_move_window)
                    {
                        success &= User32.SetWindowPlacement(hWnd, ref windowPlacement);
                    }
                }

                // recover previous screen position
                if (!prevDisplayMetrics.IsMinimized)
                {
                    if (need_move_window)
                    {
                        success &= User32.MoveWindow(hWnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                        if (debugWindows.Contains(hWnd))
                        Log.Event("MoveWindow({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                            prevDisplayMetrics.ProcessName,
                            rect.Left,
                            rect.Top,
                            rect.Width,
                            rect.Height,
                            success);
                    }

                    if (restore_fullscreen)
                    {
                        if (restoreTimes > 0 && sWindow == null) //#246, let other windows restore first
                        RestoreFullScreenWindow(hWnd, rect);
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
                }

                if (!success)
                {
                    string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    Log.Error(error);
                }
            }

            if (batchZorderFix)
            {
                HashSet<IntPtr> risky_windows = unResponsiveWindows;
                risky_windows.IntersectWith(noRecordWindows);
                if (risky_windows.Count == 0)
                try
                {
                    //changeIconText($"restore zorder");
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
                        bool isMoved = IsWindowMoved(displayKey, hWnd, 0, lastCaptureTime, out curDisplayMetrics, out prevDisplayMetrics);
                        if (restoringSnapshot && !isMoved && restoreTimes < 1)
                            continue; //do partial batch restore first
                        if (prevDisplayMetrics == null)
                            continue;

                        IntPtr prevZwnd = prevDisplayMetrics.PrevZorderWindow;
                        if (hWnd == prevZwnd)
                            prevZwnd = new IntPtr(1); //place at bottom to avoid dead loop

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

                if (AllowRestoreZorder() && curDisplayMetrics.NeedClearTopMost)
                {
                    FixTopMostWindow(hWnd);
                    topmostWindowsFixed.Add(hWnd);
                    extra_restore = true; //force next pass for topmost flag fix and zorder check
                }
            }

            Log.Trace("Restored windows position for display setting {0}", displayKey);

            if (restoringFromDB && restoreTimes == 0 && !autoInitialRestoreFromDB) using (var persistDB = new LiteDatabase(persistDbName))
            {
                HashSet<uint> dbMatchProcess = new HashSet<uint>(); // db entry (process id) matches existing window
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(dbDisplayKey);

                // launch missing process according to db
                var list = new List<ApplicationDisplayMetrics>(db.FindAll());
                if (VirtualDesktop.Enabled())
                {
                    //sort windows by virtual desktop
                    list.Sort(delegate (ApplicationDisplayMetrics adm1, ApplicationDisplayMetrics adm2)
                    {
                        if (adm1.Guid != adm2.Guid)
                            return adm1.Guid.ToString().CompareTo(adm2.Guid.ToString());
                        return 0;
                    });
                }

                var i = 0; //.bat file id
                bool yes_to_all = autoRestoreMissingWindows;
                foreach (var curDisplayMetrics in list)
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
                        if (VirtualDesktop.Enabled() && curDisplayMetrics.Guid != Guid.Empty && curDisplayMetrics.Guid != curVirtualDesktop)
                        {
                            System.Windows.Forms.MessageBox.Show("Switch to another virtual desktop to restore windows",
                                System.Windows.Forms.Application.ProductName,
                                System.Windows.Forms.MessageBoxButtons.OK,
                                System.Windows.Forms.MessageBoxIcon.Information,
                                System.Windows.Forms.MessageBoxDefaultButton.Button1,
                                System.Windows.Forms.MessageBoxOptions.DefaultDesktopOnly
                            );
                            VirtualDesktop.MoveWindowToDesktop(runProcessDlg.Handle, curDisplayMetrics.Guid);
                        }
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

                                bool is_window_apps = false;
                                if (processPath.Contains("Program Files\\WindowsApps"))
                                {
                                    is_window_apps = true;
                                    processPath = processPath.Replace("\"C:\\Program Files\\WindowsApps\\", "");
                                    int idx_slash = processPath.IndexOf('\\');
                                    processPath = processPath.Remove(idx_slash);
                                    int idx_underscore_begin = processPath.IndexOf('_');
                                    int idx_underscore_end = processPath.IndexOf("__");
                                    processPath = processPath.Remove(idx_underscore_begin, idx_underscore_end - idx_underscore_begin + 1);
                                    processPath = "shell:AppsFolder\\" + processPath + "!App";
                                }

                                if (!is_window_apps && processPath.Contains(" ") && !processPath.Contains("\"") && !processPath.Contains(".exe "))
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
                                //Process.Start(batFile);
                                string dir = curDisplayMetrics.Dir;
                                if (!String.IsNullOrEmpty(dir))
                                {
                                    if (dir.Contains(":") || dir.Contains("\\"))
                                    {
                                        //if (dir.Contains(" ") && !dir.Contains("\""))
                                        {
                                            dir = $"\"{dir}\"";
                                        }

                                        File.WriteAllText(batFile, "start \"\" /B " + dir);
                                    }
                                    else if (dir.Equals("This PC") || dir.Equals("Computer"))
                                    {
                                        File.WriteAllText(batFile, "explorer /n, /select, %SystemDrive%");
                                    }
                                    else
                                    {
                                        string home = System.Environment.GetEnvironmentVariable("USERPROFILE");
                                        string[] dirs = Directory.GetDirectories(home);

                                        bool found_in_home = false;
                                        foreach (string path in dirs)
                                        {
                                            string file = Path.GetFileName(path);
                                            if (dir == file)
                                            {
                                                found_in_home = true;
                                                break;
                                            }
                                        }
                                        if (!found_in_home)
                                        {
                                            Log.Error($"Could not locate folder {dir}, open home instead");
                                            dir = ".";
                                        }

                                        //if (dir.Contains(" ") && !dir.Contains("\""))
                                        {
                                            dir = $"\"{dir}\"";
                                        }

                                        File.WriteAllText(batFile, "cd %userprofile%" + Environment.NewLine + "start \"\" " + dir);
                                    }
                                }
                                else
                                {
                                    File.WriteAllText(batFile, "start \"\" /B " + processPath);
                                }

                                Process process = Process.Start("explorer.exe", batFile);

                                Thread.Sleep(2000);
                                //process.WaitForInputIdle();
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

            return extra_restore;
        }


        public static string GetProcExePath(uint proc_id)
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

        private static Process GetProcess(IntPtr hwnd)
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

        public static bool IsBrowserWindow(IntPtr hwnd)
        {
            string processName;
            if (windowProcessName.ContainsKey(hwnd))
            {
                processName = windowProcessName[hwnd];
            }
            else
            {
                var process = GetProcess(hwnd);
                if (process == null)
                    return false;
                try
                {
                    processName = process.ProcessName;
                }
                catch(Exception ex)
                {
                    Log.Error(ex.ToString());
                    //process might have been terminated
                    return false;
                }
            }
            return browserProcessNames.Contains(processName);
        }

        public static bool IsDesktopWindow(IntPtr hwnd)
        {
            IntPtr root = User32.GetAncestor(hwnd, User32.GetAncestorRoot);
            return root == vacantDeskWindow;
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

        private string GetExplorerFolderPath(IntPtr hwnd)
        {
            string path = "";

            try
            {
                IntPtr toolbar;
                toolbar = User32.FindWindowEx(hwnd, IntPtr.Zero, "WorkerW", null);
                toolbar = User32.FindWindowEx(toolbar, IntPtr.Zero, "ReBarWindow32", null);
                toolbar = User32.FindWindowEx(toolbar, IntPtr.Zero, "Address Band Root", null);
                toolbar = User32.FindWindowEx(toolbar, IntPtr.Zero, "msctls_progress32", null);
                toolbar = User32.FindWindowEx(toolbar, IntPtr.Zero, "Breadcrumb Parent", null);
                toolbar = User32.FindWindowEx(toolbar, IntPtr.Zero, "ToolbarWindow32", null);
                if (toolbar != IntPtr.Zero)
                {
                    path = GetWindowTitle(toolbar, use_cache: false);
                    if (path.StartsWith("Address: "))
                        path = path.Substring(9);
                }
            }
            catch(Exception ex)
            {
                Log.Error(ex.ToString());
            }

            return path;
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
            foreach (var thd in runningThreads)
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
                SystemEvents.DisplaySettingsChanging -= displaySettingsChangingHandler;
                SystemEvents.DisplaySettingsChanged -= displaySettingsChangedHandler;
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
