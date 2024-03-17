
namespace PersistentWindows.SystrayShell
{
    partial class HotKeyWindow
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            this.buttonPrevTab = new System.Windows.Forms.Button();
            this.buttonNextTab = new System.Windows.Forms.Button();
            this.buttonCloseTab = new System.Windows.Forms.Button();
            this.buttonNewTab = new System.Windows.Forms.Button();
            this.buttonHome = new System.Windows.Forms.Button();
            this.buttonEnd = new System.Windows.Forms.Button();
            this.buttonPageUp = new System.Windows.Forms.Button();
            this.buttonPageDown = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonPrevTab
            // 
            this.buttonPrevTab.BackColor = System.Drawing.SystemColors.Info;
            this.buttonPrevTab.Location = new System.Drawing.Point(23, 12);
            this.buttonPrevTab.Name = "buttonPrevTab";
            this.buttonPrevTab.Size = new System.Drawing.Size(73, 47);
            this.buttonPrevTab.TabIndex = 0;
            this.buttonPrevTab.Text = "Prev Tab";
            this.buttonPrevTab.UseVisualStyleBackColor = false;
            // 
            // buttonNextTab
            // 
            this.buttonNextTab.BackColor = System.Drawing.SystemColors.Info;
            this.buttonNextTab.Location = new System.Drawing.Point(225, 12);
            this.buttonNextTab.Name = "buttonNextTab";
            this.buttonNextTab.Size = new System.Drawing.Size(72, 47);
            this.buttonNextTab.TabIndex = 1;
            this.buttonNextTab.Text = "Next Tab";
            this.buttonNextTab.UseVisualStyleBackColor = false;
            // 
            // buttonCloseTab
            // 
            this.buttonCloseTab.BackColor = System.Drawing.SystemColors.Info;
            this.buttonCloseTab.Location = new System.Drawing.Point(23, 116);
            this.buttonCloseTab.Name = "buttonCloseTab";
            this.buttonCloseTab.Size = new System.Drawing.Size(73, 52);
            this.buttonCloseTab.TabIndex = 2;
            this.buttonCloseTab.Text = "Close Tab";
            this.buttonCloseTab.UseVisualStyleBackColor = false;
            // 
            // buttonNewTab
            // 
            this.buttonNewTab.BackColor = System.Drawing.SystemColors.Info;
            this.buttonNewTab.Location = new System.Drawing.Point(225, 116);
            this.buttonNewTab.Name = "buttonNewTab";
            this.buttonNewTab.Size = new System.Drawing.Size(72, 52);
            this.buttonNewTab.TabIndex = 3;
            this.buttonNewTab.Text = "New  Tab";
            this.buttonNewTab.UseVisualStyleBackColor = false;
            // 
            // buttonHome
            // 
            this.buttonHome.BackColor = System.Drawing.SystemColors.Info;
            this.buttonHome.Location = new System.Drawing.Point(123, 12);
            this.buttonHome.Name = "buttonHome";
            this.buttonHome.Size = new System.Drawing.Size(73, 47);
            this.buttonHome.TabIndex = 4;
            this.buttonHome.Text = "Home";
            this.buttonHome.UseVisualStyleBackColor = false;
            // 
            // buttonEnd
            // 
            this.buttonEnd.BackColor = System.Drawing.SystemColors.Info;
            this.buttonEnd.Location = new System.Drawing.Point(123, 116);
            this.buttonEnd.Name = "buttonEnd";
            this.buttonEnd.Size = new System.Drawing.Size(73, 52);
            this.buttonEnd.TabIndex = 5;
            this.buttonEnd.Text = "End";
            this.buttonEnd.UseVisualStyleBackColor = false;
            // 
            // buttonPageUp
            // 
            this.buttonPageUp.BackColor = System.Drawing.SystemColors.Info;
            this.buttonPageUp.Location = new System.Drawing.Point(23, 65);
            this.buttonPageUp.Name = "buttonPageUp";
            this.buttonPageUp.Size = new System.Drawing.Size(73, 45);
            this.buttonPageUp.TabIndex = 6;
            this.buttonPageUp.Text = "Page  Up";
            this.buttonPageUp.UseVisualStyleBackColor = false;
            // 
            // buttonPageDown
            // 
            this.buttonPageDown.BackColor = System.Drawing.SystemColors.Info;
            this.buttonPageDown.Location = new System.Drawing.Point(224, 65);
            this.buttonPageDown.Name = "buttonPageDown";
            this.buttonPageDown.Size = new System.Drawing.Size(73, 45);
            this.buttonPageDown.TabIndex = 7;
            this.buttonPageDown.Text = "Page Down";
            this.buttonPageDown.UseVisualStyleBackColor = false;
            // 
            // HotKeyWindow
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.BackColor = System.Drawing.SystemColors.MenuHighlight;
            this.ClientSize = new System.Drawing.Size(320, 180);
            this.Controls.Add(this.buttonPageDown);
            this.Controls.Add(this.buttonPageUp);
            this.Controls.Add(this.buttonEnd);
            this.Controls.Add(this.buttonHome);
            this.Controls.Add(this.buttonNewTab);
            this.Controls.Add(this.buttonCloseTab);
            this.Controls.Add(this.buttonNextTab);
            this.Controls.Add(this.buttonPrevTab);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "HotKeyWindow";
            this.Opacity = 0.5D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonPrevTab;
        private System.Windows.Forms.Button buttonNextTab;
        private System.Windows.Forms.Button buttonCloseTab;
        private System.Windows.Forms.Button buttonNewTab;
        private System.Windows.Forms.Button buttonHome;
        private System.Windows.Forms.Button buttonEnd;
        private System.Windows.Forms.Button buttonPageUp;
        private System.Windows.Forms.Button buttonPageDown;
    }
}