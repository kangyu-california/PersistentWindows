using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using Ninjacrab.PersistentWindows.Common.Diagnostics;
using Ninjacrab.PersistentWindows.Common.Models;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.Common
{
    public class PersistentWindowProcessor : IDisposable
    {
        // read and update this from a config file eventually
        private const int MaxAppsMoveUpdate = 2;
        private const int MaxAppsMoveDelay = 60; // accept massive app move in 60 seconds
        private int pendingAppsMoveTimer = 0;
        private int pendingAppMoveSum = 0;
        private Dictionary<string, SortedDictionary<string, ApplicationDisplayMetrics>> monitorApplications = null;
        private Thread pollingThread = null;
        private object displayChangeLock = null;
        private readonly List<IntPtr> winEventHooks = new List<IntPtr>();

        EventHandler displaySettingsChangedHandler;
        User32.WinEventDelegate winEventsCaptureDelegate;

        public void Start()
        {
            monitorApplications = new Dictionary<string, SortedDictionary<string, ApplicationDisplayMetrics>>();
            displayChangeLock = new object();
            CaptureApplicationsOnCurrentDisplays(initialCapture: true);

            pollingThread = new Thread(InternalRun);
            pollingThread.IsBackground = false;
            pollingThread.Name = "PersistentWindowProcessor.InternalRun()";
            pollingThread.Start();

            winEventsCaptureDelegate = WinEventProc;
            // Any movements around clicking/dragging
            this.winEventHooks.Add(User32.SetWinEventHook(
                (uint)User32Events.EVENT_SYSTEM_MOVESIZEEND,
                (uint)User32Events.EVENT_SYSTEM_MOVESIZEEND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            // This seems to cover most moves involving snaps and minmize/restore
            this.winEventHooks.Add(User32.SetWinEventHook(
                (uint)User32Events.EVENT_SYSTEM_FOREGROUND,
                (uint)User32Events.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero,
                winEventsCaptureDelegate,
                0,
                0,
                (uint)User32Events.WINEVENT_OUTOFCONTEXT));

            displaySettingsChangedHandler =
                (s, e) =>
                {
                    Log.Info("Display settings changed");
                    BeginRestoreApplicationsOnCurrentDisplays();
                };

            SystemEvents.DisplaySettingsChanged += displaySettingsChangedHandler;

            /*
            SystemEvents.PowerModeChanged += 
                (s, e) =>
                {
                    switch (e.Mode)
                    {
                        case PowerModes.Suspend:
                            Log.Info("System Suspending");
                            BeginCaptureApplicationsOnCurrentDisplays();
                            break;

                        case PowerModes.Resume:
                            Log.Info("System Resuming");
                            BeginRestoreApplicationsOnCurrentDisplays();
                            break;
                    }
                };
            */

        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            // Console.WriteLine("WinEvent received. Type: {0:x8}, Window: {1:x8}", eventType, hwnd.ToInt32());
            BeginCaptureApplicationsOnCurrentDisplays();
        }

        private void InternalRun()
        {
            while (true)
            {
                Thread.Sleep(1000);
                if (pendingAppMoveSum > 0)
                {
                    // keep started tick continue to count
                    BeginCaptureApplicationsOnCurrentDisplays();
                }
            }
        }

        private void BeginCaptureApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() =>
            {
                try
                {
                    CaptureApplicationsOnCurrentDisplays();
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

        private void CaptureApplicationsOnCurrentDisplays(string displayKey = null, bool initialCapture = false)
        {
            lock (displayChangeLock)
            {
                if (displayKey == null)
                {
                    DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                    displayKey = metrics.Key;
                }

                if (!monitorApplications.ContainsKey(displayKey))
                {
                    monitorApplications.Add(displayKey, new SortedDictionary<string, ApplicationDisplayMetrics>());
                }

                List<string> updateLogs = new List<string>();
                List<ApplicationDisplayMetrics> updateApps = new List<ApplicationDisplayMetrics>();
                var appWindows = CaptureWindowsOfInterest();
                foreach (var window in appWindows)
                {
                    ApplicationDisplayMetrics curDisplayMetrics = null;
                    if (NeedUpdateWindow(displayKey, window, out curDisplayMetrics))
                    {
                        updateApps.Add(curDisplayMetrics);
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
                        updateLogs.Add(log + log2);
                    }
                }

                if (!initialCapture)
                {
                    if (updateLogs.Count == 0 && pendingAppMoveSum == 0)
                    {
                        return;
                    }

                    ++pendingAppsMoveTimer;
                    pendingAppMoveSum += updateLogs.Count;

                    if (pendingAppsMoveTimer < 3)
                    {
                        // wait up to 3 seconds before commit update
                        return;
                    }
                }

                if (!initialCapture && pendingAppMoveSum > MaxAppsMoveUpdate * pendingAppsMoveTimer)
                {
                    // this is an undesirable status which could be caused by either of the following,
                    // 1. a new rdp session attemp to move/resize ALL windows even for the same display settings.
                    // 2. window pos/size recovery is incomplete due to lack of admin permission of this program.
                    // 3. moving many windows using mouse too fast.

                    // wait up to 60 seconds to give DisplaySettingsChanged event handler a chance to recover.
                    if (pendingAppsMoveTimer < MaxAppsMoveDelay)
                    {
                        Log.Trace("Waiting for display setting recovery");
                        return;
                    }

                    // acknowledge the status quo and proceed to recapture all windows
                    initialCapture = true;
                    Log.Trace("Full capture timer triggered");
                }

                if (pendingAppsMoveTimer != 0)
                {
                    Log.Trace("pending update timer value is {0}", pendingAppsMoveTimer);
                }

                int maxUpdateCnt = updateLogs.Count;
                if (!initialCapture && maxUpdateCnt > MaxAppsMoveUpdate)
                {
                    maxUpdateCnt = MaxAppsMoveUpdate;
                }

                if (maxUpdateCnt > 0)
                {
                    Log.Trace("{0}Capturing windows for display setting {1}", initialCapture ? "Initial " : "", displayKey);

                    List<string> commitUpdateLog = new List<string>();
                    for (int i = 0; i < maxUpdateCnt; i++)
                    {
                        ApplicationDisplayMetrics curDisplayMetrics = updateApps[i];
                        commitUpdateLog.Add(updateLogs[i]);
                        if (!monitorApplications[displayKey].ContainsKey(curDisplayMetrics.Key))
                        {
                            monitorApplications[displayKey].Add(curDisplayMetrics.Key, curDisplayMetrics);
                        }
                        else
                        {
                            monitorApplications[displayKey][curDisplayMetrics.Key].WindowPlacement = curDisplayMetrics.WindowPlacement;
                            monitorApplications[displayKey][curDisplayMetrics.Key].ScreenPosition = curDisplayMetrics.ScreenPosition;
                        }
                    }

                    //commitUpdateLog.Sort();
                    Log.Trace("{0}{1}{2} windows captured", string.Join(Environment.NewLine, commitUpdateLog), Environment.NewLine, commitUpdateLog.Count);
                }
                pendingAppsMoveTimer = 0;
                pendingAppMoveSum = 0;
            }
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

        private bool NeedUpdateWindow(string displayKey, SystemWindow window, out ApplicationDisplayMetrics curDisplayMetrics)
        {
            if (!window.IsValid())
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
                RecoverWindowPlacement = true,
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

                    if (monitorApplications[displayKey][curDisplayMetrics.Key].RecoverWindowPlacement)
                    {
                        // try recover previous placement first
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
                    lock (displayChangeLock)
                    {
                        for (int i = 0; i < 3 && RestoreApplicationsOnCurrentDisplays(); ++i)
                        {
                            // make sure unexpected WindowPlacement change by Window OS are all corrected
                            Thread.Sleep(500);
                        }
                        CaptureApplicationsOnCurrentDisplays(initialCapture: true);
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

        private bool RestoreApplicationsOnCurrentDisplays(string displayKey = null)
        {
            bool score = false;
            lock (displayChangeLock)
            {
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
                foreach (var window in CaptureWindowsOfInterest())
                {
                    if (!window.IsValid())
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
                        if (!NeedUpdateWindow(displayKey, window, out curDisplayMetrics))
                        {
                            // window position has no change
                            continue;
                        }

                        /*
                        if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                        {
                            // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                            // the window thinks it's maxxed, but does not eat all the real estate. So we'll temporarily unmaximize then
                            // re-apply that
                            windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                            User32.SetWindowPlacement(hwnd, ref windowPlacement);
                            windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                        }
                        */

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

                        score = true;
                        if (!success)
                        {
                            string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                            Log.Error(error);
                        }
                    }
                }

                Log.Trace("Restored windows position for display setting {0}", displayKey);
            }

            return score;
        }

        public void Stop()
        {
            if (pollingThread != null)
            {
                pollingThread.Abort();
            }
        }

        #region IDisposable

        public virtual void Dispose(bool disposing)
        {
            if (this.displaySettingsChangedHandler != null)
            {
                SystemEvents.DisplaySettingsChanged -= this.displaySettingsChangedHandler;
            }

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
