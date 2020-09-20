using System;
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
            bool delay_start = false;
            bool redraw_desktop = false;

            foreach (var arg in args)
            {
                if (delay_start)
                {
                    delay_start = false;
                    int seconds = Int32.Parse(arg);
                    Thread.Sleep(1000 * seconds);
                    continue;
                }

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
                    case "-delay_start":
                        delay_start = true;
                        break;
                    case "-redraw_desktop":
                        redraw_desktop = true;
                        break;
                }
            }

            while (String.IsNullOrEmpty(PersistentWindowProcessor.GetDisplayKey()))
            {
                Thread.Sleep(5000);
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
            pwp.redrawDesktop = redraw_desktop;

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
            systrayForm.enableRestoreFromDB = enable;
            systrayForm.UiRefreshTimer.Start();
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
            GetProcessInfo();
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);
        }

        static public void Restore()
        {
            pwp.restoringFromDB = true;
            pwp.StartRestoreTimer(milliSecond : 2000 /*wait mouse settle still for taskbar restore*/);
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
            //Console.WriteLine(outLine.Data);
            string line = outLine.Data;
            if (string.IsNullOrEmpty(line))
                return;
            //Log.Info("{0}", line);
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
    }
}
