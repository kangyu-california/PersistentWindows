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
        private const int MinRestoreTimes = 4; // restores with fixed RestoreLatency
        private const int MaxRestoreTimesLocal = 6; // Max restores activated by further window event for local console session
        private const int MaxRestoreTimesRemote = 10; // for remote session

        private const int CaptureLatency = 3000; // milliseconds to wait for window position capture, should be bigger than RestoreLatency
        private const int MaxCaptureLatency = 15000; // max latency to capture OS moves, needed for slow RDP session
        private const int MaxUserMovePerSecond = 4; // maximum speed of window move/resize by human
        private const int MinOsMoveWindows = 5; // minimum number of moving windows to measure in order to recognize OS initiated move

        // window position database
        private Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>> monitorApplications = null;

        // control shared by capture and restore
        private Timer captureTimer;
        private Timer restoreTimer;
        private Timer restoreFinishedTimer;
        private Object databaseLock; // lock access to window position database
        private Object controlLock = new Object();
        private string validDisplayKeyForCapture = null;

        // capture control: window move/resize activity
        private int userMoves = 0; // user initiated window move/resize events
        private DateTime firstEventTime;
        private HashSet<IntPtr> pendingCaptureWindows = new HashSet<IntPtr>();

        // restore control
        private bool restoringWindowPos = false; // about to restore
        private bool osMove = false; // window move/resize is initiated by OS
        private int restoreTimes = 0;
        private int restoreNestLevel = 0; // nested call level

        // session control
        private bool remoteSession = false;

        // callbacks
        private PowerModeChangedEventHandler powerModeChangedHandler;
        private EventHandler displaySettingsChangingHandler;
        private EventHandler displaySettingsChangedHandler;
        private SessionSwitchEventHandler sessionSwitchEventHandler;

        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private User32.WinEventDelegate winEventsCaptureDelegate;

        public void Start()
        {
            monitorApplications = new Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>>();
            databaseLock = new object();

            captureTimer = new Timer(state =>
            {
                Log.Trace("Capture timer expired");
                BeginCaptureApplicationsOnCurrentDisplays();
            });

            firstEventTime = DateTime.Now;
            validDisplayKeyForCapture = GetDisplayKey();
            StartCaptureTimer(); //initial capture

            restoreTimer = new Timer(state =>
            {
                Log.Trace("Restore timer expired");
                BeginRestoreApplicationsOnCurrentDisplays();
            });

            restoreFinishedTimer = new Timer(state =>
            {
                Log.Trace("Restore Finished");
                restoringWindowPos = false;
                osMove = false;
                ResetState();
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
                    DateTime date = DateTime.Now;
                    Log.Info("Display settings changing {0}", date);
                    ResetState();
                };

            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    DateTime date = DateTime.Now;
                    Log.Info("Display settings changed {0}", date);

                    ResetState();

                    restoringWindowPos = true;
                    BeginRestoreApplicationsOnCurrentDisplays();
                };

            SystemEvents.DisplaySettingsChanged += this.displaySettingsChangedHandler;

            powerModeChangedHandler =
                (s, e) =>
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            Log.Info("System suspending");
                            ResetState();
                            break;

                        case PowerModes.Resume:
                            Log.Info("System Resuming");
                            ResetState();
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
                        break;

                    case SessionSwitchReason.RemoteConnect:
                        remoteSession = true;
                        goto case SessionSwitchReason.SessionUnlock;
                    case SessionSwitchReason.SessionUnlock:
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        CancelCaptureTimer();
                        break;

                    case SessionSwitchReason.ConsoleConnect:
                        // session control
                        remoteSession = false;
                        Log.Trace("Session opening: reason {0}", args.Reason);
                        CancelCaptureTimer();
                        break;
                }
            };

            SystemEvents.SessionSwitch += sessionSwitchEventHandler;
        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            var window = new SystemWindow(hwnd);
            if (!User32.IsTopLevelWindow(hwnd))
            {
                return;
            }

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
                string log = string.Format("Defer consumption of window move message of process {0} at ({1}, {2}) of size {3} x {4} with title: {5}",
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

                if (pendingCaptureWindows.Count() == 0)
                {
                    firstEventTime = now;
                }
                pendingCaptureWindows.Add(hwnd);

                // figure out if all pending capture moves are OS initiated
                double elapsedMs = (now - firstEventTime).TotalMilliseconds;
                if (userMoves == 0
                    && pendingCaptureWindows.Count >= MinOsMoveWindows
                    && elapsedMs * MaxUserMovePerSecond / 1000 < pendingCaptureWindows.Count)
                {
                    osMove = true;
                    Log.Trace("os move detected. user moves :{0}, total moved windows : {1}, elapsed milliseconds {2}",
                        userMoves, pendingCaptureWindows.Count, elapsedMs);
                }

                if (restoringWindowPos)
                {
                    switch (eventType)
                    {
                        case User32Events.EVENT_SYSTEM_FOREGROUND:
                            // if user opened new window, don't abort restore, as new window is not affected by restore anyway
                            return;
                        case User32Events.EVENT_OBJECT_LOCATIONCHANGE:
                            break;
                        case User32Events.EVENT_SYSTEM_MINIMIZESTART:
                        case User32Events.EVENT_SYSTEM_MINIMIZEEND:
                        case User32Events.EVENT_SYSTEM_MOVESIZESTART:
                        case User32Events.EVENT_SYSTEM_MOVESIZEEND:
                            Log.Trace("User aborted restore by actively maneuver window");
                            var thread = new Thread(() =>
                            {
                                try
                                {
                                    lock (databaseLock)
                                    {
                                        StartCaptureApplicationsOnCurrentDisplays();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex.ToString());
                                }
                            });
                            thread.Start();
                            return;
                    }

                    CancelCaptureTimer();
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
                    if (eventType != User32Events.EVENT_OBJECT_LOCATIONCHANGE)
                    {
                        userMoves++;
                    }

                    if (userMoves > 0)
                    {
                        StartCaptureTimer();
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
                monitorApplications.Add(displayKey, new Dictionary<string, ApplicationDisplayMetrics>());
            }

            ApplicationDisplayMetrics curDisplayMetrics = null;
            if (IsWindowMoved(displayKey, window, eventType, now, out curDisplayMetrics))
            {
                Log.Trace("Capturing windows for display setting {0}", displayKey);
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
                    monitorApplications[displayKey].Add(curDisplayMetrics.Key, curDisplayMetrics);
                }
                else
                {
                    monitorApplications[displayKey][curDisplayMetrics.Key].WindowPlacement = curDisplayMetrics.WindowPlacement;
                    monitorApplications[displayKey][curDisplayMetrics.Key].ScreenPosition = curDisplayMetrics.ScreenPosition;
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

        private void BeginCaptureApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    string displayKey = GetDisplayKey();
                    if (displayKey != validDisplayKeyForCapture)
                    {
                        // discard the capture request due to display setting change
                        Log.Trace("Discard capture for {0}, when expecting {1}", displayKey, validDisplayKeyForCapture);
                        return;
                    }

                    if (restoringWindowPos && userMoves == 0 && pendingCaptureWindows.Count == 0)
                    {
                        return;
                    }

                    if (osMove)
                    {
                        // postpone capture to wait for restore
                        osMove = false;
                        if (!restoringWindowPos)
                        {
                            StartCaptureTimer(MaxCaptureLatency);
                        }
                        return;
                    }

                    lock (databaseLock)
                    {
                        StartCaptureApplicationsOnCurrentDisplays();
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
                CancelCaptureTimer();
                pendingCaptureWindows.Clear();
                userMoves = 0;
            }
        }

        private void StartCaptureApplicationsOnCurrentDisplays()
        {
            restoringWindowPos = false;
            osMove = false;
            ResetState();
            CaptureApplicationsOnCurrentDisplays();
        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey = null)
        {
            var appWindows = CaptureWindowsOfInterest();
            int cnt = 0;
            if (displayKey == null)
            {
                displayKey = GetDisplayKey();
            }

            foreach (var window in appWindows)
            {
                DateTime now = DateTime.Now;
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
                                .Where(row => row.Parent.HWnd.ToInt64() == 0
                                    && !string.IsNullOrEmpty(row.Title)
                                    //&& !row.Title.Equals("Program Manager")
                                    //&& !row.Title.Contains("Task Manager")
                                    //&& row.Position.Height != 0
                                    //&& row.Position.Width != 0
                                    && row.Visible
                                    );
        }

        private bool IsWindowMoved(string displayKey, SystemWindow window, User32Events eventType, DateTime now, out ApplicationDisplayMetrics curDisplayMetrics)
        {
            curDisplayMetrics = null;

            if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
            {
                return false;
            }

            IntPtr hwnd = window.HWnd;
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
                // these function calls are very cpu-intensive
                ApplicationName = window.Process.ProcessName,
#else
                ApplicationName = "",
#endif
                ProcessId = processId,

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
                ApplicationDisplayMetrics prevDisplayMetrics = monitorApplications[displayKey][curDisplayMetrics.Key];
                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId)
                {
                    // key collision between dead window and new window with the same hwnd
                    monitorApplications[displayKey].Remove(curDisplayMetrics.Key);
                    moved = true;
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

        private void BeginRestoreApplicationsOnCurrentDisplays()
        {
            lock (controlLock)
            {
                if (!restoringWindowPos)
                {
                    return;
                }

                CancelCaptureTimer();

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
                    ApplicationDisplayMetrics prevDisplayMetrics = monitorApplications[displayKey][applicationKey];
                    // looks like the window is still here for us to restore
                    WindowPlacement windowPlacement = prevDisplayMetrics.WindowPlacement;
                    IntPtr hwnd = prevDisplayMetrics.HWnd;

                    ApplicationDisplayMetrics curDisplayMetrics = null;
                    if (!IsWindowMoved(displayKey, window, 0, DateTime.Now, out curDisplayMetrics))
                    {
                        // window position has no change
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
                    RECT rect = prevDisplayMetrics.ScreenPosition;
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
