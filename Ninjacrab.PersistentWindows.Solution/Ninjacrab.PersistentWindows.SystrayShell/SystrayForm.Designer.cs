using System.Windows.Forms;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    static class Globals
    {
        //use Application.ProductVersion instead
        //public const string Version = "";
    }

    partial class SystrayForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;
        public System.Windows.Forms.NotifyIcon notifyIconMain;
        private System.Windows.Forms.ContextMenuStrip contextMenuStripSysTray;

        private ToolStripMenuItem captureToolStripMenuItem;
        private ToolStripMenuItem restoreToolStripMenuItem;
        private ToolStripMenuItem captureSnapshotMenuItem;
        private ToolStripMenuItem restoreSnapshotMenuItem;
        private ToolStripMenuItem pauseResumeToolStripMenuItem;
        public  ToolStripMenuItem upgradeNoticeMenuItem;
        private ToolStripMenuItem aboutToolStripMenuItem;
        private ToolStripMenuItem exitToolStripMenuItem;
        private ToolStripSeparator[] menuSeparators = new System.Windows.Forms.ToolStripSeparator[5];

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(SystrayForm));
            this.notifyIconMain = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStripSysTray = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureSnapshotMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreSnapshotMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.pauseResumeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.upgradeNoticeMenuItem = new System.Windows.Forms.ToolStripMenuItem();

            for (int i = 0; i < menuSeparators.Length; ++i)
            {
                this.menuSeparators[i] = new System.Windows.Forms.ToolStripSeparator();
                this.menuSeparators[i].Name = $"toolStripMenuItem{i}";
                this.menuSeparators[i].Size = new System.Drawing.Size(132, 6);
            }
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripSysTray.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIconMain
            // 
            this.notifyIconMain.ContextMenuStrip = this.contextMenuStripSysTray;
            //this.notifyIconMain.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIconMain.Icon")));
            this.notifyIconMain.Icon = Program.IdleIcon;
            this.notifyIconMain.Text = $"{Application.ProductName} {Application.ProductVersion}";
            this.notifyIconMain.BalloonTipTitle = "";
            this.notifyIconMain.BalloonTipText = "Please wait while restoring windows";
            this.notifyIconMain.BalloonTipIcon = ToolTipIcon.Info;
            this.notifyIconMain.Visible = true;
            this.notifyIconMain.MouseDown += new System.Windows.Forms.MouseEventHandler(this.IconMouseDown);
            this.notifyIconMain.MouseUp += new System.Windows.Forms.MouseEventHandler(this.IconMouseUp);

            this.notifyIconMain.MouseClick += new System.Windows.Forms.MouseEventHandler(this.IconMouseClick);

            this.notifyIconMain.MouseDoubleClick += new System.Windows.Forms.MouseEventHandler(this.IconMouseDoubleClick);

            // 
            // contextMenuStripSysTray
            // 
            this.contextMenuStripSysTray.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                /*
                this.manageLayoutProfile,
                this.toolStripMenuItem[3],
                */
                this.captureToolStripMenuItem,
                this.restoreToolStripMenuItem,
                this.menuSeparators[0],
                this.captureSnapshotMenuItem,
                this.restoreSnapshotMenuItem,
                this.menuSeparators[1],
                this.pauseResumeToolStripMenuItem,
                this.menuSeparators[2],
                this.upgradeNoticeMenuItem,
                this.aboutToolStripMenuItem,
                this.menuSeparators[3],
                this.exitToolStripMenuItem});
            this.contextMenuStripSysTray.Name = "contextMenuStripSysTray";
            this.contextMenuStripSysTray.Size = new System.Drawing.Size(136, 108);

            // capture
            // 
            this.captureToolStripMenuItem.Name = "capture";
            this.captureToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.captureToolStripMenuItem.Text = "&Capture windows to disk";
            this.captureToolStripMenuItem.Click += new System.EventHandler(this.CaptureWindowClickHandler);

            // restore
            // 
            this.restoreToolStripMenuItem.Name = "restore";
            this.restoreToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.restoreToolStripMenuItem.Text = "&Restore windows from disk";
            this.restoreToolStripMenuItem.Click += new System.EventHandler(this.RestoreWindowClickHandler);

            // capture snapshot
            // 
            this.captureSnapshotMenuItem.Name = "capture snapshot";
            this.captureSnapshotMenuItem.Size = new System.Drawing.Size(135, 22);
            this.captureSnapshotMenuItem.Text = "C&apture snapshot";
            this.captureSnapshotMenuItem.Click += new System.EventHandler(this.CaptureSnapshotClickHandler);

            // restore
            // 
            this.restoreSnapshotMenuItem.Name = "restore snapshot";
            this.restoreSnapshotMenuItem.Size = new System.Drawing.Size(135, 22);
            this.restoreSnapshotMenuItem.Text = "R&estore snapshot";
            this.restoreSnapshotMenuItem.Click += new System.EventHandler(this.RestoreSnapshotClickHandler);
            this.restoreSnapshotMenuItem.Enabled = false;

            // suspend/resume auto restore
            // 
            this.pauseResumeToolStripMenuItem.Name = "suspend/resume";
            this.pauseResumeToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.pauseResumeToolStripMenuItem.Text = "&Pause auto restore";
            this.pauseResumeToolStripMenuItem.Click += new System.EventHandler(this.PauseResumeAutoRestore);

            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.aboutToolStripMenuItem.Text = "&Help";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItemClickHandler);

            // pause/resume upgrade notice
            this.upgradeNoticeMenuItem.Size = new System.Drawing.Size(135, 22);
            //this.upgradeNoticeMenuItem.Text = "Disable upgrade notice";
            this.upgradeNoticeMenuItem.Click += new System.EventHandler(this.PauseResumeUpgradeNotice);

            // 
            // exitToolStripMenuItem
            // 
            this.exitToolStripMenuItem.Name = "exitToolStripMenuItem";
            this.exitToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.exitToolStripMenuItem.Text = "&Exit";
            this.exitToolStripMenuItem.Click += new System.EventHandler(this.ExitToolStripMenuItemClickHandler);
            // 
            // SystrayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 261);
            this.Name = "SystrayForm";
            this.contextMenuStripSysTray.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}

