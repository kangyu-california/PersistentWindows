using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;
using System.Net;
using System.Timers;
using System.IO;
using System.IO.Compression;
using System.Drawing;

using PersistentWindows.Common.Diagnostics;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        private const int MaxSnapshots = 38; // 0-9, a-z, ` and final one for undo

        public bool restoreToolStripMenuItemEnabled;
        public bool restoreSnapshotMenuItemEnabled;

        private bool pauseAutoRestore = false;
        private bool toggleIcon = false;

        public bool enableUpgradeNotice = true;
        private int skipUpgradeCounter = 0;
        private bool pauseUpgradeCounter = false;
        private bool foundUpgrade = false;

        public bool autoUpgrade = false;

        private int ctrlKeyPressed = 0;
        private int shiftKeyPressed = 0;
        private int altKeyPressed = 0;
        private int clickCount = 0;
        private bool firstClick = false;
        private bool doubleClick = false;

        private DateTime clickTime;

        private System.Timers.Timer clickDelayTimer;

        private Dictionary<string, bool> upgradeDownloaded = new Dictionary<string, bool>();

        public SystrayForm(bool enable_upgrade_notice)
        {
            InitializeComponent();

            enableUpgradeNotice = enable_upgrade_notice;
            if (enableUpgradeNotice)
                upgradeNoticeMenuItem.Text = "Disable upgrade notice";
            else
                upgradeNoticeMenuItem.Text = "Enable upgrade notice";

            if (File.Exists(Program.DisableWebpageCommander))
            {
                invokeWebCommander.Text = "Enable webpage commander";
            }

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
                //contextMenuStripSysTray.Show(Cursor.Position);
                return;
            }

            pauseUpgradeCounter = true;

            Keys keyPressed = Keys.None;
            //check 0-9 key pressed
            for (Keys i = Keys.D0; i <= Keys.D9; ++i)
            {
                if (User32.GetAsyncKeyState((int)i) != 0)
                {
                    keyPressed = i;
                    break;
                }
            }

            //check a-z pressed
            if (keyPressed == Keys.None)
            for (Keys i = Keys.A; i <= Keys.Z; ++i)
            {
                if (User32.GetAsyncKeyState((int)i) != 0)
                {
                    keyPressed = i;
                    break;
                }
            }

            if (keyPressed == Keys.None)
            {
                if (User32.GetAsyncKeyState((int)Keys.Oem3) != 0)
                {
                    keyPressed = Keys.Oem3;
                }
            }

            int totalSpecialKeyPressed = shiftKeyPressed + altKeyPressed;

            if (clickCount > 2)
            {
            }
            else if (totalSpecialKeyPressed > clickCount)
            {
                //no more than one key can be pressed
            }
            else if (altKeyPressed == clickCount && altKeyPressed != 0 && ctrlKeyPressed == 0)
            {
                //restore previous workspace (not necessarily a snapshot)
                Program.RestoreSnapshot(MaxSnapshots - 1);
            }
            else
            {
                if (keyPressed == Keys.None)
                {
                    if (clickCount == 1 && firstClick && !doubleClick)
                    {
                        if (ctrlKeyPressed > 0 && altKeyPressed > 0 && shiftKeyPressed == 0)
                            Program.FgWindowToBottom();
                        else if (ctrlKeyPressed > 0 && altKeyPressed == 0 && shiftKeyPressed == 0)
                            Program.RecallLastKilledPosition();
                        else if (ctrlKeyPressed == 0 && altKeyPressed == 0 && shiftKeyPressed > 0)
                            Program.CenterWindow();
                        else if (ctrlKeyPressed == 0 && altKeyPressed == 0 && shiftKeyPressed == 0)
                            //restore unnamed(default) snapshot
                            Program.RestoreSnapshot(0);
                    }
                    else if (clickCount == 2 && firstClick && doubleClick)
                        Program.CaptureSnapshot(0, delayCapture: shiftKeyPressed > 0);
                }
                else
                {
                    int snapshot = -1;
                    if (keyPressed == Keys.Oem3)
                        snapshot = MaxSnapshots - 2;
                    else if (keyPressed >= Keys.D0 && keyPressed <= Keys.D9)
                        snapshot = keyPressed - Keys.D0;
                    else if (keyPressed >= Keys.A && keyPressed <= Keys.Z)
                        snapshot = keyPressed - Keys.A + 10; 

                    if (snapshot < 0)
                    {
                        //invalid key pressed
                    }
                    else if (clickCount == 1 && firstClick && !doubleClick)
                    {
                        Program.RestoreSnapshot(snapshot);
                    }
                    else if (clickCount == 2 && firstClick && doubleClick)
                    {
                        Program.CaptureSnapshot(snapshot, delayCapture: shiftKeyPressed > 0);
                    }
                }
            }

            clickCount = 0;
            doubleClick = false;
            firstClick = false;
            ctrlKeyPressed = 0;
            shiftKeyPressed = 0;
            altKeyPressed = 0;
        }

        //private void TimerEventProcessor(Object myObject, EventArgs myEventArgs)
        public void UpdateMenuEnable(bool enableRestoreFromDB, bool checkUpgrade)
        {
            if (enableRestoreFromDB)
                restoreToolStripMenuItem.Image = null;
            else
                restoreToolStripMenuItem.Image = Properties.Resources.question;

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
                        CheckUpgradeSafe();
                    }

                    skipUpgradeCounter = (skipUpgradeCounter + 1) % 31;
                }
            }
        }
        
        public void EnableSnapshotRestore(bool enable)
        {
            restoreSnapshotMenuItem.Enabled = enable;
        }

        private void CheckUpgradeSafe()
        {
            try
            {
                CheckUpgrade();
            }
            catch (Exception ex)
            {
                Program.LogError(ex.ToString());
            }
        }

        private void CheckUpgrade()
        {
            ServicePointManager.Expect100Continue = true;
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var cli = new WebClient();
            string data = cli.DownloadString($"{Program.ProjectUrl}/releases");

            string latest_pattern = "releases/latest";
            int index = data.IndexOf(latest_pattern);
            index -= 256;
            data = data.Substring(index, 256);
            string pattern = "releases/tag/";
            index = data.IndexOf(pattern);
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

                    {
                        cli.DownloadFile(src_file, dst_file);
                        if (Directory.Exists(dst_dir))
                            Directory.Delete(dst_dir, true);
                        ZipFile.ExtractToDirectory(dst_file, dst_dir);
                        upgradeDownloaded[latestVersion] = true;

                        string batFile = Path.Combine(Program.AppdataFolder, $"pw_upgrade.bat");
                        string content = Program.WaitPwFinish;
                        content += $"\ncopy /Y \"{dst_dir}\\*.*\" \"{install_dir}\"";
                        content += "\nstart \"\" /B \"" + Path.Combine(install_dir, Application.ProductName) + ".exe\" " + Program.CmdArgs;
                        File.WriteAllText(batFile, content);

                        if (autoUpgrade)
                            Upgrade();
                        else
                        {
                            upgradeNoticeMenuItem.Text = $"Upgrade to {latestVersion}";
                            notifyIconMain.Icon = Program.UpdateIcon;
                        }
                    }
                }
            }
        }

        private void Exit()
        {
            var process = Process.GetCurrentProcess();
            process.PriorityClass = ProcessPriorityClass.High;

            Program.WriteDataDump();
            Log.Event("Session exit");

            this.notifyIconMain.Visible = false;
            //this.notifyIconMain.Icon = null;

            Log.Exit();
            Program.Stop();
            Application.Exit();
        }

        private void Upgrade()
        {
            Program.WriteDataDump();

            string batFile = Path.Combine(Program.AppdataFolder, "pw_upgrade.bat");
            Process.Start(batFile);
            Exit();
        }

        private void CaptureWindowToDisk(object sender, EventArgs e)
        {
            Program.CaptureToDisk();
            restoreToolStripMenuItem.Image = null;
        }

        private void RestoreWindowFromDisk(object sender, EventArgs e)
        {
            Program.RestoreFromDisk(restoreToolStripMenuItem.Image != null);
        }

        private void CaptureSnapshot(object sender, EventArgs e)
        {
            bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
            char snapshot_char = Program.EnterSnapshotName();
            int id = Program.SnapshotCharToId(snapshot_char);
            if (id != -1)
                Program.CaptureSnapshot(id, prompt : false, delayCapture: shift_key_pressed);
        }

        private void RestoreSnapshot(object sender, EventArgs e)
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

        private void WebCommander(object sender, EventArgs e)
        {
            if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                HotKeyForm.InvokeFromMenu();
            else if (this.invokeWebCommander.Text.Contains("Disable"))
            {
                File.Create(Program.DisableWebpageCommander);
                this.invokeWebCommander.Text = "Enable webpage commander";
                HotKeyForm.Stop();
            }
            else
            {
                try
                {
                    File.Delete(Program.DisableWebpageCommander);
                }
                catch (Exception ex)
                {
                    Log.Error(ex.ToString());
                }

                this.invokeWebCommander.Text = "Disable webpage commander";
                HotKeyForm.Start(Program.hotkey);
            }
        }

        private void ToggleIcon(object sender, EventArgs e)
        {
            if (toggleIcon)
            {
                notifyIconMain.Icon = Program.IdleIcon;
                toggleIcon = !toggleIcon;
                toggleIconMenuItem.Text = "Try customized icon";
            }
            else
            {
                using (OpenFileDialog openFileDialog = new OpenFileDialog())
                {
                    //openFileDialog.InitialDirectory = "c:\\";
                    openFileDialog.Filter = "*.ico, *.png|*.ico;*.png| *.ico | *.ico | *.png | *.png";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        //Get the path of specified file
                        string filePath = openFileDialog.FileName;
                        if (String.IsNullOrEmpty(filePath))
                            return;
                        if (filePath.EndsWith(".png"))
                        {
                            var bitmap = new Bitmap(filePath); // or get it from resource
                            notifyIconMain.Icon = Icon.FromHandle(bitmap.GetHicon());
                        }
                        else
                            notifyIconMain.Icon = new Icon(filePath);
                        toggleIcon = !toggleIcon;
                        toggleIconMenuItem.Text = "Disable customized icon";
                    }
                }
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
                CheckUpgradeSafe();
            }
        }

        private void AboutToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            Process.Start(Program.ProjectUrl + "/blob/master/Help.md");
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            bool ctrl_key_pressed = (User32.GetKeyState(0x11) & 0x8000) != 0;
            if (ctrl_key_pressed)
                Program.Restart(2, hidden:false);
            Exit();
        }

        private void IconMouseClick(object sender, MouseEventArgs e)
        {
            if (!doubleClick && e.Button == MouseButtons.Left)
            {
                firstClick = true;
                clickTime = DateTime.Now;
                Console.WriteLine("MouseClick");

                // clear memory of keyboard input
                for (Keys i = Keys.D0; i <= Keys.D9; ++i)
                {
                    User32.GetAsyncKeyState((int)i);
                }

                for (Keys i = Keys.A; i <= Keys.Z; ++i)
                {
                    User32.GetAsyncKeyState((int)i);
                }

                User32.GetAsyncKeyState((int)Keys.Oem3);
            }
        }

        private void IconMouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                DateTime now = DateTime.Now;
                var ms = now.Subtract(clickTime).TotalMilliseconds;
                Console.WriteLine("{0}", ms);
                if (ms < 30 || ms > SystemInformation.DoubleClickTime / 2)
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

                if ((User32.GetKeyState(0x11) & 0x8000) != 0)
                    ctrlKeyPressed++;

                if ((User32.GetKeyState(0x10) & 0x8000) != 0)
                    shiftKeyPressed++;

                if ((User32.GetKeyState(0x12) & 0x8000) != 0)
                    altKeyPressed++;
            }
        }

        private void IconMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                Console.WriteLine("Up");

                clickCount++;
                StartTimer(SystemInformation.DoubleClickTime);
            }
            else if (e.Button == MouseButtons.Middle)
            {
                notifyIconMain.Icon = Program.IdleIcon;
            }
        }
    }
}
