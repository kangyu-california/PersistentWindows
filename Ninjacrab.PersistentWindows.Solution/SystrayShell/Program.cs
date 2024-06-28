using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

using PersistentWindows.Common;
using PersistentWindows.Common.WinApiBridge;
using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static readonly string ProjectUrl = "https://www.github.com/kangyu-california/PersistentWindows";
        public static readonly string Contributors = $@"{ProjectUrl}/graphs/contributors";
        public static System.Drawing.Icon IdleIcon = null;
        public static System.Drawing.Icon BusyIcon = null;
        public static System.Drawing.Icon UpdateIcon = null;
        public static string AppdataFolder = null;
        public static string CmdArgs;
        public static bool Gui = true;
        public static bool hotkey_window = true;
        public static uint hotkey = 'W'; //Alt + W

        private const int MaxSnapshots = 38; // 0-9, a-z, ` and final one for undo

        static PersistentWindowProcessor pwp = null;    
        static SystrayForm systrayForm = null;
        static bool silent = false; //suppress all balloon tip & sound prompt
        static bool notification = false; //pop balloon when auto restore
        static int delay_manual_capture = 5000; //in millisecond

        // capture to db
        static uint pid = 0;
        static string commandline;
        static int lineno = 0;

        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Log.Init();
            Log.Event($" {Application.ProductVersion}; OS version: {Environment.OSVersion.VersionString}; .NET version: {Environment.Version}");

            pwp = new PersistentWindowProcessor();

            bool splash = true;
            int delay_start = 0;
            bool relaunch = false;
            int delay_manual_capture = 0;
            int delay_auto_capture = 0;
            bool redirect_appdata = false; // use "." instead of appdata/local/PersistentWindows to store db file
            bool prompt_session_restore = false;
            int delay_auto_restore = 0;
            int halt_restore = 0; //seconds to wait before trying restore again, due to frequent monitor config changes
            string ignore_process = "";
            int debug_process = 0;
            bool fix_zorder = false;
            bool fix_zorder_specified = false;
            bool show_desktop = false; //show desktop when display changes
            bool redraw_desktop = false;
            bool offscreen_fix = true;
            bool fix_unminimized_window = true;
            bool enhanced_offscreen_fix = false;
            bool auto_restore_missing_windows = false;
            bool auto_restore_from_db_at_startup = false;
            bool launch_once_per_process_id = true;
            bool check_upgrade = true;
            bool auto_upgrade = false;
            bool legacy_icon = false;
            bool waiting_taskbar = false;

            foreach (var arg in args)
            {
                CmdArgs += arg + " ";

                if (halt_restore > 1)
                {
                    halt_restore = 0;
                    pwp.haltRestore = (Int32)(float.Parse(arg) * 1000);
                    continue;
                }
                else if (delay_start != 0)
                {
                    delay_start = 0;
                    if (!waiting_taskbar)
                    {
                        Thread.Sleep((Int32)(float.Parse(arg) * 1000));
                        relaunch = true;
                    }
                    continue;
                }
                else if (delay_manual_capture != 0)
                {
                    delay_manual_capture = 0;
                    Program.delay_manual_capture = (Int32)(float.Parse(arg) * 1000);
                    continue;
                }
                else if (delay_auto_capture != 0)
                {
                    delay_auto_capture = 0;
                    pwp.UserForcedCaptureLatency = (Int32)(float.Parse(arg) * 1000);
                    continue;
                }
                else if (delay_auto_restore != 0)
                {
                    delay_auto_restore = 0;
                    pwp.UserForcedRestoreLatency = (Int32)(float.Parse(arg) * 1000);
                    continue;
                }
                else if (debug_process != 0)
                {
                    debug_process = 0;
                    pwp.SetDebugProcess(arg);
                    continue;
                }
                else if (ignore_process.Length > 0)
                {
                    ignore_process = "";
                    pwp.SetIgnoreProcess(arg);
                    continue;
                }
                else if (hotkey == 1)
                {
                    hotkey = arg[0];
                    continue;
                }

                switch(arg)
                {
                    case "-legacy_icon":
                        legacy_icon = true;
                        break;
                    case "-nogui":
                    case "-gui=0":
                        Gui = false;
                        break;
                    case "-silent":
                        Log.silent = true;
                        silent = true;
                        splash = false;
                        break;
                    case "-splash_off":
                    case "-splash=0":
                        splash = false;
                        break;
                    case "-enable_auto_restore_by_manual_capture":
                        pwp.manualNormalSession = true;
                        break;
                    case "-delay_start":
                        delay_start = 1;
                        break;
                    case "-wait_taskbar":
                        waiting_taskbar = true;
                        break;
                    case "-delay_manual_capture":
                        delay_manual_capture = 1;
                        break;
                    case "-delay_auto_capture":
                        delay_auto_capture = 1;
                        break;
                    case "-dpi_sensitive_call=1":
                        User32.DpiSenstiveCall = true;
                        break;
                    case "-reject_scale_factor_change=0":
                        pwp.rejectScaleFactorChange = false;
                        break;
                    case "-redirect_appdata":
                        redirect_appdata = true;
                        break;
                    case "-ignore_process":
                        ignore_process = "_foo_";
                        break;
                    case "-debug_process":
                        debug_process = 1;
                        break;
                    case "-show_desktop_when_display_changes":
                        show_desktop = true;
                        break;
                    case "-enhanced_offscreen_fix":
                        enhanced_offscreen_fix = true;
                        break;
                    case "-disable_offscreen_fix":
                        offscreen_fix = false;
                        break;
                    case "-offscreen_fix=0":
                    case "-fix_offscreen=0":
                    case "-fix_offscreen_window=0":
                        offscreen_fix = false;
                        break;
                    case "-fix_unminimized=0":
                    case "-fix_unminimized_window=0":
                        fix_unminimized_window = false;
                        break;
                    case "-fix_taskbar=0":
                        pwp.fixTaskBar = false;
                        break;
                    case "-foreground_background_dual_position=0":
                        pwp.enableDualPosSwitch = false;
                        break;
                    case "-ctrl_minimize_to_tray=0":
                        pwp.enableMinimizeToTray = false;
                        break;
                    case "-hotkey_window=0":
                    case "-webpage_commander_window=0":
                        hotkey_window = false;
                        break;
                    case "-hotkey":
                        hotkey = 1;
                        break;
                    case "-prompt_session_restore":
                        prompt_session_restore = true;
                        break;
                    case "-halt_restore":
                        halt_restore = 1;
                        break;
                    case "-delay_auto_restore":
                        delay_auto_restore = 1;
                        break;
                    case "-notification_on":
                    case "-notification=1":
                        notification = true;
                        break;
                    case "-fix_zorder=0":
                        fix_zorder = false;
                        fix_zorder_specified = true;
                        break;
                    case "-fix_zorder":
                    case "-fix_zorder=1":
                        fix_zorder = true;
                        fix_zorder_specified = true;
                        break;
                    case "-redraw_desktop":
                        redraw_desktop = true;
                        break;
                    case "-auto_restore_missing_windows":
                    case "-auto_restore_missing_windows=1":
                        auto_restore_missing_windows = true;
                        break;
                    case "-auto_restore_missing_windows=2":
                        auto_restore_from_db_at_startup = true;
                        break;
                    case "-auto_restore_missing_windows=3":
                        auto_restore_from_db_at_startup = true;
                        auto_restore_missing_windows = true;
                        break;
                    case "-auto_restore_new_display_session_from_db=0":
                        pwp.autoRestoreLiveWindows = false;
                        Log.Error("turn off auto restore db for new session");
                        break;
                    case "-invoke_multi_window_process_only_once=0":
                        launch_once_per_process_id = false;
                        break;
                    case "-check_upgrade=0":
                        check_upgrade = false;
                        break;
                    case "-auto_upgrade=1":
                        auto_upgrade = true;
                        break;
                }
            }

            string productName = System.Windows.Forms.Application.ProductName;
            string appDataFolder = redirect_appdata ? "." :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    productName);
            string iconFolder = appDataFolder;
