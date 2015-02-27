using System;
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
            new PersistentWindowProcessor().Start();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new SystrayForm();
            Application.Run();
        }
    }
}
