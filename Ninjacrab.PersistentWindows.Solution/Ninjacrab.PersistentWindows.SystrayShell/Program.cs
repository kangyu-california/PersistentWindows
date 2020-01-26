using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Ninjacrab.PersistentWindows.Common;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
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
