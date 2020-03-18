using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32;
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
        private const int MaxRestoreLatency = 5000; // max milliseconds to wait after previous restore pass to tell if restore is finished
        private const int MinRestoreTimes = 2; // restores with fixed RestoreLatency
        private const int MaxRestoreTimesLocal = 4; // Max restores activated by further window event for local console session
        private const int MaxRestoreTimesRemote = 6; // for remote session

        private const int CaptureLatency = 3000; // milliseconds to wait for window position capture
        private const int MinOsMoveWindows = 4; // minimum number of moving windows to measure in order to recognize OS initiated move
        private const int MaxHistoryQueueLength = 20;

        // window position database
        private Dictionary<string, Dictionary<string, Queue<ApplicationDisplayMetrics>>> monitorApplications = null;

        // control shared by capture and restore
        private Object databaseLock; // lock access to window position database
        private Object controlLock = new Object();

        // capture control
        private Timer captureTimer;
        private bool disableBatchCapture = false;
        private string validDisplayKeyForCapture = null;
        private HashSet<IntPtr> pendingCaptureWindows = new HashSet<IntPtr>();

        // restore control
        private Timer restoreTimer;
        private Timer restoreFinishedTimer;
        private bool restoringWindowPos = false; // about to restore
        private int restoreTimes = 0;
        private int restoreNestLevel = 0; // nested call level

        // session control
        private bool remoteSession = false;

        // last session
        private Dictionary<string, DateTime> eolTime = new Dictionary<string, DateTime>(); //time when end of life

        // callbacks
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
        public void Start()
        {
            /* test move taskbar function
            Thread.Sleep(3000);
            IntPtr hwnd = User32.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "Shell_TrayWnd", null);
            MoveTaskBar(hwnd, 300, 15);
            */

            monitorApplications = new Dictionary<string, Dictionary<string, Queue<ApplicationDisplayMetrics>>>();
            databaseLock = new object();
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
                lock(controlLock)
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

            this.displaySettingsChangingHandler =
                (s, e) =>
                {
                    DateTime time = DateTime.Now;
                    Log.Info("Display settings changing {0}", time);
                    ResetState();
                };

            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    DateTime time = DateTime.Now;
                    Log.Info("Display settings changed {0}", time);

                    ResetState();

                    restoringWindowPos = true;
                    StartRestoreTimer();
                };

            SystemEvents.DisplaySettingsChanged += this.displaySettingsChangedHandler;

            powerModeChangedHandler =
                (s, e) =>
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            Log.Info("System suspending");
                            break;

                        case PowerModes.Resume:
                            Log.Info("System Resuming");
                            break;
                    }
                };

            SystemEvents.PowerModeChanged += powerModeChangedHandler;

            sessionSwitchEventHandler = (sender, args) =>
            {
                switch (args.Reason)
                {
                    case SessionSwitchReason.RemoteDisconnect:
                    case SessionSwitchReason.SessionLock:
                    case SessionSwitchReason.ConsoleDisconnect:
                        Log.Trace("Session closing: reason {0}", args.Reason);
                        ResetState();
                        RecordBatchCaptureTime(DateTime.Now);
                        break;

                    case SessionSwitchReason.RemoteConnect:
                        remoteSession = true;
                        goto case SessionSwitchReason.SessionUnlock;
                    case SessionSwitchReason.SessionUnlock:
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        break;

                    case SessionSwitchReason.ConsoleConnect:
                        // session control
                        remoteSession = false;
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        break;
                }
            };

            SystemEvents.SessionSwitch += sessionSwitchEventHandler;
        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (!User32.IsTopLevelWindow(hwnd))
            {
                return;
            }

            var window = new SystemWindow(hwnd);
            if (window.Parent.HWnd.ToInt64() != 0 || !window.Visible || string.IsNullOrEmpty(window.Title))
            {
                // only track top level visible windows
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
#endif
                Log.Trace("WinEvent received. Type: {0:x4}, Window: {1:x8}", (uint)eventType, hwnd.ToInt64());
#if DEBUG
                RECT screenPosition = new RECT();
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
                            /*
                        case User32Events.EVENT_SYSTEM_MOVESIZESTART:
                            return;

                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                        case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                        case User32Events.EVENT_SYSTEM_FOREGROUND:
                            var thread = new Thread(() =>
                            {
                                try
                                {
                                    lock (databaseLock)
                                    {
                                        string displayKey = GetDisplayKey();
                                        CaptureWindow(window, eventType, now, displayKey);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.ToString());
                                }
                            });
                            thread.Start();
                            return;
                            */
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
                                // can not tell if this event is caused by user snap operation or OS initiated closing session
                                // wait for other user move events until capture timer expires
                                if (pendingCaptureWindows.Count == 0)
                                {
                                    StartCaptureTimer();
                                }
                                pendingCaptureWindows.Add(hwnd);
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
                                    //CancelCaptureTimer();
                                    lock (databaseLock)
                                    {
                                        CaptureWindow(window, eventType, now, displayKey);
                                        if (eventType != User32Events.EVENT_SYSTEM_FOREGROUND)
                                        {
                                            RemoveBatchCaptureTime();
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

        private bool CaptureWindow(SystemWindow window, User32Events eventType, DateTime now, string displayKey)
        {
            bool ret = false;

            if (!monitorApplications.ContainsKey(displayKey))
            {
                monitorApplications.Add(displayKey, new Dictionary<string, Queue<ApplicationDisplayMetrics>>());
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
                    window.Title
                    );
                string log2 = string.Format("\n    WindowPlacement.NormalPosition at ({0}, {1}) of size {2} x {3}",
                    curDisplayMetrics.WindowPlacement.NormalPosition.Left,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Top,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Width,
                    curDisplayMetrics.WindowPlacement.NormalPosition.Height
                    );
                Log.Trace(log + log2);

                if (!monitorApplications[displayKey].ContainsKey(curDisplayMetrics.Key))
                {
                    monitorApplications[displayKey].Add(curDisplayMetrics.Key, new Queue<ApplicationDisplayMetrics>());
                    monitorApplications[displayKey][curDisplayMetrics.Key].Enqueue(curDisplayMetrics);
                }
                else
                {
                    if (monitorApplications[displayKey][curDisplayMetrics.Key].Count == MaxHistoryQueueLength)
                    {
                        // limit length of capture history
                        monitorApplications[displayKey][curDisplayMetrics.Key].Dequeue();
                    }
                    monitorApplications[displayKey][curDisplayMetrics.Key].Enqueue(curDisplayMetrics);
                }
                ret = true;
            }

            return ret;
        }

        private string GetDisplayKey()
        {
            DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
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

        private void CancelRestoreFinishedTimer(int milliSecond)
        {
            restoreFinishedTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void BatchCaptureApplicationsOnCurrentDisplays()
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
                        CaptureApplicationsOnCurrentDisplays(displayKey);
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

        private void ResetState()
        {
            lock(controlLock)
            {
                // end of restore period
                CancelRestoreTimer();
                restoreTimes = 0;
                restoreNestLevel = 0;

                // reset capture statistics for next capture period
                disableBatchCapture = false;
                pendingCaptureWindows.Clear();
            }
        }

        private void RecordBatchCaptureTime(DateTime time, bool force = false)
        {
            lock (controlLock)
            {
                if (!eolTime.ContainsKey(validDisplayKeyForCapture))
                {
                    eolTime.Add(validDisplayKeyForCapture, time);
                    Log.Trace("Capture time {0}", time);
                }
                else if (force)
                {
                    eolTime[validDisplayKeyForCapture] = time;
                }
            }
        }

        private void RemoveBatchCaptureTime()
        {
            lock(controlLock)
            {
                if (eolTime.ContainsKey(validDisplayKeyForCapture))
                {
                    eolTime.Remove(validDisplayKeyForCapture);
                }
            }

        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey)
        {
            var appWindows = CaptureWindowsOfInterest();
            DateTime now = DateTime.Now;
            int cnt = 0;
            Log.Trace("Capturing windows for display setting {0}", displayKey);
            foreach (var window in appWindows)
            {
                if (CaptureWindow(window, 0, now, displayKey))
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
            RECT screenPosition = new RECT();
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
#if DEBUG
                // these function calls are very CPU-intensive
                ApplicationName = window.Process.ProcessName,
#else
                ApplicationName = "",
#endif
                ClassName = window.ClassName,
                ProcessId = processId,

                IsTaskbar = isTaskBar,
                CaptureTime = time,
                WindowPlacement = windowPlacement,
                NeedUpdateWindowPlacement = false,
                ScreenPosition = screenPosition
            };

            bool moved = false;
            if (!monitorApplications[displayKey].ContainsKey(curDisplayMetrics.Key))
            {
                moved = true;
            }
            else
            {
                ApplicationDisplayMetrics[] captureHistory = monitorApplications[displayKey][curDisplayMetrics.Key].ToArray();
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
                        monitorApplications[displayKey][curDisplayMetrics.Key].Clear();
                        foreach (var metrics in captureHistory)
                        {
                            monitorApplications[displayKey][curDisplayMetrics.Key].Enqueue(metrics);
                        }
                    }
                }
                prevDisplayMetrics = captureHistory.Last();

                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId
                    || prevDisplayMetrics.ClassName != curDisplayMetrics.ClassName)
                {
                    // key collision between dead window and new window with the same hwnd
                    monitorApplications[displayKey].Remove(curDisplayMetrics.Key);
                    moved = true;
                }
                else if (eventType == User32Events.EVENT_SYSTEM_FOREGROUND)
                {
                    // when close/reopen session, OS/user may activate existing window (possibly with different position)
                    // just ignore it
                }
                else if (isTaskBar)
                {
                    moved = !prevDisplayMetrics.ScreenPosition.Equals(curDisplayMetrics.ScreenPosition);
                }
                else if (!prevDisplayMetrics.EqualPlacement(curDisplayMetrics))
                {
                    /*
                    Log.Trace("Unexpected WindowPlacement.NormalPosition change if ScreenPosition keep same {0} {1} {2}",
                        window.Process.ProcessName, processId, window.HWnd.ToString("X8"));

                    string log = string.Format("prev WindowPlacement ({0}, {1}) of size {2} x {3}",
                        prevDisplayMetrics.WindowPlacement.NormalPosition.Left,
                        prevDisplayMetrics.WindowPlacement.NormalPosition.Top,
                        prevDisplayMetrics.WindowPlacement.NormalPosition.Width,
                        prevDisplayMetrics.WindowPlacement.NormalPosition.Height
                        );

                    string log2 = string.Format("cur  WindowPlacement ({0}, {1}) of size {2} x {3}",
                        curDisplayMetrics.WindowPlacement.NormalPosition.Left,
                        curDisplayMetrics.WindowPlacement.NormalPosition.Top,
                        curDisplayMetrics.WindowPlacement.NormalPosition.Width,
                        curDisplayMetrics.WindowPlacement.NormalPosition.Height
                        );

                    Log.Trace("{0}", log);
                    Log.Trace("{0}", log2);

                    if (monitorApplications[displayKey][curDisplayMetrics.Key].RecoverWindowPlacement)
                    {
                        Log.Trace("Try recover previous placement");
                        WindowPlacement prevWP = prevDisplayMetrics.WindowPlacement;
                        User32.SetWindowPlacement(hwnd, ref prevWP);
                        RECT rect = prevDisplayMetrics.ScreenPosition;
                        User32.MoveWindow(hwnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                        monitorApplications[displayKey][curDisplayMetrics.Key].RecoverWindowPlacement = false;
                    }
                    else
                    {
                        Log.Trace("Fail to recover NormalPosition {0} {1} {2}",
                            window.Process.ProcessName, processId, window.HWnd.ToString("X8"));
                        // needUpdate = true;
                        // immediately update WindowPlacement with current value
                        monitorApplications[displayKey][curDisplayMetrics.Key].WindowPlacement = curDisplayMetrics.WindowPlacement;
                        monitorApplications[displayKey][curDisplayMetrics.Key].RecoverWindowPlacement = true;
                    }
                    */
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
            lock (controlLock)
            {
                if (!restoringWindowPos)
                {
                    return;
                }

                if (restoreNestLevel > 1)
                {
                    // avoid overloading CPU due to too many restore threads ready to run
                    Log.Trace("restore busy");
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
                        CancelRestoreFinishedTimer(MaxRestoreLatency);
                        if (restoreTimes < (remoteSession ? MaxRestoreTimesRemote : MaxRestoreTimesLocal))
                        {
                            validDisplayKeyForCapture = GetDisplayKey();
                            RestoreApplicationsOnCurrentDisplays(validDisplayKeyForCapture);
                            restoreTimes++;

                            // schedule finish restore
                            StartRestoreFinishedTimer(MaxRestoreLatency);

                            // force next restore, as Windows OS might not send expected message during restore
                            if (restoreTimes < MinRestoreTimes)
                            {
                                StartRestoreTimer(wait: remoteSession);
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

        private void MoveTaskBar(IntPtr hwnd, int x, int y)
        {
            // simulate mouse drag, assuming taskbar is unlocked
            /*
                ControlGetPos x, y, w, h, MSTaskListWClass1, ahk_class Shell_TrayWnd
                MouseMove x+1, y+1
                MouseClickDrag Left, x+1, y+1, targetX, targetY, 10
            */

            RECT screenPosition = new RECT();
            IntPtr hReBar = User32.FindWindowEx(hwnd, IntPtr.Zero, "ReBarWindow32", null);
            User32.GetWindowRect(hReBar, ref screenPosition);
            int dx;
            int dy;
            if (screenPosition.Width > screenPosition.Height)
            {
                //horizontal
                dx = 1;
                dy = screenPosition.Height / 2;
            }
            else
            {
                dx = screenPosition.Width / 2;
                dy = 1;
            }
            IntPtr hTaskBar = User32.FindWindowEx(hReBar, IntPtr.Zero, "MSTaskSwWClass", null);
            hTaskBar = User32.FindWindowEx(hTaskBar, IntPtr.Zero, "MSTaskListWClass", null);
            User32.GetWindowRect(hTaskBar, ref screenPosition);
            User32.SetCursorPos(screenPosition.Left + dx, screenPosition.Top + dy);
            User32.SetForegroundWindow(hwnd);
            User32.SetActiveWindow(hwnd);
            Thread.Sleep(1000); // wait to be activated
            User32.SetForegroundWindow(hTaskBar);
            User32.SetActiveWindow(hTaskBar);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTDOWN,
                0, 0, 0, UIntPtr.Zero);
            Thread.Sleep(2500); // wait to be activated
            User32.SetCursorPos(x, y);
            User32.mouse_event(MouseAction.MOUSEEVENTF_LEFTUP,
                0, 0, 0, UIntPtr.Zero);
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
            if (eolTime.ContainsKey(displayKey))
            {
                restoreTime = eolTime[displayKey];
                TimeSpan ts = new TimeSpan((CaptureLatency + 1000) * TimeSpan.TicksPerMillisecond);
                restoreTime = restoreTime.Subtract(ts);
            }
            else
            {
                restoreTime = DateTime.Now;
            }
            Log.Trace("Restore time {0}", restoreTime);

            foreach (var window in sWindows)
            {
                if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
                {
                    continue;
                }

                var proc_name = window.Process.ProcessName;
                if (proc_name.Contains("CodeSetup"))
                {
                    // prevent hang in SetWindowPlacement()
                    continue;
                }

                string applicationKey = ApplicationDisplayMetrics.GetKey(window.HWnd, window.Process.ProcessName);

                if (monitorApplications[displayKey].ContainsKey(applicationKey))
                {
                    ApplicationDisplayMetrics curDisplayMetrics = null;
                    if (!IsWindowMoved(displayKey, window, 0, restoreTime, out curDisplayMetrics))
                    {
                        // window position has no change
                        continue;
                    }

                    ApplicationDisplayMetrics[] captureHisotry = monitorApplications[displayKey][applicationKey].ToArray();
                    ApplicationDisplayMetrics prevDisplayMetrics = captureHisotry.Last();
                    RECT rect = prevDisplayMetrics.ScreenPosition;
                    WindowPlacement windowPlacement = prevDisplayMetrics.WindowPlacement;
                    IntPtr hwnd = prevDisplayMetrics.HWnd;

                    bool success = true;
                    if (curDisplayMetrics.IsTaskbar)
                    {
                        MoveTaskBar(hwnd, rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
                        continue;
                    }

                    if (restoreTimes >= MinRestoreTimes || curDisplayMetrics.NeedUpdateWindowPlacement)
                    {
                        // recover NormalPosition (the workspace position prior to snap)
                        if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                        {
                            // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                            // the window thinks it's maximized, but does not eat all the real estate. So we'll temporarily unmaximize then
                            // re-apply that
                            windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                            User32.SetWindowPlacement(hwnd, ref windowPlacement);
                            windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                        }

                        success &= User32.SetWindowPlacement(hwnd, ref windowPlacement);
                        Log.Info("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                            window.Process.ProcessName,
                            windowPlacement.NormalPosition.Left,
                            windowPlacement.NormalPosition.Top,
                            windowPlacement.NormalPosition.Width,
                            windowPlacement.NormalPosition.Height,
                            success);
                    }

                    // recover previous screen position
                    success &= User32.MoveWindow(hwnd, rect.Left, rect.Top, rect.Width, rect.Height, true);

                    /*
                    success &= User32.SetWindowPos(
                        window.HWnd,
                        IntPtr.Zero,
                        rect.Left,
                        rect.Top,
                        rect.Width,
                        rect.Height,
                        SetWindowPosFlags.DoNotRedraw |
                        SetWindowPosFlags.DoNotActivate |
                        SetWindowPosFlags.DoNotChangeOwnerZOrder);

                    success &= User32.SetWindowPos(
                        window.HWnd,
                        IntPtr.Zero,
                        rect.Left,
                        rect.Top,
                        rect.Width,
                        rect.Height,
                        SetWindowPosFlags.DoNotActivate |
                        SetWindowPosFlags.DoNotChangeOwnerZOrder |
                        SetWindowPosFlags.AsynchronousWindowPosition);
                    */

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
            }

            Log.Trace("Restored windows position for display setting {0}", displayKey);

            return succeed;
        }

        public void Stop()
        {
            //stop running thread of event loop
        }

#region IDisposable
        public virtual void Dispose(bool disposing)
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
