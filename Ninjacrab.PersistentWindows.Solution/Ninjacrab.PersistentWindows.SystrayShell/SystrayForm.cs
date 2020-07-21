using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        private bool pauseAutoRestore = false;
        public bool enableRestoreFromDB = false;
        public Timer UiRefreshTimer = new Timer();

        public SystrayForm()
        {
            UiRefreshTimer.Interval = 500;
            UiRefreshTimer.Tick += new EventHandler(TimerEventProcessor);

            InitializeComponent();
        }

        private void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        {
            restoreToolStripMenuItem.Enabled = enableRestoreFromDB;
            UiRefreshTimer.Stop();
        }

        private void CaptureWindowClickHandler(object sender, EventArgs e)
        {
            Program.Capture();
            this.restoreToolStripMenuItem.Enabled = true;
        }

        private void RestoreWindowClickHandler(object sender, EventArgs e)
        {
            Program.Restore();
        }

        private void PauseResumeAutoRestore(object sender, EventArgs e)
        {
            if (!pauseAutoRestore)
            {
                pauseAutoRestore = true;
                Program.PauseAutoRestore();
                pauseResumeToolStripMenuItem.Text = "Resume auto restore";
            }
            else
            {
                Program.ResumeAutoRestore();
                pauseAutoRestore = false;
                pauseResumeToolStripMenuItem.Text = "Pause auto restore";
            }
        }

        private void AboutToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            Process.Start(Program.ProjectUrl);
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            this.notifyIconMain.Visible = false;
            this.notifyIconMain.Icon = null;
            Application.Exit();
        }

    }
}
