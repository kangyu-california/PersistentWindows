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

        private System.Windows.Forms.ToolStripMenuItem captureToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem restoreToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem diagnosticsToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem aboutToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator[] toolStripMenuItem = new System.Windows.Forms.ToolStripSeparator[3];

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
            this.diagnosticsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.aboutToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.captureToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.restoreToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            for (int i = 0; i < 3; ++i)
            {
                this.toolStripMenuItem[i] = new System.Windows.Forms.ToolStripSeparator();
                this.toolStripMenuItem[i].Name = $"toolStripMenuItem{i}";
                this.toolStripMenuItem[i].Size = new System.Drawing.Size(132, 6);
            }
            this.exitToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStripSysTray.SuspendLayout();
            this.SuspendLayout();
            // 
            // notifyIconMain
            // 
            this.notifyIconMain.ContextMenuStrip = this.contextMenuStripSysTray;
            //this.notifyIconMain.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIconMain.Icon")));
            this.notifyIconMain.Icon = Properties.Resources.pwIcon;
            this.notifyIconMain.Text = $"Persistent Windows {Application.ProductVersion}";
            this.notifyIconMain.Visible = true;
            // 
            // contextMenuStripSysTray
            // 
            this.contextMenuStripSysTray.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                this.captureToolStripMenuItem,
                this.toolStripMenuItem[0],
                this.restoreToolStripMenuItem,
                this.toolStripMenuItem[1],
                this.diagnosticsToolStripMenuItem,
                this.aboutToolStripMenuItem,
                this.toolStripMenuItem[2],
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

            // 
            // diagnosticsToolStripMenuItem
            // 
            this.diagnosticsToolStripMenuItem.Name = "diagnosticsToolStripMenuItem";
            this.diagnosticsToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.diagnosticsToolStripMenuItem.Text = "&Diagnostics";
            this.diagnosticsToolStripMenuItem.Click += new System.EventHandler(this.DiagnosticsToolStripMenuItemClickHandler);
            // 
            // aboutToolStripMenuItem
            // 
            this.aboutToolStripMenuItem.Name = "aboutToolStripMenuItem";
            this.aboutToolStripMenuItem.Size = new System.Drawing.Size(135, 22);
            this.aboutToolStripMenuItem.Text = "&About";
            this.aboutToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItemClickHandler);

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
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            //this.Icon = Properties.Resources.pwIcon;
            this.Name = "SystrayForm";
            this.contextMenuStripSysTray.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
    }
}

