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
        private int AppsMovedThreshold = 4;
        private DesktopDisplayMetrics lastMetrics = null;
        private Hook windowProcHook;
        private Dictionary<string, SortedDictionary<string, ApplicationDisplayMetrics>> monitorApplications = null;
        private object displayChangeLock = null;

        public void Start()
        {
            lastMetrics = DesktopDisplayMetrics.AcquireMetrics();
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

            /*
            windowProcHook = new Hook();
            windowProcHook.Type = HookType.WH_CALLWNDPROC;
            windowProcHook.Callback += GlobalWindowProcCallback;
            windowProcHook.StartHook();
            */
        }

        int GlobalWindowProcCallback(int code, IntPtr wParam, IntPtr lParam, ref bool callNext)
        {
            CallWindowProcedureParam callbackParam = (CallWindowProcedureParam)Marshal.PtrToStructure(lParam, typeof(CallWindowProcedureParam));
            switch(callbackParam.message)
            {
                case WindowsMessage.WINDOWPOSCHANGED:
                    WindowPositionChangedHandler(callbackParam);
                    break;

                case WindowsMessage.POWERBROADCAST:
                    Log.Info("Power Broadcast - {0}    {1}", wParam, lParam);
                    break;

                case WindowsMessage.ACTIVATE:
                case WindowsMessage.ACTIVATEAPP:
                case WindowsMessage.CAPTURECHANGED:
                case WindowsMessage.ENTERSIZEMOVE:
                case WindowsMessage.ERASEBKGND:
                case WindowsMessage.EXITSIZEMOVE:
                case WindowsMessage.GETTEXT:
                case WindowsMessage.GETICON:
                case WindowsMessage.GETMINMAXINFO:
                case WindowsMessage.HSHELL_ACTIVATESHELLWINDOW:
                case WindowsMessage.IME_NOTIFY:
                case WindowsMessage.IME_SETCONTEXT:
                case WindowsMessage.KILLFOCUS:
                case WindowsMessage.MOVING:
                case WindowsMessage.NCACTIVATE:
                case WindowsMessage.NCCALCSIZE:
                case WindowsMessage.NCHITTEST:
                case WindowsMessage.NCPAINT:
                case WindowsMessage.NULL:
                case WindowsMessage.SETCURSOR:
                case WindowsMessage.SIZING:
                case WindowsMessage.SIZE:
                case WindowsMessage.WININICHANGE:
                case WindowsMessage.WINDOWPOSCHANGING:
                    break;

                default:
                    int enumValue = (int)callbackParam.message;
                    switch(enumValue)
                    {
                        case 647:
                        case 49666: 
                            break;

                        default:
                            Log.Info(callbackParam.message.ToString());
                            break;
                    }
                    break;
            }
            callNext = true;
            return 0;
        }

        /// <summary>
        /// OMG this method is awful!!! but yagni
        /// </summary>
        /// <param name="callbackParam"></param>
        private void WindowPositionChangedHandler(CallWindowProcedureParam callbackParam)
        {
            ApplicationDisplayMetrics appMetrics = null;
            if (monitorApplications == null ||
                !monitorApplications.ContainsKey(lastMetrics.Key))
            {
                Log.Error("No definitions found for this resolution: {0}", lastMetrics.Key);
                return;
            }

            appMetrics = monitorApplications[lastMetrics.Key]
                .FirstOrDefault(row => row.Value.HWnd == callbackParam.hwnd)
                .Value;

            if (appMetrics == null)
            {
                var newAppWindow = SystemWindow.AllToplevelWindows
                    .FirstOrDefault(row => row.Parent.HWnd.ToInt64() == 0 
                        && !string.IsNullOrEmpty(row.Title)
                        && !row.Title.Equals("Program Manager")
                        && !row.Title.Contains("Task Manager")
                        && row.Visible
                        && row.HWnd == callbackParam.hwnd);

                if (newAppWindow == null)
                {
                    Log.Error("Can't find hwnd {0}", callbackParam.hwnd.ToInt64());
                    return;
                }
                ApplicationDisplayMetrics applicationDisplayMetric = null;
                AddOrUpdateWindow(lastMetrics.Key, newAppWindow, out applicationDisplayMetric);
                return;
            }

            WindowPlacement windowPlacement = appMetrics.WindowPlacement;
            WindowsPosition newPosition = (WindowsPosition)Marshal.PtrToStructure(callbackParam.lparam, typeof(WindowsPosition));
            windowPlacement.NormalPosition.Left = newPosition.Left;
            windowPlacement.NormalPosition.Top = newPosition.Top;
            windowPlacement.NormalPosition.Right = newPosition.Left + newPosition.Width;
            windowPlacement.NormalPosition.Bottom = newPosition.Top + newPosition.Height;

            var key = appMetrics.Key;
            if (monitorApplications[lastMetrics.Key].ContainsKey(key))
            {
                monitorApplications[lastMetrics.Key][key].WindowPlacement = windowPlacement;
            }
            else
            {
                Log.Error("Hwnd {0} is not in list, we should capture", callbackParam.hwnd.ToInt64());
                return;
            }

            Log.Info("WPCH - Capturing {0} at [{1}x{2}] size [{3}x{4}]",
                appMetrics,
                appMetrics.WindowPlacement.NormalPosition.Left,
                appMetrics.WindowPlacement.NormalPosition.Top,
                appMetrics.WindowPlacement.NormalPosition.Width,
                appMetrics.WindowPlacement.NormalPosition.Height
                );
        }

        private void InternalRun()
        {
            while(true)
            {
                Thread.Sleep(1500);
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
                    monitorApplications.Add(displayKey, new SortedDictionary<string, ApplicationDisplayMetrics>());
                }

                var appWindows = CaptureWindowsOfInterest();

                List<string> changeLog = new List<string>();
                List<ApplicationDisplayMetrics> apps = new List<ApplicationDisplayMetrics>();
                foreach (var window in appWindows)
                {
                    ApplicationDisplayMetrics applicationDisplayMetric = null;
                    bool addToChangeLog = AddOrUpdateWindow(displayKey, window, out applicationDisplayMetric);

                    if (addToChangeLog)
                    {
                        apps.Add(applicationDisplayMetric);
                        changeLog.Add(string.Format("CAOCD - Capturing {0,-45} at [{1,4}x{2,4}] size [{3,4}x{4,4}] V:{5} {6} ",
                            applicationDisplayMetric,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Left,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Top,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Width,
                            applicationDisplayMetric.WindowPlacement.NormalPosition.Height,
                            window.Visible,
                            window.Title
                            ));
                    }
                }

                // only save the updated if it didn't seem like something moved everything
                if ((apps.Count > 0 && apps.Count < AppsMovedThreshold) 
                    || initialCapture)
                {
                    foreach(var app in apps)
                    {
                        if (!monitorApplications[displayKey].ContainsKey(app.Key))
                        {
                            monitorApplications[displayKey].Add(app.Key, app);
                        }
                        else if (!monitorApplications[displayKey][app.Key].EqualPlacement(app))
                        {
                            monitorApplications[displayKey][app.Key].WindowPlacement = app.WindowPlacement;
                        }
                    }
                    changeLog.Sort();
                    Log.Info("{0}Capturing applications for {1}", initialCapture ? "Initial " : "", displayKey);
                    Log.Trace("{0} windows recorded{1}{2}", apps.Count, Environment.NewLine, string.Join(Environment.NewLine, changeLog));
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
                //ApplicationName = window.Process.ProcessName,
                ApplicationName = "..",
                //ProcessId = window.Process.Id,
                ProcessId = 0,
                WindowPlacement = windowPlacement
            };

            bool updated = false;
            if (!monitorApplications[displayKey].ContainsKey(applicationDisplayMetric.Key))
            {
                updated = true;
            }
            else if (!monitorApplications[displayKey][applicationDisplayMetric.Key].EqualPlacement(applicationDisplayMetric))
            {
                updated = true;
            }
            return updated;
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
                DesktopDisplayMetrics metrics = DesktopDisplayMetrics.AcquireMetrics();
                if (displayKey == null)
                {
                    displayKey = metrics.Key;
                }

                /// lastMetrics = DesktopDisplayMetrics.AcquireMetrics();
                if (!monitorApplications.ContainsKey(displayKey))
                {
                    // no old profile, we're done
                    Log.Trace("No old profile found for {0}", displayKey);
                    /// CaptureApplicationsOnCurrentDisplays(initialCapture: true);
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
                    string applicationKey = string.Format("{0}-{1}", window.HWnd.ToInt64(), "..");
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
