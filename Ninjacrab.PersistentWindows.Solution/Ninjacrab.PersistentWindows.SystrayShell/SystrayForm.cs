using System;
using System.Diagnostics;
using System.Windows.Forms;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        public volatile bool enableRefresh = false;
        public bool enableRestoreFromDB = false;

        private bool pauseAutoRestore = false;
        private Timer uiRefreshTimer = new Timer();

        public SystrayForm()
        {
            uiRefreshTimer.Interval = 2000;
            uiRefreshTimer.Tick += new EventHandler(TimerEventProcessor);
            uiRefreshTimer.Enabled = true;

            InitializeComponent();
        }

        private void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        {
            if (enableRefresh)
            {
#if DEBUG
                Program.LogEvent("ui refresh timer triggered");
#endif
                restoreToolStripMenuItem.Enabled = enableRestoreFromDB;
                enableRefresh = false;
            }
        }

        private void ManageLayoutProfileClickHandler(object sender, EventArgs e)
        {
            Program.ManageLayoutProfile();
        }

        private void CaptureWindowClickHandler(object sender, EventArgs e)
        {
            Program.Capture();
            enableRestoreFromDB = true;
        }

        private void RestoreWindowClickHandler(object sender, EventArgs e)
        {
            Program.Restore();
        }

        private void PauseResumeAutoRestore(object sender, EventArgs e)
        {
            if (pauseAutoRestore)
            {
                Program.ResumeAutoRestore();
                pauseAutoRestore = false;
                pauseResumeToolStripMenuItem.Text = "Pause auto restore";
            }
            else
            {
                pauseAutoRestore = true;
                Program.PauseAutoRestore();
                pauseResumeToolStripMenuItem.Text = "Resume auto restore";
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

        private void IconMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
            }
        }

    }
}
