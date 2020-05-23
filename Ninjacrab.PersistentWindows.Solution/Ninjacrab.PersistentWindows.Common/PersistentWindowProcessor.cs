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
//using System.Windows.Forms;

using Microsoft.Win32;

using LiteDB;
using ManagedWinapi.Windows;

using Ninjacrab.PersistentWindows.Common.Diagnostics;
using Ninjacrab.PersistentWindows.Common.Models;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;


namespace Ninjacrab.PersistentWindows.Common
{
    public class PersistentWindowProcessor : IDisposable
    {
        // constant
        private const int RestoreLatency = 500; // milliseconds to wait for next pass of window position recovery
        private const int DefaultRestoreLatency = 2000; // restore latency in case display changed event is not generated
        private const int MaxRestoreLatency = 5000; // max milliseconds to wait after previous restore pass to tell if restore is finished
        private const int MinRestoreTimes = 2; // restores with fixed RestoreLatency
        private const int MaxRestoreTimesLocal = 4; // Max restores activated by further window event for local console session
        private const int MaxRestoreTimesRemote = 8; // for remote session

        private const int CaptureLatency = 3000; // milliseconds to wait for window position capture
        private const int MinOsMoveWindows = 4; // minimum number of moving windows to measure in order to recognize OS initiated move
        private const int MaxHistoryQueueLength = 20;

        // window position database
        private Dictionary<string, Dictionary<IntPtr, Queue<ApplicationDisplayMetrics>>> monitorApplications  //in-memory database
            = new Dictionary<string, Dictionary<IntPtr, Queue<ApplicationDisplayMetrics>>>();
        private LiteDatabase persistDB; //on-disk database

        // control shared by capture and restore
        private Object databaseLock = new Object(); // lock access to window position database
        private Object controlLock = new Object();

        // capture control
        private Timer captureTimer;
        private bool disableBatchCapture = false;
        private string validDisplayKeyForCapture = null;
        private HashSet<IntPtr> pendingCaptureWindows = new HashSet<IntPtr>();
        private Dictionary<IntPtr, string> windowTitle = new Dictionary<IntPtr, string>();

        // restore control
        private Timer restoreTimer;
        private Timer restoreFinishedTimer;
        private bool restoringWindowPos = false; // about to restore
        private int restoreTimes = 0;
        private int restoreNestLevel = 0; // nested call level
        public bool restoreFromDB = false;
        private Dictionary<string, int> multiwindowProcess = new Dictionary<string, int>()
            {
                // avoid launch process multiple times
                { "chrome", 0},
                { "firefox", 0 },
                { "opera", 0},
            };

        // session control
        private bool remoteSession = false;
        private bool sessionLocked = false; //requires password to unlock

        // display session end time
        private Dictionary<string, DateTime> sessionEndTime = new Dictionary<string, DateTime>();

        // callbacks
        public delegate void CallBack();
        public CallBack showRestoreTip;
        public CallBack hideRestoreTip;

        private PowerModeChangedEventHandler powerModeChangedHandler;
        private EventHandler displaySettingsChangingHandler;
        private EventHandler displaySettingsChangedHandler;
        private SessionSwitchEventHandler sessionSwitchEventHandler;

        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private User32.WinEventDelegate winEventsCaptureDelegate;

#if DEBUG
        private void DebugInterval()
        {
            ;
        }
#endif
        public bool Start()
        {
            string productName = System.Windows.Forms.Application.ProductName;
            string tempFolderPath = Path.GetTempPath();
#if DEBUG
            tempFolderPath = "."; //avoid db path conflict with release version
#endif
            // remove outdated db files
            var dir = new DirectoryInfo(tempFolderPath);
            var db_version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            foreach (var file in dir.EnumerateFiles($@"{productName}*.db"))
            {
                var fname = file.Name;
                if (!fname.Contains(db_version))
                {
                    try
                    {
                        file.Delete();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex.ToString());
                    }
                }
            }

            try
            {
                persistDB = new LiteDatabase($@"{tempFolderPath}/{productName}.{db_version}.db");
            }
            catch (Exception)
            {
                System.Windows.Forms.MessageBox.Show($"Only one instance of {productName} can be run!");
                return false;
            }

            validDisplayKeyForCapture = GetDisplayKey();
            BatchCaptureApplicationsOnCurrentDisplays();

#if DEBUG
            var debugTimer = new Timer(state =>
            {
                DebugInterval();
            });
            debugTimer.Change(2000, 2000);
#endif            

