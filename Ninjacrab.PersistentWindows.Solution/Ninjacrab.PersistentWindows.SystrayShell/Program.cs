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
        static PersistentWindowProcessor pwp;
        [STAThread]
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
            pwp.Start();

            if (!no_splash)
            {
                StartSplashForm();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new SystrayForm();
            Application.Run();
        }

        static void StartSplashForm()
        {
            var thread = new Thread(() => TimedSplashForm());
            thread.IsBackground = false;
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
            pwp.BatchCaptureApplicationsOnCurrentDisplays(saveDB : true);
        }

        static public void Restore()
        {
            Thread.Sleep(2000); // let mouse settle still for taskbar restoration
            pwp.BatchRestoreApplicationsOnCurrentDisplays(restoreDB : true);
        }

        static public void Stop()
        {
            pwp.Stop();
        }

    }
}
