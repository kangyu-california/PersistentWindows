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

        private int shiftKeyPressed = 0;
        private int controlKeyPressed = 0;
        private int altKeyPressed = 0;
        private int clickCount = 0;
        private System.Threading.Timer clickDelayTimer;

        public SystrayForm()
        {
            uiRefreshTimer.Interval = 2000;
            uiRefreshTimer.Tick += new EventHandler(TimerEventProcessor);
            uiRefreshTimer.Enabled = true;

            clickDelayTimer = new System.Threading.Timer(state =>
            {
                pauseUpgradeCounter = true;

                int keyPressed = -1;
                //check 0-9 key pressed
                for (int i = 0x30; i < 0x3a; ++i)
                {
                    if (User32.GetAsyncKeyState(i) != 0)
                    {
                        keyPressed = i;
                        break;
                    }
                }

                //check a-z pressed
                if (keyPressed < 0)
                for (int i = 0x41; i < 0x5b; ++i)
                {
                    if (User32.GetAsyncKeyState(i) != 0)
                    {
                        keyPressed = i;
                        break;
                    }
                }

                int totalSpecialKeyPressed = shiftKeyPressed + controlKeyPressed + altKeyPressed;

                if (clickCount > 3)
                {
                }
                else if (totalSpecialKeyPressed > clickCount)
                {
                    //no more than one key can be pressed
                }
                else if (shiftKeyPressed == clickCount)
                {
                    // take counted snapshot
                    Program.CaptureSnapshot(clickCount);
                }
                else if (controlKeyPressed == clickCount)
                {
                    //restore counted snapshot
                    Program.RestoreSnapshot(clickCount);
                }
                else if (altKeyPressed == clickCount)
                {
                    //restore previous workspace (not necessarily a snapshot)
                    Program.RestoreSnapshot(36); //MaxSnapShot - 1
                }
                else if (totalSpecialKeyPressed == 0)
                {
                    if (keyPressed < 0)
                    {
                        if (clickCount == 1)
                            //restore unnamed(default) snapshot
                            Program.RestoreSnapshot(0);
                        else
                            Program.CaptureSnapshot(0);
                    }
                    else
                    {
                        int snapshot;
                        if (keyPressed < 0x3a)
                            snapshot = keyPressed - 0x30;
                        else
                            snapshot = keyPressed - 0x41 + 10; 

                        if (snapshot < 0 || snapshot > 35)
                        {
                            //invalid key pressed
                        }
                        else if (clickCount == 1)
                        {
                            Program.RestoreSnapshot(snapshot);
                        }
                        else
                        {
                            Program.CaptureSnapshot(snapshot);
                        }
                    }
                }

                clickCount = 0;
                shiftKeyPressed = 0;
                controlKeyPressed = 0;
                altKeyPressed = 0;
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
                    if (pauseUpgradeCounter)
                    {
                        pauseUpgradeCounter = false;
                    }
                    else
                    {
                        if (skipUpgradeCounter == 0)
                        {
                            CheckUpgrade();
                        }

                        skipUpgradeCounter = (skipUpgradeCounter + 1) % 7;
                    }
                }
            }
        }

        private void CheckUpgrade()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var cli = new WebClient();
            string data;
            try
            {
                data = cli.DownloadString($"{Program.ProjectUrl}/releases/latest");
            }
            catch (Exception ex)
            {
                Program.LogEvent(ex.ToString());
                return;
            }

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

        private void CaptureWindowClickHandler(object sender, EventArgs e)
        {
            Program.CaptureToDisk();
            restoreToolStripMenuItem.Enabled = true;
        }

        private void RestoreWindowClickHandler(object sender, EventArgs e)
        {
            Program.RestoreFromDisk();
        }

        private void CaptureSnapshotClickHandler(object sender, EventArgs e)
        {
            char snapshot_char = Program.EnterSnapshotName();
            int id = Program.SnapshotCharToId(snapshot_char);
            if (id != -1)
                Program.CaptureSnapshot(id, prompt : false);
        }

        private void RestoreSnapshotClickHandler(object sender, EventArgs e)
        {
            char snapshot_char = Program.EnterSnapshotName();
            int id = Program.SnapshotCharToId(snapshot_char);
            if (id != -1)
                Program.RestoreSnapshot(id);
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
                CheckUpgrade();
            }
        }

        private void AboutToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            if (foundUpgrade)
                Process.Start($"{Program.ProjectUrl}/releases/latest");
            else
                Process.Start(Program.ProjectUrl + "/blob/master/Help.md");
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            this.notifyIconMain.Visible = false;
            this.notifyIconMain.Icon = null;
            Application.Exit();
        }

        private void IconMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if ((User32.GetKeyState(0x10) & 0x8000) != 0)
                    shiftKeyPressed++;

                if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                    controlKeyPressed++;

                if ((User32.GetKeyState(0x12) & 0x8000) != 0)
                    altKeyPressed++;
            }
        }

        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                clickCount++;
                clickDelayTimer.Change(400, System.Threading.Timeout.Infinite);

                // clear memory of keyboard input
                for (int i = 0x30; i < 0x3a; ++i)
                {
                    User32.GetAsyncKeyState(i);
                }

                for (int i = 0x41; i < 0x5b; ++i)
                {
                    User32.GetAsyncKeyState(i);
                }
            }
        }
    }
}
