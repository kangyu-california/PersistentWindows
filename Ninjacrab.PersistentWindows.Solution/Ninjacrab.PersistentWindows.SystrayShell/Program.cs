using System;
using System.Threading;
using System.Windows.Forms;
using Ninjacrab.PersistentWindows.Common;

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

        //[STAThread]
        static void Main(string[] args)
        {
            bool no_splash = false;
            foreach (var arg in args)
            {
                switch(arg)
                {
                    case "-silent":
                        no_splash = true;
                        break;
                }
            }

#if (!DEBUG)
            Mutex singleInstMutex = new Mutex(true, Application.ProductName);
            if (!singleInstMutex.WaitOne(TimeSpan.Zero, true))
            {
                MessageBox.Show($"Only one inst of {Application.ProductName} can be run!");
                //Application.Exit();
                return;
            }
            else
            {
                singleInstMutex.ReleaseMutex();
            }
#endif

            pwp = new PersistentWindowProcessor();
            pwp.showRestoreTip = ShowRestoreTip;
            pwp.hideRestoreTip = HideRestoreTip;
            pwp.Start();

            if (!no_splash)
            {
                StartSplashForm();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            systrayForm = new SystrayForm();
            Application.Run();
        }

        static void ShowRestoreTip()
        {
            var thread = new Thread(() =>
            {
                systrayForm.notifyIconMain.Visible = true;
                systrayForm.notifyIconMain.ShowBalloonTip(30000, "", "Please wait while restoring windows", ToolTipIcon.Info);
            });

            thread.IsBackground = false;
            thread.Start();
        }

        static void HideRestoreTip()
        {
            systrayForm.notifyIconMain.Visible = false;
            systrayForm.notifyIconMain.Visible = true;
        }

        static void StartSplashForm()
        {
            var thread = new Thread(() => TimedSplashForm());
            thread.IsBackground = false;
            thread.Priority = ThreadPriority.Highest;
            thread.Name = "StartSplashForm";
            thread.Start();
        }

        static void TimedSplashForm()
        {
            var thread = new Thread(() => Application.Run(new SplashForm()));
            thread.IsBackground = false;
            thread.Name = "TimedSplashForm";
            thread.Start();
            Thread.Sleep(5000);
            thread.Abort();
        }

        static public void Capture()
        {
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveToDB : true);
        }

        static public void Restore()
        {
            pwp.BatchRestoreApplicationsOnCurrentDisplays(restoreFromDB : true);
        }

    }
}
