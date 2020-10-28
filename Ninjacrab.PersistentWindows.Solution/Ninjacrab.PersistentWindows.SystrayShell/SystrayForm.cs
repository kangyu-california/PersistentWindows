using System;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;

using Ninjacrab.PersistentWindows.Common;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        private Timer uiRefreshTimer = new Timer();

        public volatile bool enableRefresh = false;
        public bool enableRestoreFromDB = false;

        private bool pauseAutoRestore = false;

        public bool enableUpgradeNotice = true;
        private int skipUpgradeCounter = 0;
        private bool foundUpgrade = false;

        private bool controlKeyPressed;
        private bool altKeyPressed;
        private bool singleClick;
        private System.Threading.Timer clickDelayTimer;

        public SystrayForm()
        {
            uiRefreshTimer.Interval = 2000;
            uiRefreshTimer.Tick += new EventHandler(TimerEventProcessor);
            uiRefreshTimer.Enabled = true;

            clickDelayTimer = new System.Threading.Timer(state =>
            {
                if (singleClick)
                {
                    if (controlKeyPressed)
                    {
                        //restore named snapshot
                        ;
                    }
                    else if (altKeyPressed)
                    {
                        //restore previous workspace (not necessarily a snapshot)
                        Program.RestoreSnapshot(PersistentWindowProcessor.PreviousSnapshot);
                    }
                    else
                    {
                        //restore unnamed(default) snapshot
                        Program.RestoreSnapshot(PersistentWindowProcessor.DefaultSnapshot);
                    }
                }
            });

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

                if (enableUpgradeNotice)
                {
                    if (skipUpgradeCounter == 0)
                    {
                        CheckUpgrade();
                    }
                    skipUpgradeCounter = (skipUpgradeCounter + 1) % 7;
                }
            }
        }

        private void CheckUpgrade()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var cli = new WebClient();
            string data = cli.DownloadString($"{Program.ProjectUrl}/releases/latest");
            string pattern = "releases/tag/";
            int index = data.IndexOf(pattern);
            string latestVersion = data.Substring(index + pattern.Length, data.Substring(index + pattern.Length, 6).LastIndexOf('"'));

            if (!Application.ProductVersion.StartsWith(latestVersion))
            {
                notifyIconMain.ShowBalloonTip(5000, $"{Application.ProductName} {latestVersion} upgrade is available", "The upgrade notice can be disabled in menu", ToolTipIcon.Info);
                foundUpgrade = true;
                aboutToolStripMenuItem.Text = "Goto upgrade";
            }
        }

        private void SnapshotAction(bool doubleClick)
        {
            controlKeyPressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
            altKeyPressed = (User32.GetKeyState(0x12) & 0x8000) != 0;

            if (doubleClick)
            {
                // cancel previous single click event
                singleClick = false;

                if (controlKeyPressed)
                {
                    //TODO: create named snapshot
                    ;
                }
                else if (altKeyPressed)
                {
                    ;
                }
                else
                {
                    //take unnamed(default) snapshot
                    Program.TakeSnapshot(PersistentWindowProcessor.DefaultSnapshot);
                }

                return;
            }

            singleClick = true;
            clickDelayTimer.Change(500, System.Threading.Timeout.Infinite);
        }

        private void ManageLayoutProfileClickHandler(object sender, EventArgs e)
        {
            Program.ManageLayoutProfile();
        }

        private void CaptureWindowClickHandler(object sender, EventArgs e)
        {
            Program.Capture();
            restoreToolStripMenuItem.Enabled = true;
        }

        private void RestoreWindowClickHandler(object sender, EventArgs e)
        {
            Program.RestoreDisk();
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

        private void PauseResumeUpgradeNotice(Object sender, EventArgs e)
        {
            if (enableUpgradeNotice)
            {
                enableUpgradeNotice = false;
                upgradeNoticeMenuItem.Text = "Enable upgrade notice";
            }
            else
            {
                enableUpgradeNotice = true;
                upgradeNoticeMenuItem.Text = "Disable upgrade notice";
            }
        }

        private void AboutToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            if (foundUpgrade)
                Process.Start($"{Program.ProjectUrl}/releases/latest");
            else
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
                //this.notifyIconMain.Icon = new System.Drawing.Icon(System.Drawing.SystemIcons.Exclamation, 40, 40);
                SnapshotAction(doubleClick: false);
            }
        }

        private void IconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                SnapshotAction(doubleClick: true);
            }
        }
    }
}
