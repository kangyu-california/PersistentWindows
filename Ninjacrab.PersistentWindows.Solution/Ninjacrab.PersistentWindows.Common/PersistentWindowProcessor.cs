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
        private const int captureLatency = 2000; // microseconds to wait for window position capture
        private const int restoreLatency = 1500; // microseconds to wait for window position recovery

        // key data structure
        private Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>> monitorApplications = null;

        // control
        private bool betweenCaptureRestore = false; // neither capture nor restore
        private bool restoringWindowPos = false; // about to restore
        private Timer captureTimer;
        private Timer restoreTimer;
        private Object dataLock;

        // callbacks
        private PowerModeChangedEventHandler powerModeChangedHandler;
        private EventHandler displaySettingsChangingHandler;
        private EventHandler displaySettingsChangedHandler;

        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();
        private User32.WinEventDelegate winEventsCaptureDelegate;

        public void Start()
        {
            monitorApplications = new Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>>();
            dataLock = new object();
            BeginCaptureApplicationsOnCurrentDisplays();

            captureTimer = new Timer(state =>
            {
                Log.Trace("Capture timer expired");
                BeginCaptureApplicationsOnCurrentDisplays();
            });

            restoreTimer = new Timer(state =>
            {
                Log.Trace("Restore timer expired");
                BeginRestoreApplicationsOnCurrentDisplays();
            });

            winEventsCaptureDelegate = WinEventProc;

            /*
            // captures user click, snap and minimize
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
                User32Events.EVENT_SYSTEM_MINIMIZEEND, //window restored
                User32Events.EVENT_SYSTEM_MINIMIZEEND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));
            */

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
                    betweenCaptureRestore = true;
                };

            SystemEvents.DisplaySettingsChanging += this.displaySettingsChangingHandler;

            this.displaySettingsChangedHandler =
                (s, e) =>
                {
                    DateTime date = DateTime.Now;
                    Log.Info("Display settings changed {0}", date);

                    restoreTimer.Change(restoreLatency, Timeout.Infinite);
                    betweenCaptureRestore = false;
                    restoringWindowPos = true;
                };

            SystemEvents.DisplaySettingsChanged += this.displaySettingsChangedHandler;

            powerModeChangedHandler =
                (s, e) =>
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            Log.Info("System suspending");
                            betweenCaptureRestore = true;
                            break;

                        case PowerModes.Resume:
                            Log.Info("System Resuming");
                            //restoreTimer.Change(restoreLatency, Timeout.Infinite);
                            betweenCaptureRestore = false;

                            break;
                    }
                };

            SystemEvents.PowerModeChanged += powerModeChangedHandler;
        }

        private void WinEventProc(IntPtr hWinEventHook, User32Events eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (betweenCaptureRestore)
            {
                return;
            }

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

            Thread captureWindowThread = new Thread(() =>
            {
                try
                {
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

                    if (restoringWindowPos)
                    {
                        restoreTimer.Change(restoreLatency, Timeout.Infinite);
                    }
                    else
                    {
                        captureTimer.Change(captureLatency, Timeout.Infinite);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            });
            captureWindowThread.IsBackground = false;
            captureWindowThread.Start();
        }

        private bool CaptureWindow(SystemWindow window, User32Events eventType, DateTime now, string displayKey = null)
        {
            bool ret = false;

            if (displayKey == null)
            {
                DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                displayKey = metrics.Key;
            }

            if (!monitorApplications.ContainsKey(displayKey))
            {
                monitorApplications.Add(displayKey, new Dictionary<string, ApplicationDisplayMetrics>());
            }

            ApplicationDisplayMetrics curDisplayMetrics = null;
            if (NeedUpdateWindow(displayKey, window, eventType, now, out curDisplayMetrics))
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

        private void BeginCaptureApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    lock (dataLock)
                    {
                        CaptureApplicationsOnCurrentDisplays();
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

        private void CaptureApplicationsOnCurrentDisplays(string displayKey = null)
        {
            var appWindows = CaptureWindowsOfInterest();
            int cnt = 0;
            foreach (var window in appWindows)
            {
                DateTime now = DateTime.Now;
                if (CaptureWindow(window, 0, now, null))
                {
                    cnt++;
                }
            }

            Log.Trace("{0} windows captured", cnt);
        }

        private IEnumerable<SystemWindow> CaptureWindowsOfInterest()
        {
            return SystemWindow.AllToplevelWindows
                                .Where(row => row.Parent.HWnd.ToInt64() == 0
                                    && !string.IsNullOrEmpty(row.Title)
                                    //&& !row.Title.Equals("Program Manager")
                                    //&& !row.Title.Contains("Task Manager")
                                    && row.Visible
                                    );
        }

        private bool NeedUpdateWindow(string displayKey, SystemWindow window, User32Events eventType, DateTime now, out ApplicationDisplayMetrics curDisplayMetrics)
        {
            if (!window.IsValid() || string.IsNullOrEmpty(window.ClassName))
            {
                curDisplayMetrics = null;
                return false;
            }

            IntPtr hwnd = window.HWnd;
            WindowPlacement windowPlacement = new WindowPlacement();
            User32.GetWindowPlacement(window.HWnd, ref windowPlacement);

            // compensate for GetWindowPlacement() failure to get real coordinate of snapped window
            RECT screenPosition = new RECT();
            User32.GetWindowRect(hwnd, ref screenPosition);

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
                ScreenPosition = screenPosition
            };

            bool needUpdate = false;
            if (!monitorApplications[displayKey].ContainsKey(curDisplayMetrics.Key))
            {
                needUpdate = true;
            }
            else
            {
                ApplicationDisplayMetrics prevDisplayMetrics = monitorApplications[displayKey][curDisplayMetrics.Key];
                if (prevDisplayMetrics.ProcessId != curDisplayMetrics.ProcessId)
                {
                    // key collision between dead window and new window with the same hwnd
                    monitorApplications[displayKey].Remove(curDisplayMetrics.Key);
                    needUpdate = true;
                }
                else if (!prevDisplayMetrics.ScreenPosition.Equals(curDisplayMetrics.ScreenPosition))
                {
                    needUpdate = true;
                }
                else if (!prevDisplayMetrics.EqualPlacement(curDisplayMetrics))
                {
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

                    /*
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
                    if (restoringWindowPos)
                    {
                        needUpdate = true;
                    }
                }
                else
                {
                    // nothing changed except event type & time
                }
            }

            return needUpdate;
        }

        private void BeginRestoreApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    lock (dataLock)
                    {
                        RestoreApplicationsOnCurrentDisplays();
                        restoringWindowPos = false;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            });
            thread.IsBackground = false;
            thread.Name = "PersistentWindowProcessor.RestoreApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private bool RestoreApplicationsOnCurrentDisplays(string displayKey = null, SystemWindow sWindow = null)
        {
            int score = 0;
            if (displayKey == null)
            {
                DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                displayKey = metrics.Key;
            }

            if (!monitorApplications.ContainsKey(displayKey)
                || monitorApplications[displayKey].Count == 0)
            {
                // the display setting has not been captured yet
                Log.Trace("Unknown display setting {0}", displayKey);
                return false;
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
                    if (!NeedUpdateWindow(displayKey, window, 0, DateTime.Now, out curDisplayMetrics))
                    {
                        // window position has no change
                        continue;
                    }

                    bool success;
                    // recover NormalPosition (the workspace position prior to snap)
                    success = User32.SetWindowPlacement(hwnd, ref windowPlacement);
                    Log.Info("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                        window.Process.ProcessName,
                        windowPlacement.NormalPosition.Left,
                        windowPlacement.NormalPosition.Top,
                        windowPlacement.NormalPosition.Width,
                        windowPlacement.NormalPosition.Height,
                        success);

                    // recover previous screen position
                    RECT rect = prevDisplayMetrics.ScreenPosition;
                    success |= User32.MoveWindow(hwnd, rect.Left, rect.Top, rect.Width, rect.Height, true);
                    Log.Info("MoveWindow({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                        window.Process.ProcessName,
                        rect.Left,
                        rect.Top,
                        rect.Width,
                        rect.Height,
                        success);

                    if (success)
                    {
                        score++;
                    }
                    else
                    {
                        string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        Log.Error(error);
                    }
                }
            }

            Log.Trace("Restored windows position for display setting {0}", displayKey);

            return score > 0;
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
