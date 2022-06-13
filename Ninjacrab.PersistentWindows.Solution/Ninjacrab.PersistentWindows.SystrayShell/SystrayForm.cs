using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;
using System.Timers;
using System.IO;
using System.IO.Compression;

using Ninjacrab.PersistentWindows.Common;
using Ninjacrab.PersistentWindows.Common.WinApiBridge;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        public bool restoreToolStripMenuItemEnabled;
        public bool restoreSnapshotMenuItemEnabled;

        private bool pauseAutoRestore = false;

        public bool enableUpgradeNotice = true;
        private int skipUpgradeCounter = 0;
        private bool pauseUpgradeCounter = false;
        private bool foundUpgrade = false;

        public bool autoUpgrade = false;

        private int shiftKeyPressed = 0;
        private int controlKeyPressed = 0;
        private int altKeyPressed = 0;
        private int clickCount = 0;
        private bool firstClick = false;
        private bool doubleClick = false;

        private DateTime clickTime;

        private System.Timers.Timer clickDelayTimer;

        private Dictionary<string, bool> upgradeDownloaded = new Dictionary<string, bool>();

        public SystrayForm()
        {
            InitializeComponent();

            clickDelayTimer = new System.Timers.Timer(1000);
            clickDelayTimer.Elapsed += ClickTimerCallBack;
            clickDelayTimer.SynchronizingObject = this.contextMenuStripSysTray;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = false;
        }

        public void StartTimer(int milliseconds)
        {
            clickDelayTimer.Interval = milliseconds;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = true;
        }

        private void ClickTimerCallBack(Object source, ElapsedEventArgs e)
        {
            if (clickCount == 0)
            {
                // fix context menu position
                contextMenuStripSysTray.Show(Cursor.Position);
                return;
            }

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
            else if (shiftKeyPressed == clickCount && shiftKeyPressed != 0)
            {
                // take counted snapshot
                Program.CaptureSnapshot(clickCount);
            }
            else if (controlKeyPressed == clickCount && controlKeyPressed != 0)
            {
                //restore counted snapshot
                Program.RestoreSnapshot(clickCount);
            }
            else if (altKeyPressed == clickCount && altKeyPressed != 0)
            {
                //restore previous workspace (not necessarily a snapshot)
                Program.RestoreSnapshot(36); //MaxSnapShot - 1
            }
            else if (totalSpecialKeyPressed == 0)
            {
                if (keyPressed < 0)
                {
                    if (clickCount == 1 && firstClick && !doubleClick)
                        //restore unnamed(default) snapshot
                        Program.RestoreSnapshot(0);
                    else if (clickCount == 2 && firstClick && doubleClick)
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
                    else if (clickCount == 1 && firstClick && !doubleClick)
                    {
                        Program.RestoreSnapshot(snapshot);
                    }
                    else if (clickCount == 2 && firstClick && doubleClick)
                    {
                        Program.CaptureSnapshot(snapshot);
                    }
                }
            }

            clickCount = 0;
            doubleClick = false;
            firstClick = false;
            shiftKeyPressed = 0;
            controlKeyPressed = 0;
            altKeyPressed = 0;

        }

        //private void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        public void UpdateMenuEnable(bool enableRestoreFromDB, bool checkUpgrade)
        {
            restoreToolStripMenuItemEnabled = enableRestoreFromDB;

            if (checkUpgrade && enableUpgradeNotice)
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

                    skipUpgradeCounter = (skipUpgradeCounter + 1) % 31;
                }
            }
        }
        
        public void EnableSnapshotRestore(bool enable)
        {
            restoreSnapshotMenuItemEnabled = enable;
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
                Program.LogError(ex.ToString());
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

                if (!upgradeDownloaded.ContainsKey(latestVersion))
                {
                    var src_file = $"{Program.ProjectUrl}/releases/download/{latestVersion}/{System.Windows.Forms.Application.ProductName}{latestVersion}.zip";
                    var dst_file = $"{Program.AppdataFolder}/upgrade.zip";
                    var dst_dir = Path.Combine($"{Program.AppdataFolder}", "upgrade");
                    var install_dir = Application.StartupPath;

                    try
                    {
                        cli.DownloadFile(src_file, dst_file);
                        if (Directory.Exists(dst_dir))
                            Directory.Delete(dst_dir, true);
                        ZipFile.ExtractToDirectory(dst_file, dst_dir);
                        upgradeDownloaded[latestVersion] = true;

                        string batFile = Path.Combine(Program.AppdataFolder, $"pw_upgrade.bat");
                        string content = "timeout /t 5 /nobreak > NUL";
                        content += $"\ncopy /Y \"{dst_dir}\\*.*\" \"{install_dir}\"";
                        content += "\nstart \"\" /B \"" + Path.Combine(install_dir, Application.ProductName) + ".exe\" " + Program.CmdArgs;
                        File.WriteAllText(batFile, content);

                        if (autoUpgrade)
                            Upgrade();
                        else
                            upgradeNoticeMenuItem.Text = $"Upgrade to {latestVersion}";
                    }
                    catch (Exception ex)
                    {
                        Program.LogError(ex.ToString());
                    }
                }
            }
        }

        private void Upgrade()
        {
            string batFile = Path.Combine(Program.AppdataFolder, "pw_upgrade.bat");
            Process.Start(batFile);

            this.notifyIconMain.Visible = false;
            this.notifyIconMain.Icon = null;
            Application.Exit();
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
            {
                // for debug issue #109 only
                //Program.ChangeZorderMethod();

                Program.RestoreSnapshot(id);
            }
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
            if (foundUpgrade)
            {
                Upgrade();
            }
            else if (enableUpgradeNotice)
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
            Process.Start(Program.ProjectUrl + "/blob/master/Help.md");
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            this.notifyIconMain.Visible = false;
            this.notifyIconMain.Icon = null;
            Application.Exit();
        }

        private void IconMouseClick(object sender, MouseEventArgs e)
        {
            if (!doubleClick && e.Button == MouseButtons.Left)
            {
                firstClick = true;
                clickTime = DateTime.Now;
                Console.WriteLine("MouseClick");

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

        private void IconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                DateTime now = DateTime.Now;
                var ms = now.Subtract(clickTime).TotalMilliseconds;
                Console.WriteLine("{0}", ms);
                if (ms < 30 || ms > 200)
                {
                    Program.LogError($"ignore bogus double click {ms} ms");
                    return;
                }

                doubleClick = true;
                Console.WriteLine("MouseDoubleClick");
            }
        }

        private void IconMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Console.WriteLine("Down");

                if ((User32.GetKeyState(0x10) & 0x8000) != 0)
                    shiftKeyPressed++;

                if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                    controlKeyPressed++;

                if ((User32.GetKeyState(0x12) & 0x8000) != 0)
                    altKeyPressed++;
            }
            else
            {
                restoreSnapshotMenuItem.Enabled = restoreSnapshotMenuItemEnabled;
                restoreToolStripMenuItem.Enabled = restoreToolStripMenuItemEnabled;
            }
        }

        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Console.WriteLine("Up");

                clickCount++;
                StartTimer(400);
            }
            else if (e.Button == MouseButtons.Right)
            {
                StartTimer(5);
            }
        }
    }
}