#if DEBUG
            //avoid db path conflict with release version
            //appDataFolder = ".";
            appDataFolder = AppDomain.CurrentDomain.BaseDirectory;
#endif
            AppdataFolder = appDataFolder;

            // default icons
            IdleIcon = legacy_icon ? Properties.Resources.pwIcon2 : Properties.Resources.pwIcon;
            var iconHandle = Properties.Resources.pwIconBusy.GetHicon();
            BusyIcon = legacy_icon ? Properties.Resources.pwIconBusy2 : System.Drawing.Icon.FromHandle(iconHandle);
            iconHandle = Properties.Resources.pwIconUpdate.GetHicon();
            UpdateIcon = System.Drawing.Icon.FromHandle(iconHandle);

            // customized icon/png
            for (int i = 0; i < 2; i++)
            {
                if (i == 1)
                    iconFolder = AppDomain.CurrentDomain.BaseDirectory;

                string icon_path = Path.Combine(iconFolder, "pwIcon.ico");
                string icon_png_path = Path.Combine(iconFolder, "pwIcon.png");
                if (File.Exists(icon_png_path))
                {
                    var bitmap = new System.Drawing.Bitmap(icon_png_path); // or get it from resource
                    IdleIcon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
                else if (File.Exists(icon_path))
                {
                    IdleIcon = new System.Drawing.Icon(icon_path);
                }

                icon_path = Path.Combine(iconFolder, "pwIconBusy.ico");
                icon_png_path = Path.Combine(iconFolder, "pwIconBusy.png");
                if (File.Exists(icon_png_path))
                {
                    var bitmap = new System.Drawing.Bitmap(icon_png_path);
                    BusyIcon = System.Drawing.Icon.FromHandle(bitmap.GetHicon());
                }
                else if (File.Exists(icon_path))
                {
                    BusyIcon = new System.Drawing.Icon(icon_path);
                }
            }

            systrayForm = new SystrayForm();
            systrayForm.enableUpgradeNotice = check_upgrade;
            systrayForm.autoUpgrade = auto_upgrade;

            if (relaunch)
            {
                Restart();
                return;
            }

            if (!waiting_taskbar)
            {
                bool ready = WaitTaskbarReady();
                if (!ready)
                    return;
            }

            PersistentWindowProcessor.icon = IdleIcon;
            if (fix_zorder_specified)
            {
                if (fix_zorder)
                    pwp.fixZorder = 2; //force z-order recovery for all
                else
                    pwp.fixZorder = 0; //no z-order recovery at all
            }
            else
            {
                // pwp.fixZorder = 1 //do z-order recovery only for snapshot 
            }

            pwp.showRestoreTip = ShowRestoreTip;
            pwp.hideRestoreTip = HideRestoreTip;
            pwp.enableRestoreMenu = EnableRestoreMenu;
            pwp.enableRestoreSnapshotMenu = EnableRestoreSnapshotMenu;
            pwp.changeIconText = ChangeIconText;
            pwp.showDesktop = show_desktop;
            pwp.redrawDesktop = redraw_desktop;
            pwp.redirectAppDataFolder = redirect_appdata;
            pwp.enhancedOffScreenFix = enhanced_offscreen_fix;
            pwp.enableOffScreenFix = offscreen_fix;
            pwp.fixUnminimizedWindow = fix_unminimized_window;
            pwp.promptSessionRestore = prompt_session_restore;
            pwp.autoRestoreMissingWindows = auto_restore_missing_windows;
            pwp.launchOncePerProcessId = launch_once_per_process_id;
            if (ignore_process.Length > 0)
                pwp.SetIgnoreProcess(ignore_process);

            if (hotkey_window)
                HotKeyForm.Start(hotkey);

            if (!pwp.Start(auto_restore_from_db_at_startup))
            {
                systrayForm.notifyIconMain.Visible = false;
                return;
            }

            if (splash)
            {
                StartSplashForm();
            }

            //systrayForm.notifyIconMain.Visible = false;

            Application.Run();
        }

        static bool WaitTaskbarReady()
        {
            if (Gui == false)
                return true;

            try
            {
                IntPtr taskbar = User32.FindWindowA("Shell_TrayWnd", null);
                IntPtr hWndTrayNotify = User32.FindWindowEx(taskbar, IntPtr.Zero, "TrayNotifyWnd", null);
                IntPtr hWndSysPager = User32.FindWindowEx(hWndTrayNotify, IntPtr.Zero, "SysPager", null);
                IntPtr hWndToolbar = User32.FindWindowEx(hWndSysPager, IntPtr.Zero, "ToolbarWindow32", null);
                if (hWndToolbar != IntPtr.Zero)
                    return true;
            }
            catch (Exception )
            {
                Log.Error("taskbar not ready, restart PersistentWindows");
            }

            Restart();
            return false;
        }

        static void Restart()
        {
            string batFile = Path.Combine(AppdataFolder, $"pw_restart.bat");
            string content = "timeout /t 10 /nobreak > NUL";
            content += "\nstart \"\" /B \"" + Path.Combine(Application.StartupPath, Application.ProductName) + ".exe\" " + "-wait_taskbar " + Program.CmdArgs;
            File.WriteAllText(batFile, content);
            Process.Start(batFile);

            Log.Error("program restarted");
        }

        public static void ShowRestoreTip()
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action)delegate ()
                {
                    ShowRestoreTip();
                });
            else
            {
                NotifyIcon ni = systrayForm.notifyIconMain;
                ni.Icon = BusyIcon;

                if (silent)
                    return;

                if (Gui)
                    ni.Visible = true;

                if (!notification)
                    return;

                ni.ShowBalloonTip(5000);
            }
        }

        public static void HideRestoreTip(bool show_icon = true)
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action)delegate ()
                {
                    HideRestoreTip(show_icon);
                });
            else
            {
                NotifyIcon ni = systrayForm.notifyIconMain;
                ni.Icon = IdleIcon;

                if (Gui)
                {
                    if (show_icon)
                        ni.Visible = true;
                    else
                        ni.Visible = false;
                }
            }
        }

        static void EnableRestoreMenu(bool enableRestoreDB, bool checkUpgrade)
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action) delegate ()
                {
                    EnableRestoreMenu(enableRestoreDB, checkUpgrade);
                });
            else
                systrayForm.UpdateMenuEnable(enableRestoreDB, checkUpgrade);
        }

        static void EnableRestoreSnapshotMenu(bool enable)
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action) delegate ()
                {
                    EnableRestoreSnapshotMenu(enable);
                });
            else
                systrayForm.EnableSnapshotRestore(enable);
        }

        static System.Threading.Timer snapshot_timer; 
        static public void CaptureSnapshot(int id, bool prompt = true, bool delayCapture = false)
        {
            snapshot_timer = new System.Threading.Timer(state =>
            {
                if (!pwp.TakeSnapshot(id))
                    return;

                if (!silent)
                {
                    char c = SnapshotIdToChar(id);
                    if (prompt)
                        systrayForm.notifyIconMain.ShowBalloonTip(5000, $"snapshot '{c}' is captured", $"click icon then immediately press key '{c}' to restore the snapshot", ToolTipIcon.Info);
                }

                EnableRestoreSnapshotMenu(true);
                snapshot_timer.Dispose();
            });

            snapshot_timer.Change(delayCapture ? delay_manual_capture : 0, Timeout.Infinite);
        }

        static public void ChangeIconText(string text)
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action) delegate ()
                {
                    ChangeIconText(text);
                });
            else
            {
                if (String.IsNullOrEmpty(text))
                    text = $"{Application.ProductName} {Application.ProductVersion}";
                systrayForm.notifyIconMain.Text = text.Substring(0, Math.Min(40, text.Length));
            }
        }

        static public char SnapshotIdToChar(int id)
        {
            char c;
            if (id < 10)
            {
                c = '0';
                c += (char)id;
            }
            else if (id == MaxSnapshots - 2)
                c = '`';
            else
            {
                c = 'a';
                c += (char)(id - 10);
            }

            return c;
        }

        static public int SnapshotCharToId(char c)
        {
            if (c == '`' || c == '~')
                return MaxSnapshots - 2;
            if (c < '0')
                return -1;
            if (c > 'z')
                return -1;
            if (c <= '9')
                return (int)(c - '0');
            if (c < 'a')
                return -1;
            return (int)(c - 'a' + 10);
        }

        static void StartSplashForm()
        {
            var thread = new Thread(() =>
            {
                Application.Run(new SplashForm());
            });
            thread.IsBackground = false;
            thread.Priority = ThreadPriority.Highest;
            thread.Name = "StartSplashForm";
            thread.Start();
        }

        static public char EnterSnapshotName()
        {
            var profileDlg = new LayoutProfile();
            profileDlg.Icon = IdleIcon;
            profileDlg.ShowDialog(systrayForm);

            return profileDlg.snapshot_name;
        }

        static public string EnterDbEntryName()
        {
            var dlg = new NameDbEntry();
            dlg.Icon = IdleIcon;
            dlg.ShowDialog(systrayForm);

            return dlg.db_entry_name;
        }

        static System.Threading.Timer capture_to_hdd_timer;
        static public void CaptureToDisk()
        {
            capture_to_hdd_timer = new System.Threading.Timer(state =>
            {
                GetProcessInfo();
                pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);

                capture_to_hdd_timer.Dispose();
            });

            //shift key pressed, delay capture
            bool delay_capture = false;
            if ((User32.GetKeyState(0x10) & 0x8000) != 0)
                delay_capture = true;

            pwp.dbDisplayKey = pwp.GetDisplayKey();
            if ((User32.GetKeyState(0x11) & 0x8000) != 0) //ctrl key pressed
            {
                var name = EnterDbEntryName();
                if (String.IsNullOrEmpty(name))
                    return;
                pwp.dbDisplayKey += name;
            }

            capture_to_hdd_timer.Change(delay_capture ? delay_manual_capture : 0, Timeout.Infinite);
        }

        static public void RestoreFromDisk(bool ask_dialog)
        {
            bool ctrl_key_pressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
            bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
            if (ask_dialog || (shift_key_pressed && !ctrl_key_pressed))
            {
                var listCollection = pwp.GetDbCollections();
                var dlg = new DbKeySelect();
                foreach (var collection in listCollection)
                {
                    dlg.InsertCollection(collection);
                }
                //dlg.InsertCollection(new string('a', 256));
                dlg.Icon = IdleIcon;
                dlg.ShowDialog(systrayForm);
                var result = dlg.result;
                if (String.IsNullOrEmpty(result))
                    return;

                pwp.dbDisplayKey = result;
            }
            else
            {
                pwp.dbDisplayKey = pwp.GetDisplayKey();
                if (ctrl_key_pressed)
                {
                    if (shift_key_pressed)
                        pwp.autoInitialRestoreFromDB = true;
                    else
                    {
                        var name = EnterDbEntryName();
                        if (String.IsNullOrEmpty(name))
                            return;

                        pwp.dbDisplayKey += name;
                    }
                }
            }

            pwp.restoringFromDB = true;
            pwp.StartRestoreTimer(milliSecond : 1000 /*wait mouse settle still for taskbar restore*/);
        }

        static public void RestoreSnapshot(int id)
        {
            pwp.RestoreSnapshot(id);
        }

        static public void FgWindowToBottom()
        {
            pwp.FgWindowToBottom();
        }

        static public void RecallLastKilledPosition()
        {
            pwp.RecallLastPosition(PersistentWindowProcessor.GetForegroundWindow());
        }

        static public void CenterWindow()
        {
            pwp.CenterWindow(PersistentWindowProcessor.GetForegroundWindow());
        }

        static public void PauseAutoRestore()
        {
            pwp.pauseAutoRestore = true;
            pwp.sessionActive = false; //disable capture as well
        }

        static public void ResumeAutoRestore()
        {
            pwp.pauseAutoRestore = false;
            pwp.restoringFromMem = true;
            pwp.StartRestoreTimer();
        }

        static void GetProcessInfo()
        {
            Process process = new Process();

            var os_version = Environment.OSVersion;
            if (os_version.Version.Major < 10)
            {
                process.StartInfo.FileName = "wmic.exe";
                //process.StartInfo.Arguments = "process get caption,commandline,processid /format:csv";
                process.StartInfo.Arguments = "process get commandline,processid /format:csv";
                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandlerWmic);
            }
            else
            {
                process.StartInfo.FileName = "powershell.exe";
                process.StartInfo.Arguments = "get-ciminstance win32_process | select processid,commandline | format-list";
                process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            }

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = false;


            // Start process and handlers
            process.Start();
            process.BeginOutputReadLine();
            //process.BeginErrorReadLine();
            process.WaitForExit();

            pid = 0;
            lineno = 0;
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            string line = outLine.Data;
            lineno++;

            if (string.IsNullOrEmpty(line))
            {
                if (pid != 0)
                {
                    pwp.processCmd[pid] = commandline;
                }
            }
            else if (line.StartsWith("processid"))
            {
                uint.TryParse(line.Split(':')[1], out pid);
            }
            else if (line.StartsWith("commandline"))
            {
                commandline = line.Substring(14);
            }
            else if (line.Length > 14)
            {
                commandline += line.Substring(14);
            }
        }

        static void OutputHandlerWmic(object sendingProcess, DataReceivedEventArgs outLine)
        {
            //* Do your stuff with the output (write to console/log/StringBuilder)
            string line = outLine.Data;
            if (string.IsNullOrEmpty(line))
                return;
            string[] fields = line.Split(',');
            if (fields.Length < 3)
                return;
            uint processId;
            if (uint.TryParse(fields[2], out processId))
            {
                if (!string.IsNullOrEmpty(fields[1]))
                {
                    pwp.processCmd[processId] = fields[1];
                }
            }
        }

        public static void LogError(string format, params object[] args)
        {
            Log.Error(format, args);
        }

    }
}
