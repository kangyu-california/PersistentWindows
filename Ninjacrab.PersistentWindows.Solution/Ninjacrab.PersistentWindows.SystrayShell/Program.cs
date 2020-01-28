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
        static Mutex singleInstMutex = new Mutex(true, Application.ProductName);
        [STAThread]
        static void Main()
        {
            if (!singleInstMutex.WaitOne(TimeSpan.Zero, true))
            {
#if (!DEBUG)
                MessageBox.Show($"Only one inst of {Application.ProductName} can be run!");
                //Application.Exit();
                return;
#endif
            }
            else
            {
                singleInstMutex.ReleaseMutex();
            }

            StartSplashForm();

            PersistentWindowProcessor pwp = new PersistentWindowProcessor();
            pwp.Start();

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

    }
}