            captureTimer = new Timer(state =>
            {
                lock (controlLock)
                {
                    if (disableBatchCapture)
                    {
                        return;
                    }

                    if (pendingCaptureWindows.Count > MinOsMoveWindows)
                    {
                        RecordBatchCaptureTime(DateTime.Now);
                    }
                    pendingCaptureWindows.Clear();
                }

                Log.Trace("Capture timer expired");
                BatchCaptureApplicationsOnCurrentDisplays();
            });

            restoreTimer = new Timer(state =>
            {
                Log.Trace("Restore timer expired");
                BatchRestoreApplicationsOnCurrentDisplays();
            });

            restoreFinishedTimer = new Timer(state =>
            {
                Log.Trace("Restore Finished");
                restoringWindowPos = false;
                ResetState();
                RemoveBatchCaptureTime();

                // clear DbMatchWindow flag in db
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(validDisplayKeyForCapture);
                var results = db.Find(x => x.DbMatchWindow == true); // find process not yet started
                foreach (var curDisplayMetrics in results)
                {
                    curDisplayMetrics.DbMatchWindow = false;
                    db.Update(curDisplayMetrics);
                }

                hideRestoreTip();
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
                User32Events.EVENT_SYSTEM_MOVESIZEEND,
                User32Events.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // captures user restore window
            this.winEventHooks.Add(User32.SetWinEventHook(
                User32Events.EVENT_SYSTEM_MINIMIZESTART,
                User32Events.EVENT_SYSTEM_MINIMIZEEND, //window restored
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
                    Log.Info("Display settings changing {0}", displayKey);
                    lock (controlLock)
                    {
                        EndDisplaySession();
                    }
                };

            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    string displayKey = GetDisplayKey();
                    Log.Info("Display settings changed {0}", displayKey);

                    lock (controlLock)
                    {
                        if (sessionLocked)
                        {
                            //wait for session unlock to start restore
                        }
                        else
                        {
                            // change display on the fly
                            ResetState();
                            restoringWindowPos = true;
                            StartRestoreTimer();
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
                            Log.Info("System suspending");
                            lock (controlLock)
                            {
                                if (!sessionLocked)
                                {
                                    EndDisplaySession();
                                }
                            }
                            break;

                        case PowerModes.Resume:
                            Log.Info("System Resuming");
                            lock (controlLock)
                            {
                                if (!sessionLocked)
                                {
                                    // force restore in case OS does not generate display changed event
                                    restoringWindowPos = true;
                                    StartRestoreTimer(milliSecond : DefaultRestoreLatency);
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
                        Log.Trace("Session closing: reason {0}", args.Reason);
                        lock (controlLock)
                        {
                            sessionLocked = true;
                            EndDisplaySession();
                        }
                        break;
                    case SessionSwitchReason.SessionUnlock:
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        lock (controlLock)
                        {
                            sessionLocked = false;
                            // force restore in case OS does not generate display changed event
                            restoringWindowPos = true;
                            StartRestoreTimer();
                        }
                        break;

                    case SessionSwitchReason.RemoteDisconnect:
                    case SessionSwitchReason.ConsoleDisconnect:
                        Log.Trace("Session closing: reason {0}", args.Reason);
                        break;

                    case SessionSwitchReason.RemoteConnect:
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        remoteSession = true;
                        break;
                    case SessionSwitchReason.ConsoleConnect:
                        remoteSession = false;
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        break;
                }
            };

            SystemEvents.SessionSwitch += sessionSwitchEventHandler;

            return true;
        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!User32.IsTopLevelWindow(hwnd))
            {
                return;
            }

            var window = new SystemWindow(hwnd);
            if (window.Parent.HWnd.ToInt64() != 0)
            {
                // only track top level windows
                return;
            }

            if (eventType == User32Events.EVENT_OBJECT_DESTROY)
            {
                if (idObject != 0)
                {
                    // ignore non-window object (caret etc)
                    return;
                }

                lock (databaseLock)
                {
                    foreach (var displayKey in monitorApplications.Keys)
                    {
                        monitorApplications[displayKey].Remove(hwnd);
                    }

                    windowTitle.Remove(hwnd);
                }

                return;
            }

            // only track visible windows
            if (!window.Visible)
            {
                return;
            }

            // auto track taskbar
            if (string.IsNullOrEmpty(window.Title) && !IsTaskBar(window))
            {
                return;
            }

            try
            {
#if DEBUG
                if (window.Title.Contains("Microsoft Visual Studio")
                    && (eventType == User32Events.EVENT_OBJECT_LOCATIONCHANGE
                        || eventType == User32Events.EVENT_SYSTEM_FOREGROUND))
                {
                    return;
                }

                Log.Trace("WinEvent received. Type: {0:x4}, Window: {1:x8}", (uint)eventType, hwnd.ToInt64());

                RECT2 screenPosition = new RECT2();
                User32.GetWindowRect(hwnd, ref screenPosition);
                string log = string.Format("Received message of process {0} at ({1}, {2}) of size {3} x {4} with title: {5}",
                    window.Process.ProcessName,
                    screenPosition.Left,
                    screenPosition.Top,
                    screenPosition.Width,
                    screenPosition.Height,
                    window.Title
                    );
                Log.Trace(log);
#endif

                DateTime now = DateTime.Now;

                if (restoringWindowPos)
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            // let it trigger next restore
                            break;

                        default:
                            // no capture during restore
                            return;
                    }

                    lock (controlLock)
                    {
                        if (restoreTimes >= MinRestoreTimes)
                        {
                            // a new window move is initiated by OS instead of user during restore, restart restore timer
                            StartRestoreTimer();
                        }
                    }
                }
                else
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            lock (controlLock)
                            {
                                if (restoreFromDB)
                                {
                                    if (restoreTimes >= MinRestoreTimes)
                                    {
                                        // a new window move is initiated by OS instead of user during restore, restart restore timer
                                        StartRestoreTimer();
                                    }
                                }
                                else
                                {
                                    // can not tell if this event is caused by user snap operation or OS initiated closing session
                                    // wait for other user move events until capture timer expires
                                    if (pendingCaptureWindows.Count == 0)
                                    {
                                        StartCaptureTimer();
                                    }
                                    pendingCaptureWindows.Add(hwnd);
                                }
                            }
                            break;

                        case User32Events.EVENT_SYSTEM_FOREGROUND:
                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                        case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                            string displayKey = GetDisplayKey();
                            if (displayKey != validDisplayKeyForCapture)
                            {
                                disableBatchCapture = true;
                                Log.Trace("Discard capture for {0}, when expecting {1}", displayKey, validDisplayKeyForCapture);
                                break;
                            }

                            var thread = new Thread(() =>
                            {
                                try
                                {
                                    lock (databaseLock)
                                    {
                                        CaptureWindow(window, eventType, now, displayKey);
                                        if (eventType != User32Events.EVENT_SYSTEM_FOREGROUND)
                                        {
                                            if (!restoreFromDB)
                                            {
                                                RemoveBatchCaptureTime();
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.ToString());
                                }
                            });
                            thread.Start();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        private bool CaptureWindow(SystemWindow window, User32Events eventType, DateTime now, string displayKey, bool saveToDB = false)
        {
            bool ret = false;
            IntPtr hWnd = window.HWnd;

            if (!monitorApplications.ContainsKey(displayKey))
            {
                monitorApplications.Add(displayKey, new Dictionary<IntPtr, Queue<ApplicationDisplayMetrics>>());
            }

            ApplicationDisplayMetrics curDisplayMetrics = null;
            if (IsWindowMoved(displayKey, window, eventType, now, out curDisplayMetrics))
            {
                string log = string.Format("Captured {0,-8} at ({1}, {2}) of size {3} x {4} V:{5} {6} ",
                    curDisplayMetrics,
                    curDisplayMetrics.ScreenPosition.Left,
                    curDisplayMetrics.ScreenPosition.Top,
                    curDisplayMetrics.ScreenPosition.Width,
                    curDisplayMetrics.ScreenPosition.Height,
                    window.Visible,
                    curDisplayMetrics.Title
                    );
                string log2 = string.Format("\n    WindowPlacement.NormalPosition at ({0}, {1}) of size {2} x {3}",
                    curDisplayMetrics.WindowPlacement.NormalPosition.Left,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Top,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Width,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Height
                    );
                Log.Trace(log + log2);

                if (!monitorApplications[displayKey].ContainsKey(hWnd))
                {
                    monitorApplications[displayKey].Add(hWnd, new Queue<ApplicationDisplayMetrics>());
                    monitorApplications[displayKey][hWnd].Enqueue(curDisplayMetrics);
                }
                else if (!sessionEndTime.ContainsKey(displayKey))
                    //no capture when session is inactive to avoid queue overflow
                {
                    if (monitorApplications[displayKey][hWnd].Count == MaxHistoryQueueLength)
                    {
                        // limit length of capture history
                        monitorApplications[displayKey][hWnd].Dequeue();
                    }
                    monitorApplications[displayKey][hWnd].Enqueue(curDisplayMetrics);
                }
                ret = true;
            }

            if (saveToDB && curDisplayMetrics != null && monitorApplications[displayKey].ContainsKey(hWnd))
            {
                try
                {
                    var db = persistDB.GetCollection<ApplicationDisplayMetrics>(displayKey);
                    windowTitle[hWnd] = curDisplayMetrics.Title;
                    curDisplayMetrics.ProcessName = window.Process.ProcessName;
                    curDisplayMetrics.DbMatchWindow = false;

                    IntPtr hProcess = Kernel32.OpenProcess(Kernel32.ProcessAccessFlags.QueryInformation, false, curDisplayMetrics.ProcessId);
                    string procPath = GetProcExePath(hProcess);
                    if (!String.IsNullOrEmpty(procPath))
                    {
                        curDisplayMetrics.ProcessExePath = procPath;
                    }
                    db.Insert(curDisplayMetrics);
                    Kernel32.CloseHandle(hProcess);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            }
            return ret;
        }

        private string GetDisplayKey()
        {
            DesktopDisplayMetrics metrics = new DesktopDisplayMetrics();
            metrics.AcquireMetrics();
            return metrics.Key;
        }

        private void StartCaptureTimer(int milliSeconds = CaptureLatency)
        {
            // restart capture timer
            captureTimer.Change(milliSeconds, Timeout.Infinite);
        }

        private void CancelCaptureTimer()
        {
            // restart capture timer
            captureTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void StartRestoreTimer(int milliSecond = RestoreLatency, bool wait = false)
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
            var thread = new Thread(() =>
            {
                try
                {
                    string displayKey = GetDisplayKey();
                    if (displayKey != validDisplayKeyForCapture)
                    {
                        disableBatchCapture = true;
                        // discard the capture request due to display setting change
                        Log.Trace("Discard capture for {0}, when expecting {1}", displayKey, validDisplayKeyForCapture);
                        return;
                    }

                    lock (databaseLock)
                    {
                        CaptureApplicationsOnCurrentDisplays(displayKey, saveToDB);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }

            });
            thread.IsBackground = false;
            thread.Name = "PersistentWindowProcessor.BeginCaptureApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private void EndDisplaySession()
        {
            CancelCaptureTimer();
            ResetState();
            RecordBatchCaptureTime(DateTime.Now);
        }

        private void ResetState()
        {
            lock (controlLock)
            {
                // end of restore period
                CancelRestoreTimer();
                restoreTimes = 0;
                restoreNestLevel = 0;
                restoreFromDB = false;

                // reset counter of multiwindowProcess
                var keys = new List<string>();
                foreach (var key in multiwindowProcess.Keys)
                {
                    keys.Add(key);
                }

                foreach (var key in keys)
                {
                    multiwindowProcess[key] = 0;
                }

                // reset capture statistics for next capture period
                disableBatchCapture = false;
                pendingCaptureWindows.Clear();
            }
        }

        private void RecordBatchCaptureTime(DateTime time, bool force = false)
        {
            lock (controlLock)
            {
                if (!sessionEndTime.ContainsKey(validDisplayKeyForCapture))
                {
                    sessionEndTime.Add(validDisplayKeyForCapture, time);
                    Log.Trace("Capture time {0}", time);
                }
                else if (force)
                {
                    sessionEndTime[validDisplayKeyForCapture] = time;
                }
            }
        }

        private void RemoveBatchCaptureTime(bool force = true)
        {
            lock (controlLock)
            {
                if (!force && sessionEndTime.ContainsKey(validDisplayKeyForCapture))
                {
                    return;
                }
                sessionEndTime.Remove(validDisplayKeyForCapture);
            }

        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey, bool saveToDB = false)
        {
            var appWindows = CaptureWindowsOfInterest();
            DateTime now = DateTime.Now;
            int cnt = 0;
            Log.Trace("Capturing windows for display setting {0}", displayKey);
            if (saveToDB)
            {
                var db = persistDB.GetCollection<ApplicationDisplayMetrics>(displayKey);
                db.DeleteAll();
            }

            foreach (var window in appWindows)
            {
                if (CaptureWindow(window, 0, now, displayKey, saveToDB))
                {
                    cnt++;
                }
            }

            if (cnt > 0)
            {
                Log.Trace("{0} windows captured", cnt);
            }
        }

        private IEnumerable<SystemWindow> CaptureWindowsOfInterest()
        {
            return SystemWindow.AllToplevelWindows
                                .Where(row =>
                                {
                                    return row.Parent.HWnd.ToInt64() == 0
                                    //&& (!string.IsNullOrEmpty(row.Title)
                                    //&& !row.Title.Equals("Program Manager")
                                    //&& !row.Title.Contains("Task Manager")
                                    //&& row.Position.Height != 0
                                    //&& row.Position.Width != 0
                                    && row.Visible;
                                });
        }

        private bool IsWindowMoved(string displayKey, SystemWindow window, User32Events eventType, DateTime time, out ApplicationDisplayMetrics curDisplayMetrics)
        {
            curDisplayMetrics = null;

            if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
            {
                return false;
            }

            IntPtr hwnd = window.HWnd;
            bool isTaskBar = false;
            if (IsTaskBar(window))
            {
                // capture task bar
                isTaskBar = true;
            }
            else if (string.IsNullOrEmpty(window.Title))
            {
                return false;
            }

            WindowPlacement windowPlacement = new WindowPlacement();
            User32.GetWindowPlacement(window.HWnd, ref windowPlacement);

            // compensate for GetWindowPlacement() failure to get real coordinate of snapped window
            RECT2 screenPosition = new RECT2();
            User32.GetWindowRect(hwnd, ref screenPosition);
            if (screenPosition.Top < 0 && screenPosition.Top > -15)
            {
                // automatically fix small negative y coordinate to avoid repeated recovery failure
                screenPosition.Top = 0;
            }

            uint processId = 0;
            uint threadId = User32.GetWindowThreadProcessId(window.HWnd, out processId);

            curDisplayMetrics = new ApplicationDisplayMetrics
            {
                HWnd = hwnd,
                ProcessId = processId,

                // this function call is very CPU-intensive
                //ProcessName = window.Process.ProcessName,
                ProcessName = "",

                ClassName = window.ClassName,
                Title = isTaskBar ? "$taskbar$" : window.Title,

                CaptureTime = time,
                WindowPlacement = windowPlacement,
                NeedUpdateWindowPlacement = false,
                ScreenPosition = screenPosition
            };

            bool moved = false;
            if (!monitorApplications[displayKey].ContainsKey(hwnd))
            {
                //newly created or not recorded window
                windowTitle[hwnd] = curDisplayMetrics.Title;
                moved = true;
            }
            else
            {
                ApplicationDisplayMetrics[] captureHistory = monitorApplications[displayKey][hwnd].ToArray();
                ApplicationDisplayMetrics prevDisplayMetrics;
                if (eventType == 0 && restoringWindowPos)
                {
                    //truncate OS move event that happens after cut-off time
                    int truncateSize = 0;
                    foreach (var metrics in captureHistory)
                    {
                        if (metrics.CaptureTime > time)
                        {
                            break;
                        }
                        truncateSize++;
                    }

                    if (truncateSize == 0)
                    {
                        Log.Trace("unexpected zero captured events");
                        return false;
                    }
                    else if (truncateSize < captureHistory.Length)
                    {
                        // truncate capture history to filter out OS moves
                        Array.Resize(ref captureHistory, truncateSize);
                        monitorApplications[displayKey][hwnd].Clear();
                        foreach (var metrics in captureHistory)
                        {
                            monitorApplications[displayKey][hwnd].Enqueue(metrics);
                        }
                    }
                }
                prevDisplayMetrics = captureHistory.Last();

                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId
                    || prevDisplayMetrics.ClassName != curDisplayMetrics.ClassName)
                {
                    // key collision between dead window and new window with the same hwnd
                    monitorApplications[displayKey].Remove(hwnd);
                    moved = true;
                }
                else if (eventType == User32Events.EVENT_SYSTEM_FOREGROUND)
                {
                    // when close/reopen session, OS/user may activate existing window (possibly with different position)
                    // just ignore it
                }
                else if (!prevDisplayMetrics.EqualPlacement(curDisplayMetrics))
                {
                    //monitorApplications[displayKey][curDisplayMetrics.Key].WindowPlacement = curDisplayMetrics.WindowPlacement;
                    curDisplayMetrics.NeedUpdateWindowPlacement = true;
                    moved = true;
                }
                else if (!prevDisplayMetrics.ScreenPosition.Equals(curDisplayMetrics.ScreenPosition))
                {
                    moved = true;
                }
                else
                {
                    // nothing changed except event type & time
                }
            }

            return moved;
        }

        public void BatchRestoreApplicationsOnCurrentDisplays()
        {
            if (restoreTimes == 0)
            {
                showRestoreTip();
                if (restoreFromDB)
                {
                    Thread.Sleep(2000); // let mouse settle still for taskbar restoration
                }
            }

            lock (controlLock)
            {
                if (!restoringWindowPos && !restoreFromDB)
                {
                    return;
                }

                if (restoreNestLevel > 0)
                {
                    // avoid overloading CPU due to too many restore threads ready to run
                    Log.Trace("restore busy");
                    StartRestoreTimer();
                    return;
                }
                restoreNestLevel++;
            }

            var thread = new Thread(() =>
            {
                try
                {
                    lock (databaseLock)
                    {
                        CancelRestoreFinishedTimer();
                        if (restoreTimes < (remoteSession ? MaxRestoreTimesRemote : MaxRestoreTimesLocal))
                        {
                            validDisplayKeyForCapture = GetDisplayKey();
                            RestoreApplicationsOnCurrentDisplays(validDisplayKeyForCapture);
                            restoreTimes++;

                            // schedule finish restore
                            StartRestoreFinishedTimer(milliSecond : MaxRestoreLatency);

                            // force next restore, as Windows OS might not send expected message during restore
                            if (restoreTimes < MinRestoreTimes)
                            {
                                StartRestoreTimer();
                            }
                        }
                        else
                        {
                            // immediately finish restore
                            StartRestoreFinishedTimer(0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }

                lock (controlLock)
                {
                    restoreNestLevel--;
                }
            });
            thread.IsBackground = false;
            thread.Name = "PersistentWindowProcessor.RestoreApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private bool IsTaskBar(SystemWindow window)
        {
            return window.ClassName.Equals("Shell_TrayWnd");
        }

        private void TestMoveTaskBar()
        {
            Thread.Sleep(3000);
            IntPtr hwnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            MoveTaskBar(hwnd, 300, 15);
        }

        private void MoveTaskBar(IntPtr hwnd, int x, int y)
        {
            // simulate mouse drag, assuming taskbar is unlocked
            /*
                ControlGetPos x, y, w, h, MSTaskListWClass1, ahk_class Shell_TrayWnd
                MouseMove x+1, y+1
                MouseClickDrag Left, x+1, y+1, targetX, targetY, 10
            */

            RECT2 screenPosition = new RECT2();
            IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            //User32.GetWindowRect(hReBar, ref screenPosition);

            IntPtr hTaskBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
            hTaskBar = User32.FindWindowEx(hTaskBar, IntPtr.Zero, "MSTaskListWClass", null);
            User32.GetWindowRect(hTaskBar, ref screenPosition);

            // try place cursor to head and then tail of taskbar to guarantee move success
            int dx;
            int dy;
            if (screenPosition.Width > screenPosition.Height)
            {
                switch (restoreTimes)
                {
                    case 1:
                        dx = screenPosition.Width - restoreTimes * 10;
                        break;
                    default:
                        dx = 1;
                        break;
                }
                dy = screenPosition.Height / 2;
            }
            else
            {
                dx = screenPosition.Width / 2;
                switch (restoreTimes)
                {
                    case 1:
                        dy = screenPosition.Height - restoreTimes * 10;
                        break;
                    default:
                        dy = 1;
                        break;
                }
            }

            // avoid unnecessary move
            int centerx = screenPosition.Left + screenPosition.Width / 2;
            int centery = screenPosition.Top + screenPosition.Height / 2;
            int deltax = Math.Abs(centerx - x);
            int deltay = Math.Abs(centery - y);
            if (deltax + deltay < 300)
            {
                // taskbar center has no change
                return;
            }

            User32.SetCursorPos(screenPosition.Left + dx, screenPosition.Top + dy);
            //User32.SetForegroundWindow(hwnd);
            //User32.SetActiveWindow(hwnd);
            //Thread.Sleep(1000); // wait to be activated
            //User32.SetForegroundWindow(hTaskBar);
            User32.SetActiveWindow(hTaskBar);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(3500); // wait to be activated
            User32.SetCursorPos(x, y);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
        }

        private ApplicationDisplayMetrics SearchDb(IEnumerable<ApplicationDisplayMetrics> results)
        { 
            foreach (var result in results)
            {
                if (!result.DbMatchWindow)
                {
                    // map to the first matching db entry
                    Log.Trace("restore window position with matching process name {0}", result.ProcessName);
                    return result;
                }
            }

            return null;
        }

        private bool RestoreApplicationsOnCurrentDisplays(string displayKey, SystemWindow sWindow = null)
        {
            bool succeed = false;

            if (!monitorApplications.ContainsKey(displayKey)
                || monitorApplications[displayKey].Count == 0)
            {
                // the display setting has not been captured yet
                Log.Trace("Unknown display setting {0}", displayKey);
                return succeed;
            }

            Log.Info("Restoring applications for {0}", displayKey);
            IEnumerable<SystemWindow> sWindows;
            SystemWindow[] arr = new SystemWindow[1];
            if (sWindow != null)
            {
                arr[0] = sWindow;
                sWindows = arr;
            }
            else
            {
                sWindows = CaptureWindowsOfInterest();
            }

            // determine the time to be restored
            DateTime restoreTime;
            if (sessionEndTime.ContainsKey(displayKey))
            {
                restoreTime = sessionEndTime[displayKey];
                TimeSpan ts = new TimeSpan((CaptureLatency + 1000) * TimeSpan.TicksPerMillisecond);
                restoreTime = restoreTime.Subtract(ts);
            }
            else
            {
                restoreTime = DateTime.Now;
            }
            Log.Trace("Restore time {0}", restoreTime);

            ILiteCollection<ApplicationDisplayMetrics> db = null;
            if (restoreFromDB)
            {
                db = persistDB.GetCollection<ApplicationDisplayMetrics>(displayKey);

                foreach (var window in sWindows)
                {
                    if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
                    {
                        continue;
                    }

                    IntPtr hWnd = window.HWnd;
                    if (!monitorApplications[displayKey].ContainsKey(hWnd))
                    {
                        continue;
                    }

                    ApplicationDisplayMetrics curDisplayMetrics = null;
                    var processName = window.Process.ProcessName;
                    uint processId = 0;
                    uint threadId = User32.GetWindowThreadProcessId(hWnd, out processId);

                    if (windowTitle.ContainsKey(hWnd))
                    {
                        string title = windowTitle[hWnd];
                        var results = db.Find(x => x.ClassName == window.ClassName && x.Title == title && x.ProcessName == processName && x.ProcessId == processId);
                        curDisplayMetrics = SearchDb(results);

                        if (curDisplayMetrics == null)
                        {
                            results = db.Find(x => x.ClassName == window.ClassName && x.Title == title && x.ProcessName == processName);
                            curDisplayMetrics = SearchDb(results);
                        }
                    }

                    if (curDisplayMetrics == null)
                    {
                        var results = db.Find(x => x.ClassName == window.ClassName && x.ProcessName == processName);
                        curDisplayMetrics = SearchDb(results);
                    }

                    if (curDisplayMetrics == null)
                    {
                        // no db data to restore
                        continue;
                    }

                    // update stale window/process id
                    curDisplayMetrics.HWnd = hWnd;
                    curDisplayMetrics.ProcessId = processId;
                    curDisplayMetrics.ProcessName = processName;
                    curDisplayMetrics.DbMatchWindow = true;
                    db.Update(curDisplayMetrics);

                    curDisplayMetrics.CaptureTime = restoreTime;

                    if (monitorApplications[displayKey][hWnd].Count == MaxHistoryQueueLength)
                    {
                        // limit length of capture history
                        monitorApplications[displayKey][hWnd].Dequeue();
                    }
                    monitorApplications[displayKey][hWnd].Enqueue(curDisplayMetrics);
                }
            }

            foreach (var window in sWindows)
            {
                if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
                {
                    continue;
                }

                IntPtr hWnd = window.HWnd;
                if (!monitorApplications[displayKey].ContainsKey(hWnd))
                {
                    continue;
                }

                ApplicationDisplayMetrics curDisplayMetrics = null;
                if (!IsWindowMoved(displayKey, window, 0, restoreTime, out curDisplayMetrics))
                {
                    continue;
                }

                ApplicationDisplayMetrics[] captureHisotry = monitorApplications[displayKey][hWnd].ToArray();
                ApplicationDisplayMetrics prevDisplayMetrics = captureHisotry.Last();
                RECT2 rect = prevDisplayMetrics.ScreenPosition;
                WindowPlacement windowPlacement = prevDisplayMetrics.WindowPlacement;

                if (IsTaskBar(window))
                {
                    MoveTaskBar(hWnd, rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                    continue;
                }

                bool success = true;
                if (restoreTimes >= MinRestoreTimes || curDisplayMetrics.NeedUpdateWindowPlacement)
                {
                    // recover NormalPosition (the workspace position prior to snap)
                    if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                    {
                        // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                        // the window thinks it's maximized, but does not eat all the real estate. So we'll temporarily unmaximize then
                        // re-apply that
                        windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                        User32.SetWindowPlacement(hWnd, ref windowPlacement);
                        windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                    }

                    success &= User32.SetWindowPlacement(hWnd, ref windowPlacement);
                    Log.Info("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                        window.Process.ProcessName,
                        windowPlacement.NormalPosition.Left,
                        windowPlacement.NormalPosition.Top,
                        windowPlacement.NormalPosition.Width,
                        windowPlacement.NormalPosition.Height,
                        success);
                }

                // recover previous screen position
                success &= User32.MoveWindow(hWnd, rect.Left, rect.Top, rect.Width, rect.Height, false);

                Log.Info("MoveWindow({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                    window.Process.ProcessName,
                    rect.Left,
                    rect.Top,
                    rect.Width,
                    rect.Height,
                    success);

                succeed = true;
                if (!success)
                {
                    string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    Log.Error(error);
                }
            }

            User32.RedrawWindow(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, User32.RedrawWindowFlags.Invalidate);

            Log.Trace("Restored windows position for display setting {0}", displayKey);

            if (restoreFromDB)
            {
                // launch process in db
                var results = db.Find(x => x.DbMatchWindow == false); // find process not yet started
                foreach (var curDisplayMetrics in results)
                {
#if DEBUG
                    if (curDisplayMetrics.Title.Contains("Microsoft Visual Studio"))
                    {
                        continue;
                    }
#endif

                    if (multiwindowProcess.ContainsKey(curDisplayMetrics.ProcessName))
                    {
                        if (multiwindowProcess[curDisplayMetrics.ProcessName] > 0)
                        {
                            // already launched
                            continue;
                        }
                        multiwindowProcess[curDisplayMetrics.ProcessName]++;
                    }

                    if (!String.IsNullOrEmpty(curDisplayMetrics.ProcessExePath))
                    {
                        Log.Trace("launch process {0}", curDisplayMetrics.ProcessExePath);
                        System.Diagnostics.Process.Start(curDisplayMetrics.ProcessExePath);
                        Thread.Sleep(1000);
                    }
                }
            }

            return succeed;
        }

        private string GetProcExePath(IntPtr hProc)
        {
            string pathToExe = string.Empty;

            int nChars = 4096;
            StringBuilder buf = new StringBuilder(nChars);

            bool success = Kernel32.QueryFullProcessImageName(hProc, 0, buf, ref nChars);

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

            return pathToExe;
        }

        public void StopRunningThreads()
        {
            //stop running thread of event loop
        }

#region IDisposable
        public virtual void Dispose(bool disposing)
        {
            StopRunningThreads();

            SystemEvents.DisplaySettingsChanging -= this.displaySettingsChangingHandler;
            SystemEvents.DisplaySettingsChanged -= this.displaySettingsChangedHandler;
            SystemEvents.PowerModeChanged -= powerModeChangedHandler;
            SystemEvents.SessionSwitch -= sessionSwitchEventHandler;

            foreach (var handle in this.winEventHooks)
            {
                User32.UnhookWinEvent(handle);
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
