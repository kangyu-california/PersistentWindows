using System;
using System.Threading;
using System.Windows.Forms;

using Ninjacrab.PersistentWindows.Common;
using Ninjacrab.PersistentWindows.Common.Diagnostics;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static readonly string ProjectUrl = "https://github.com/kangyu-california/PersistentWindows";

        static PersistentWindowProcessor pwp = null;    
        static SystrayForm systrayForm = null;
        static bool silent = false;
        static bool notification_on = false;

        [STAThread]
        static void Main(string[] args)
        {
            bool no_splash = false;
            bool dry_run = false;
            bool fix_zorder = false;
            foreach (var arg in args)
            {
                switch(arg)
                {
                    case "-silent":
                        silent = true;
                        no_splash = true;
                        break;
                    case "-splash_off":
                        no_splash = true;
                        break;
                    case "-notification_on":
                        notification_on = true;
                        break;
                    case "-dry_run":
                        Log.Trace("dry_run mode");
                        dry_run = true;
                        break;
                    case "-fix_zorder":
                        fix_zorder = true;
                        break;
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            systrayForm = new SystrayForm();

            pwp = new PersistentWindowProcessor();
            pwp.dryRun = dry_run;
            pwp.fixZorder = fix_zorder;
            pwp.showRestoreTip = ShowRestoreTip;
            pwp.hideRestoreTip = HideRestoreTip;
            pwp.enableRestoreMenu = EnableRestoreMenu;

            if (!pwp.Start())
            {
                return;
            }

            if (!no_splash)
            {
                StartSplashForm();
            }

            Application.Run();
        }

        static void ShowRestoreTip()
        {
            var thread = new Thread(() =>
            {
                if (silent)
                    return;

                systrayForm.notifyIconMain.Visible = false;
                systrayForm.notifyIconMain.Visible = true;

                if (!notification_on)
                    return;

                systrayForm.notifyIconMain.ShowBalloonTip(10000);
            });

            thread.IsBackground = false;
            thread.Start();
        }

        static void HideRestoreTip()
        {
            if (silent || !notification_on)
                return;
            systrayForm.notifyIconMain.Visible = false;
            systrayForm.notifyIconMain.Visible = true;
        }

        static void EnableRestoreMenu(bool enable)
        {
            systrayForm.restoreToolStripMenuItem.Enabled = enable;
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

        static public void Capture()
        {
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);
        }

        static public void Restore()
        {
            pwp.restoringFromDB = true;
            pwp.StartRestoreTimer(milliSecond : 2000 /*wait mouse settle still for taskbar restore*/);
        }

    }
}
