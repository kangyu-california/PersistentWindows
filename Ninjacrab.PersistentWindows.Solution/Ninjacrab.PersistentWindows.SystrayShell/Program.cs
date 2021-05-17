using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;

using Ninjacrab.PersistentWindows.Common;
using Ninjacrab.PersistentWindows.Common.Diagnostics;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static readonly string ProjectUrl = "https://www.github.com/kangyu-california/PersistentWindows";
        public static System.Drawing.Icon IdleIcon = null;
        public static System.Drawing.Icon BusyIcon = null;
        public static string AppdataFolder = null;
        public static string CmdArgs;

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
            bool sticky_display_config = false; // experiment switch to speed up restore by pre-restore when monitor goes to sleep 
            int  halt_restore = 0; //seconds to wait before trying restore again, due to frequent monitor config changes
            bool halt_restore_specified = false;
            bool dry_run = false; //dry run mode without real restore, for debug purpose only
            bool fix_zorder = false;
            bool fix_zorder_specified = false;
            bool redraw_desktop = false;
            bool offscreen_fix = true;
            bool fix_unminimized_window = true;
            bool enhanced_offscreen_fix = false;
            bool auto_restore_missing_windows = false;
            bool auto_restore_from_db_at_startup = false;
            bool restore_one_window_per_process = false;
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

                switch(arg)
                {
                    case "-silent":
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
                    case "-sticky_display_config=1":
                        sticky_display_config = true;
                        break;
                    case "-prompt_session_restore":
                        prompt_session_restore = true;
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
                    case "-restore_one_window_per_process=1":
                        restore_one_window_per_process = true;
                        break;
                    case "-check_upgrade=0":
                        check_upgrade = false;
                        break;
                    case "-auto_upgrade=1":
                        auto_upgrade = true;
                        break;
                }
            }

            while (String.IsNullOrEmpty(PersistentWindowProcessor.GetDisplayKey()))
            {
                Thread.Sleep(5000);
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
            pwp.redrawDesktop = redraw_desktop;
            pwp.redirectAppDataFolder = redirect_appdata;
            pwp.enhancedOffScreenFix = enhanced_offscreen_fix;
            pwp.enableOffScreenFix = offscreen_fix;
            pwp.fixUnminimizedWindow = fix_unminimized_window;
            pwp.promptSessionRestore = prompt_session_restore;
            pwp.autoRestoreMissingWindows = auto_restore_missing_windows;
            pwp.restoreOneWindowPerProcess = restore_one_window_per_process;
            pwp.haltRestore = halt_restore;
            pwp.stickyDisplayConfig = sticky_display_config;

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
            var thread = new Thread(() =>
            {
                systrayForm.notifyIconMain.Icon = BusyIcon;

                if (silent)
                    return;

                //systrayForm.notifyIconMain.Visible = false;
                systrayForm.notifyIconMain.Visible = true;

                if (!notification)
                    return;

                systrayForm.notifyIconMain.ShowBalloonTip(5000);
            });

            thread.IsBackground = false;
            thread.Start();
        }

        static void HideRestoreTip()
        {
            systrayForm.notifyIconMain.Icon = IdleIcon;

            /*
            if (silent)
                return;
            */

            //systrayForm.notifyIconMain.Visible = false;
            systrayForm.notifyIconMain.Visible = true;
        }

        static void EnableRestoreMenu(bool enableRestoreDB)
        {
            systrayForm.UpdateMenuEnable(enableRestoreDB);
        }

        static public void CaptureSnapshot(int id, bool prompt = true)
        {
            pwp.TakeSnapshot(id);
            if (!silent)
            {
                char c = SnapshotIdToChar(id);
                if (prompt)
                    systrayForm.notifyIconMain.ShowBalloonTip(5000, $"snapshot '{c}' is captured", $"click icon then immediately press key '{c}' to restore the snapshot", ToolTipIcon.Info);
            }
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

        static public void CaptureToDisk()
        {
            GetProcessInfo();
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);
        }

        static public void RestoreFromDisk()
        {
            pwp.restoringFromDB = true;
            pwp.StartRestoreTimer(milliSecond : 2000 /*wait mouse settle still for taskbar restore*/);
        }

        static public void RestoreSnapshot(int id)
        {
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
