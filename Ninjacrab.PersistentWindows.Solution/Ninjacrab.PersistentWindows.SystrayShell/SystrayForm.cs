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
        private bool pauseUpgradeCounter = false;
        private bool foundUpgrade = false;

        private bool shiftKeyPressed;
        private bool controlKeyPressed;
        private bool altKeyPressed;
        private bool singleClick;
        private int clickCount;
        private System.Threading.Timer clickDelayTimer;

        public SystrayForm()
        {
            uiRefreshTimer.Interval = 2000;
            uiRefreshTimer.Tick += new EventHandler(TimerEventProcessor);
            uiRefreshTimer.Enabled = true;

            clickDelayTimer = new System.Threading.Timer(state =>
            {
                pauseUpgradeCounter = true;
                if (clickCount > 3)
                    clickCount = 3;

                if (shiftKeyPressed)
                {
                    // take counted snapshot
                    Program.TakeSnapshot(clickCount);
                }
                else if (controlKeyPressed)
                {
                    //restore counted snapshot
                    Program.RestoreSnapshot(clickCount);
                }
                else if (altKeyPressed)
                {
                    //restore previous workspace (not necessarily a snapshot)
                    Program.RestoreSnapshot(4);
                }
                else
                {
                    if (clickCount == 1)
                        //restore unnamed(default) snapshot
                        Program.RestoreSnapshot(0);
                    else if (clickCount == 2)
                        Program.TakeSnapshot(0);
                }

                clickCount = 0;
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

                    if (pauseUpgradeCounter)
                        pauseUpgradeCounter = false;
                    else
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

            string[] latest = latestVersion.Split('.');
            int latest_major = Int32.Parse(latest[0]);
            int latest_minor = Int32.Parse(latest[1]);

            string[] current = Application.ProductVersion.Split('.');
            int current_major = Int32.Parse(current[0]);
            int current_minor = Int32.Parse(current[1]);

            if (current_major < latest_major
                || current_major == latest_major && current_minor < latest_minor)
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
                    Program.TakeSnapshot(0);
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
            Program.CaptureToDisk();
            restoreToolStripMenuItem.Enabled = true;
        }

        private void RestoreWindowClickHandler(object sender, EventArgs e)
        {
            Program.RestoreFromDisk();
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

        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                clickCount++;

                shiftKeyPressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
                controlKeyPressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
                altKeyPressed = (User32.GetKeyState(0x12) & 0x8000) != 0;

                clickDelayTimer.Change(500, System.Threading.Timeout.Infinite);
            }
        }
    }
}
