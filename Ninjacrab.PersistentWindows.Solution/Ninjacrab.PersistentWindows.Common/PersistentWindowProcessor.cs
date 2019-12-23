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
        private int AppsMovedThreshold = 2;
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

            SystemEvents.DisplaySettingsChanged += (s, e) =>
                {
                    Log.Info("Display settings changed");
                    BeginRestoreApplicationsOnCurrentDisplays();
                };
            SystemEvents.PowerModeChanged += (s, e) =>
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

                List<string> changeLog = new List<string>();
                List<ApplicationDisplayMetrics> changeApps = new List<ApplicationDisplayMetrics>();
                var appWindows = CaptureWindowsOfInterest();
                foreach (var window in appWindows)
                {
                    ApplicationDisplayMetrics app = null;
                    if (AddOrUpdateWindow(displayKey, window, out app))
                    {
                        changeApps.Add(app);
                        changeLog.Add(string.Format("CAOCD - Capturing {0,-8} at [{1,4}x{2,4}] size [{3,4}x{4,4}] V:{5} {6} ",
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

                if (!initialCapture && changeLog.Count > AppsMovedThreshold)
                {
                    // starting an rdp session may abruptly change window size/position, 
                    // wait for BeginRestoreApplicationsOnCurrentDisplays() to undo such unwanted change
                    return;
                }

                int maxChangeCnt = changeLog.Count;
                if (!initialCapture && maxChangeCnt > AppsMovedThreshold)
                {
                    maxChangeCnt = AppsMovedThreshold;
                }

                List<string> commitChangeLog = new List<string>();
                for (int i = 0; i < maxChangeCnt; i++)
                {
                    ApplicationDisplayMetrics app = changeApps[i];
                    commitChangeLog.Add(changeLog[i]);
                    if (!monitorApplications[displayKey].ContainsKey(app.Key))
                    {
                        monitorApplications[displayKey].Add(app.Key, app);
                    }
                    else if (!monitorApplications[displayKey][app.Key].EqualPlacement(app))
                    {
                        monitorApplications[displayKey][app.Key].WindowPlacement = app.WindowPlacement;
                    }
                }

                if (maxChangeCnt > 0)
                {
                    commitChangeLog.Sort();
                    Log.Info("{0}Capturing applications for {1}", initialCapture ? "Initial " : "", displayKey);
                    Log.Trace("{0} windows recorded{1}{2}", commitChangeLog.Count, Environment.NewLine, string.Join(Environment.NewLine, commitChangeLog));
                }
            }
        }

        private IEnumerable<SystemWindow> CaptureWindowsOfInterest()
        {
            return SystemWindow.AllToplevelWindows
                                .Where(row => row.Parent.HWnd.ToInt64() == 0
                                    && !string.IsNullOrEmpty(row.Title)
                                    && !row.Title.Equals("Program Manager")
                                    && !row.Title.Contains("Task Manager")
                                    && row.Visible);
        }

        private bool AddOrUpdateWindow(string displayKey, SystemWindow window, out ApplicationDisplayMetrics applicationDisplayMetric)
        {
            WindowPlacement windowPlacement = new WindowPlacement();
            User32.GetWindowPlacement(window.HWnd, ref windowPlacement);

            if (windowPlacement.ShowCmd == ShowWindowCommands.Normal)
            {
                User32.GetWindowRect(window.HWnd, ref windowPlacement.NormalPosition);
            }

            applicationDisplayMetric = new ApplicationDisplayMetrics
            {
                HWnd = window.HWnd,

                // avoid cpu intensive operation
                //ApplicationName = window.Process.ProcessName,
                //ProcessId = window.Process.Id,
                ApplicationName = "",
                ProcessId = 0,

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
                    // nothing to restore since not captured yet
                    Log.Trace("No old profile found for {0}", displayKey);
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

                    //string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), window.Process.ProcessName);
                    string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), "");
                    if (monitorApplications[displayKey].ContainsKey(applicationKey))
                    {
                        // looks like the window is still here for us to restore
                        WindowPlacement windowPlacement = monitorApplications[displayKey][applicationKey].WindowPlacement;
                        IntPtr hwnd = monitorApplications[displayKey][applicationKey].HWnd;
                        if (!User32.IsWindow(hwnd))
                        {
                            continue;
                        }

                        if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                        {
                            // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                            // the window thinks it's maxxed, but does not eat all the real estate. So we'll temporarily unmaximize then
                            // re-apply that
                            windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                            User32.SetWindowPlacement(hwnd, ref windowPlacement);
                            windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                        }

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
