using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

using Ninjacrab.PersistentWindows.Common;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;
using Ninjacrab.PersistentWindows.Common.Diagnostics;

namespace Ninjacrab.PersistentWindows.SystrayShell
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
        public static string AppdataFolder = null;
        public static string CmdArgs;
        public static bool Gui = true;

        static PersistentWindowProcessor pwp = null;    
        static SystrayForm systrayForm = null;
        static bool silent = false; //suppress all balloon tip & sound prompt
        static bool notification = false; //pop balloon when auto restore

        [STAThread]
        static void Main(string[] args)
        {
            bool splash = true;
            int delay_start = 0;
            bool redirect_appdata = false; // use "." instead of appdata/local/PersistentWindows to store db file
            bool prompt_session_restore = false;
            bool slow_restore = false;
            int  halt_restore = 0; //seconds to wait before trying restore again, due to frequent monitor config changes
            bool halt_restore_specified = false;
            string ignore_process = "";
            bool dry_run = false; //dry run mode without real restore, for debug purpose only
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

            foreach (var arg in args)
            {
                CmdArgs += arg + " ";

                if (halt_restore_specified)
                {
                    halt_restore_specified = false;
                    halt_restore = Int32.Parse(arg);
                    continue;
                }
                else if (delay_start != 0)
                {
                    delay_start = 0;
                    Thread.Sleep(Int32.Parse(arg) * 1000);
                    continue;
                }
                else if (ignore_process.Length > 0)
                {
                    ignore_process = arg;
                    continue;
                }

                switch(arg)
                {
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
                    case "-delay_start":
                        delay_start = 1;
                        break;
                    case "-redirect_appdata":
                        redirect_appdata = true;
                        break;
                    case "-ignore_process":
                        ignore_process = "_foo_";
                        break;
                    case "-show_desktop_when_display_changes":
                        LogEvent("show desktop = 1");
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
                    case "-prompt_session_restore":
                        prompt_session_restore = true;
                        break;
                    case "-slow_restore":
                        slow_restore = true;
                        break;
                    case "-halt_restore":
                        halt_restore_specified = true;
                        break;
                    case "-notification_on":
                    case "-notification=1":
                        notification = true;
                        break;
                    case "-dry_run":
                        dry_run = true;
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

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string productName = System.Windows.Forms.Application.ProductName;
            string appDataFolder = redirect_appdata ? "." :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    productName);
#if DEBUG
            //avoid db path conflict with release version
            //appDataFolder = ".";
            appDataFolder = AppDomain.CurrentDomain.BaseDirectory;
#endif
            AppdataFolder = appDataFolder;

            string icon_path = Path.Combine(appDataFolder, "pwIcon.ico");
            if (File.Exists(icon_path))
            {
                IdleIcon = new System.Drawing.Icon(icon_path);
            }
            else
            {
                IdleIcon = Properties.Resources.pwIcon;
            }

            icon_path = Path.Combine(appDataFolder, "pwIconBusy.ico");
            if (File.Exists(icon_path))
            {
                BusyIcon = new System.Drawing.Icon(icon_path);
            }
            else
            {
                BusyIcon = Properties.Resources.pwIconBusy;
            }

            systrayForm = new SystrayForm();
            systrayForm.enableUpgradeNotice = check_upgrade;
            systrayForm.autoUpgrade = auto_upgrade;
            if (check_upgrade)
                systrayForm.upgradeNoticeMenuItem.Text = "Disable upgrade notice";
            else
                systrayForm.upgradeNoticeMenuItem.Text = "Enable upgrade notice";

            pwp = new PersistentWindowProcessor();
            pwp.icon = IdleIcon;
            pwp.dryRun = dry_run;
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
            pwp.showDesktop = show_desktop;
            pwp.redrawDesktop = redraw_desktop;
            pwp.redirectAppDataFolder = redirect_appdata;
            pwp.enhancedOffScreenFix = enhanced_offscreen_fix;
            pwp.enableOffScreenFix = offscreen_fix;
            pwp.fixUnminimizedWindow = fix_unminimized_window;
            pwp.promptSessionRestore = prompt_session_restore;
            pwp.autoRestoreMissingWindows = auto_restore_missing_windows;
            pwp.launchOncePerProcessId = launch_once_per_process_id;
            pwp.haltRestore = halt_restore;
            pwp.slowRestore = slow_restore;
            if (ignore_process.Length > 0)
                pwp.SetIgnoreProcess(ignore_process);

            if (!pwp.Start(auto_restore_from_db_at_startup))
            {
                systrayForm.notifyIconMain.Visible = false;
                return;
            }

            if (splash)
            {
                StartSplashForm();
            }

            Application.Run();
        }

        static void ShowRestoreTip()
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

                //systrayForm.notifyIconMain.Visible = false;
                if (Gui)
                    ni.Visible = true;

                if (!notification)
                    return;

                ni.ShowBalloonTip(5000);
            }
        }

        static void HideRestoreTip()
        {
            if (systrayForm.contextMenuStripSysTray.InvokeRequired)
                systrayForm.contextMenuStripSysTray.BeginInvoke((Action)delegate ()
                {
                    HideRestoreTip();
                });
            else
            {
                NotifyIcon ni = systrayForm.notifyIconMain;
                ni.Icon = IdleIcon;

                /*
                if (silent)
                    return;
                */

                //systrayForm.notifyIconMain.Visible = false;
                if (Gui)
                    ni.Visible = true;
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

        static public void CaptureSnapshot(int id, bool prompt = true)
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
        }

        static public void ChangeZorderMethod()
        {
            pwp.fixZorderMethod++;
            systrayForm.notifyIconMain.Text = $"{Application.ProductName} {Application.ProductVersion} {pwp.fixZorderMethod}";
        }

        static public char SnapshotIdToChar(int id)
        {
            char c;
            if (id < 10)
            {
                c = '0';
                c += (char)id;
            }
            else
            {
                c = 'a';
                c += (char)(id - 10);
            }

            return c;
        }

        static public int SnapshotCharToId(char c)
        {
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

        static public void CaptureToDisk()
        {
            pwp.dbDisplayKey = pwp.GetDisplayKey();
            if ((User32.GetKeyState(0x11) & 0x8000) != 0) //ctrl key pressed
            {
                pwp.dbDisplayKey += EnterDbEntryName();
            }

            GetProcessInfo();
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);
        }

        static public void RestoreFromDisk()
        {
            pwp.restoringFromDB = true;
            pwp.curDisplayKey = pwp.dbDisplayKey = pwp.GetDisplayKey();
            if ((User32.GetKeyState(0x11) & 0x8000) != 0) //ctrl key pressed
            {
                pwp.dbDisplayKey += EnterDbEntryName();
            }
            pwp.StartRestoreTimer(milliSecond : 2000 /*wait mouse settle still for taskbar restore*/);
        }

        static public void RestoreSnapshot(int id)
        {
            pwp.dbDisplayKey = null;
            pwp.curDisplayKey = pwp.GetDisplayKey();
            pwp.RestoreSnapshot(id);
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
            process.StartInfo.FileName = "wmic.exe";
            //process.StartInfo.Arguments = "process get caption,commandline,processid /format:csv";
            process.StartInfo.Arguments = "process get commandline,processid /format:csv";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = false;

            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(OutputHandler);

            // Start process and handlers
            process.Start();
            process.BeginOutputReadLine();
            //process.BeginErrorReadLine();
            process.WaitForExit();
        }

        static void OutputHandler(object sendingProcess, DataReceivedEventArgs outLine)
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

        public static void LogEvent(string format, params object[] args)
        {
            Log.Event(format, args);
        }

        public static void LogError(string format, params object[] args)
        {
            Log.Error(format, args);
        }
    }
}
