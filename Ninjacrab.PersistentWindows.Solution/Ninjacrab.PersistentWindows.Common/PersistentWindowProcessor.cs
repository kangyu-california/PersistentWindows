using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedWinapi;
using ManagedWinapi.Hooks;
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
        private const int MaxAppsMoveUpdate = 4;
        private int pendingUpdateTimer = 0;
        private Hook windowProcHook = null;
        private Dictionary<string, SortedDictionary<string, ApplicationDisplayMetrics>> monitorApplications = null;
        private object displayChangeLock = null;

        public void Start()
        {
            monitorApplications = new Dictionary<string, SortedDictionary<string, ApplicationDisplayMetrics>>();
            displayChangeLock = new object();
            CaptureApplicationsOnCurrentDisplays(initialCapture: true);

            var thread = new Thread(InternalRun);
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.InternalRun()";
            thread.Start();

            SystemEvents.DisplaySettingsChanged += 
                (s, e) =>
                {
                    Log.Info("Display settings changed");
                    BeginRestoreApplicationsOnCurrentDisplays();
                };

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

        }


        private void InternalRun()
        {
            while(true)
            {
                Thread.Sleep(1000);
                CaptureApplicationsOnCurrentDisplays();
            }
        }

        private void BeginCaptureApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() => CaptureApplicationsOnCurrentDisplays());
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.BeginCaptureApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey = null, bool initialCapture = false)
        {            
            lock(displayChangeLock)
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
                    ApplicationDisplayMetrics app = null;
                    if (NeedUpdateWindow(displayKey, window, out app))
                    {
                        updateApps.Add(app);
                        updateLogs.Add(string.Format("Captured {0,-8} at [{1,4}x{2,4}] size [{3,4}x{4,4}] V:{5} {6} ",
                            app,
                            app.WindowPlacement.NormalPosition.Left,
                            app.WindowPlacement.NormalPosition.Top,
                            app.WindowPlacement.NormalPosition.Width,
                            app.WindowPlacement.NormalPosition.Height,
                            window.Visible,
                            window.Title
                            ));
                    }
                }

                if (!initialCapture && updateLogs.Count > MaxAppsMoveUpdate)
                {
                    // this is an undesirable status which could be caused by either of the following,
                    // 1. a new rdp session attemp to move/resize ALL windows even for the same display settings.
                    // 2. window pos/size recovery is incomplete due to lack of admin permission of this program.
                    // 3. moving many windows using mouse too fast.

                    // The remedy for issue 1
                    // wait up to 60 seconds to give DisplaySettingsChanged event handler a chance to recover.
                    ++pendingUpdateTimer;
                    if (pendingUpdateTimer < 60)
                    {
                        Log.Trace("Waiting for display setting recovery");
                        return;
                    }

                    // the remedy for issue 2 and 3
                    // acknowledge the status quo and proceed to recapture all windows
                    initialCapture = true;
                    Log.Trace("Full capture timer triggered");
                }

                if (pendingUpdateTimer != 0)
                {
                    Log.Trace("pending update timer value is {0}", pendingUpdateTimer);
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
                        ApplicationDisplayMetrics app = updateApps[i];
                        commitUpdateLog.Add(updateLogs[i]);
                        if (!monitorApplications[displayKey].ContainsKey(app.Key))
                        {
                            monitorApplications[displayKey].Add(app.Key, app);
                        }
                        else if (!monitorApplications[displayKey][app.Key].EqualPlacement(app))
                        {
                            monitorApplications[displayKey][app.Key].WindowPlacement = app.WindowPlacement;
                        }
                    }

                    commitUpdateLog.Sort();
                    Log.Trace("{0}{1}{2} windows captured", string.Join(Environment.NewLine, commitUpdateLog), Environment.NewLine, commitUpdateLog.Count);
                    pendingUpdateTimer = 0;
                }
            }
        }

        private IEnumerable<SystemWindow> CaptureWindowsOfInterest()
        {
            return SystemWindow.AllToplevelWindows
                                .Where(row => row.Parent.HWnd.ToInt64() == 0
                                    //&& !string.IsNullOrEmpty(row.Title)
                                    //&& !row.Title.Equals("Program Manager")
                                    //&& !row.Title.Contains("Task Manager")
                                    && row.Visible
                                    );
        }

        private bool NeedUpdateWindow(string displayKey, SystemWindow window, out ApplicationDisplayMetrics applicationDisplayMetric)
        {
            WindowPlacement windowPlacement = new WindowPlacement();
            User32.GetWindowPlacement(window.HWnd, ref windowPlacement);

            /*
            if (windowPlacement.ShowCmd == ShowWindowCommands.Normal)
            {
                User32.GetWindowRect(window.HWnd, ref windowPlacement.NormalPosition);
            }
            */

            applicationDisplayMetric = new ApplicationDisplayMetrics
            {
                HWnd = window.HWnd,

#if DEBUG
                // these function calls are very cpu-intensive
                ApplicationName = window.Process.ProcessName,
                ProcessId = window.Process.Id,
#else
                ApplicationName = "",
                ProcessId = 0,
#endif

                WindowPlacement = windowPlacement
            };

            bool needUpdate = false;
            if (!monitorApplications[displayKey].ContainsKey(applicationDisplayMetric.Key))
            {
                needUpdate = true;
            }
            else if (!monitorApplications[displayKey][applicationDisplayMetric.Key].EqualPlacement(applicationDisplayMetric))
            {
                needUpdate = true;
            }
            return needUpdate;
        }

        private void BeginRestoreApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() => 
            {
                try
                {
                    RestoreApplicationsOnCurrentDisplays();
                }
                catch(Exception ex)
                {
                    Log.Error(ex.ToString());
                }
            });
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.RestoreApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private void RestoreApplicationsOnCurrentDisplays(string displayKey = null)
        {
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
                    CaptureApplicationsOnCurrentDisplays(displayKey, initialCapture: true);
                    return;
                }

                Log.Info("Restoring applications for {0}", displayKey);
                foreach (var window in CaptureWindowsOfInterest())
                {
                    var proc_name = window.Process.ProcessName;
                    if (proc_name.Contains("CodeSetup"))
                    {
                        // prevent hang in SetWindowPlacement()
                        continue;
                    }

#if DEBUG
                    string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), window.Process.ProcessName);
#else
                    string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), "");
#endif            
                    if (monitorApplications[displayKey].ContainsKey(applicationKey))
                    {
                        // looks like the window is still here for us to restore
                        WindowPlacement windowPlacement = monitorApplications[displayKey][applicationKey].WindowPlacement;
                        IntPtr hwnd = monitorApplications[displayKey][applicationKey].HWnd;
                        if (!User32.IsWindow(hwnd))
                        {
                            continue;
                        }

                        ApplicationDisplayMetrics app = null;
                        if (!NeedUpdateWindow(displayKey, window, out app))
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

                        var success = User32.SetWindowPlacement(monitorApplications[displayKey][applicationKey].HWnd, ref windowPlacement);
                        if (!success)
                        {
                            string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                            Log.Error(error);
                        }
                        Log.Info("SetWindowPlacement({0} [{1}x{2}]-[{3}x{4}]) - {5}",
                            window.Process.ProcessName,
                            windowPlacement.NormalPosition.Left,
                            windowPlacement.NormalPosition.Top,
                            windowPlacement.NormalPosition.Width,
                            windowPlacement.NormalPosition.Height,
                            success);
                    }
                }
                Log.Trace("Restored windows position for display setting {0}", displayKey);
            }
        }

        public void Dispose()
        {
            if (windowProcHook != null)
            {
                windowProcHook.Dispose();
            }
        }
    }
}
