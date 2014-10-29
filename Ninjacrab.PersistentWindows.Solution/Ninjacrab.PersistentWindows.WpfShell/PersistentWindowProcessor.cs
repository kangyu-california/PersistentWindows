using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedWinapi.Windows;
using Microsoft.Win32;
using Ninjacrab.PersistentWindows.WpfShell.Diagnostics;
using Ninjacrab.PersistentWindows.WpfShell.Models;
using Ninjacrab.PersistentWindows.WpfShell.WinApiBridge;
using NLog;

namespace Ninjacrab.PersistentWindows.WpfShell
{
    public class PersistentWindowProcessor
    {
        private DesktopDisplayMetrics lastMetrics = null;

        public void Start()
        {
            var thread = new Thread(InternalRun);
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.InternalRun()";
            thread.Start();

            SystemEvents.DisplaySettingsChanged += (s, e) => BeginRestoreApplicationsOnCurrentDisplays();
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
            CaptureApplicationsOnCurrentDisplays();
            lastMetrics = DesktopDisplayMetrics.AcquireMetrics();
        }

        private readonly Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>> monitorApplications = new Dictionary<string, Dictionary<string, ApplicationDisplayMetrics>>();
        private readonly object displayChangeLock = new object();

        private void InternalRun()
        {
            while(true)
            {
                CaptureApplicationsOnCurrentDisplays();
                Thread.Sleep(1000);
            }
        }

        private void BeginCaptureApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() => CaptureApplicationsOnCurrentDisplays());
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.BeginCaptureApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private void CaptureApplicationsOnCurrentDisplays(string displayKey = null)
        {            
            lock(displayChangeLock)
            {
                DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                if (displayKey == null)
                {
                    displayKey = metrics.Key;
                }

                if (!metrics.Equals(lastMetrics))
                {
                    // since the resolution doesn't match, lets wait till it's restored
                    Log.Info("Detected changes in display metrics, will capture once windows are restored");
                    return;
                }

                if (!monitorApplications.ContainsKey(displayKey))
                {
                    monitorApplications.Add(displayKey, new Dictionary<string, ApplicationDisplayMetrics>());
                }

                List<string> changeLog = new List<string>();
                var windows = SystemWindow.AllToplevelWindows.Where(row => row.VisibilityFlag == true);
                foreach (var window in windows)
                {
                    WindowPlacement windowPlacement = new WindowPlacement();
                    User32.GetWindowPlacement(window.HWnd, ref windowPlacement);

                    var applicationDisplayMetric = new ApplicationDisplayMetrics
                    {
                        HWnd = window.HWnd,
                        ApplicationName = window.Process.ProcessName,
                        ProcessId = window.Process.Id,
                        WindowPlacement = windowPlacement
                    };

                    bool addToChangeLog = false;
                    if (!monitorApplications[displayKey].ContainsKey(applicationDisplayMetric.Key))
                    {
                        monitorApplications[displayKey].Add(applicationDisplayMetric.Key, applicationDisplayMetric);
                        addToChangeLog = true;
                    }
                    else if (!monitorApplications[displayKey][applicationDisplayMetric.Key].EqualPlacement(applicationDisplayMetric))
                    {
                        monitorApplications[displayKey][applicationDisplayMetric.Key].WindowPlacement = applicationDisplayMetric.WindowPlacement;
                        addToChangeLog = true;
                    }

                    if (addToChangeLog)
                    {
                        changeLog.Add(string.Format("Capturing {0} at [{1}x{2}] size [{3}x{4}]",
                            applicationDisplayMetric.ApplicationName,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Left,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Top,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Width,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Height
                            ));
                    }
                }

                if (changeLog.Count > 0)
                {
                    Log.Info("Capturing applications for {0}", displayKey);
                    Log.Trace(string.Join(Environment.NewLine, changeLog));
                }
            }
        }

        private void BeginRestoreApplicationsOnCurrentDisplays()
        {
            var thread = new Thread(() => RestoreApplicationsOnCurrentDisplays());
            thread.IsBackground = true;
            thread.Name = "PersistentWindowProcessor.RestoreApplicationsOnCurrentDisplays()";
            thread.Start();
        }

        private void RestoreApplicationsOnCurrentDisplays(string displayKey = null)
        {
            lock (displayChangeLock)
            {
                DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                if (displayKey == null)
                {
                    displayKey = metrics.Key;
                }

                lastMetrics = DesktopDisplayMetrics.AcquireMetrics();
                if (!monitorApplications.ContainsKey(displayKey))
                {
                    // no old profile, we're done
                    Log.Info("No old profile found for {0}", displayKey);
                    return;
                }

                Log.Info("Restoring applications for {0}", displayKey);
                foreach (var window in SystemWindow.AllToplevelWindows.Where(row => row.VisibilityFlag == true))
                {
                    string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), window.Process.ProcessName);
                    if (monitorApplications[displayKey].ContainsKey(applicationKey))
                    {
                        // looks like the window is still here for us to restore
                        WindowPlacement windowPlacement = monitorApplications[displayKey][applicationKey].WindowPlacement;

                        if (windowPlacement.ShowCmd == ShowWindowCommands.Maximize)
                        {
                            // When restoring maximized windows, it occasionally switches res and when the maximized setting is restored
                            // the window thinks it's maxxed, but does not eat all the real estate. So we'll temporarily unmaximize then
                            // re-apply that
                            windowPlacement.ShowCmd = ShowWindowCommands.Normal;
                            User32.SetWindowPlacement(monitorApplications[displayKey][applicationKey].HWnd, ref windowPlacement);
                            windowPlacement.ShowCmd = ShowWindowCommands.Maximize;
                        }
                        var success = User32.SetWindowPlacement(monitorApplications[displayKey][applicationKey].HWnd, ref windowPlacement);
                        if(!success)
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
    }
}
