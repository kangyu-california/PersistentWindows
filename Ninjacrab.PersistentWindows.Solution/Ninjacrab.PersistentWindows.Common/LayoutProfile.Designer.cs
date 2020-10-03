using System.Windows.Forms;
using System.Drawing;

namespace Ninjacrab.PersistentWindows.Common
{
    partial class LayoutProfile
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LayoutProfile));
            this.ProfileName = new System.Windows.Forms.TextBox();
            this.AddProfile = new System.Windows.Forms.Button();
            this.SwitchProfile = new System.Windows.Forms.Button();
            this.DeleteProfile = new System.Windows.Forms.Button();
            this.ProfileList = new System.Windows.Forms.ListBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.CloseBtn = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ProfileName
            // 
            this.ProfileName.Location = new System.Drawing.Point(92, 80);
            this.ProfileName.Name = "ProfileName";
            this.ProfileName.Size = new System.Drawing.Size(215, 22);
            this.ProfileName.TabIndex = 1;
            this.ProfileName.TextChanged += new System.EventHandler(this.ProfileName_TextChanged);
            // 
            // AddProfile
            // 
            this.AddProfile.Location = new System.Drawing.Point(359, 75);
            this.AddProfile.Name = "AddProfile";
            this.AddProfile.Size = new System.Drawing.Size(82, 33);
            this.AddProfile.TabIndex = 2;
            this.AddProfile.Text = "Add";
            this.AddProfile.UseVisualStyleBackColor = true;
            this.AddProfile.Click += new System.EventHandler(this.AddProfile_Click);
            // 
            // SwitchProfile
            // 
            this.SwitchProfile.Location = new System.Drawing.Point(359, 152);
            this.SwitchProfile.Name = "SwitchProfile";
            this.SwitchProfile.Size = new System.Drawing.Size(82, 37);
            this.SwitchProfile.TabIndex = 3;
            this.SwitchProfile.Text = "Open";
            this.SwitchProfile.UseVisualStyleBackColor = true;
            this.SwitchProfile.Click += new System.EventHandler(this.SwitchProfile_Click);
            // 
            // DeleteProfile
            // 
            this.DeleteProfile.Location = new System.Drawing.Point(359, 227);
            this.DeleteProfile.Name = "DeleteProfile";
            this.DeleteProfile.Size = new System.Drawing.Size(82, 36);
            this.DeleteProfile.TabIndex = 4;
            this.DeleteProfile.Text = "Delete";
            this.DeleteProfile.UseVisualStyleBackColor = true;
            this.DeleteProfile.Click += new System.EventHandler(this.DeleteProfile_Click);
            // 
            // ProfileList
            // 
            this.ProfileList.FormattingEnabled = true;
            this.ProfileList.ItemHeight = 16;
            this.ProfileList.Location = new System.Drawing.Point(92, 152);
            this.ProfileList.Name = "ProfileList";
            this.ProfileList.Size = new System.Drawing.Size(215, 196);
            this.ProfileList.TabIndex = 0;
            this.ProfileList.SelectedIndexChanged += new System.EventHandler(this.ProfileList_SelectedIndexChanged);
            this.ProfileList.KeyDown += new System.Windows.Forms.KeyEventHandler(this.ProfileList_KeyDown);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(92, 132);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(106, 17);
            this.label1.TabIndex = 5;
            this.label1.Text = "Existing profiles";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(92, 60);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(117, 17);
            this.label2.TabIndex = 6;
            this.label2.Text = "New profile name";
            // 
            // CloseBtn
            // 
            this.CloseBtn.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.CloseBtn.Location = new System.Drawing.Point(359, 308);
            this.CloseBtn.Name = "CloseBtn";
            this.CloseBtn.Size = new System.Drawing.Size(82, 40);
            this.CloseBtn.TabIndex = 7;
            this.CloseBtn.Text = "Cancel";
            this.CloseBtn.UseVisualStyleBackColor = true;
            this.CloseBtn.Click += new System.EventHandler(this.Close_Click);
            // 
            // LayoutProfile
            // 
            this.AcceptButton = this.CloseBtn;
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(525, 417);
            this.Controls.Add(this.CloseBtn);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.DeleteProfile);
            this.Controls.Add(this.SwitchProfile);
            this.Controls.Add(this.AddProfile);
            this.Controls.Add(this.ProfileName);
            this.Controls.Add(this.ProfileList);
            //this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LayoutProfile";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Manage Layout Profiles";
            this.Load += new System.EventHandler(this.LayoutProfile_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.TextBox ProfileName;
        private System.Windows.Forms.Button AddProfile;
        private System.Windows.Forms.Button SwitchProfile;
        private System.Windows.Forms.Button DeleteProfile;
        private System.Windows.Forms.ListBox ProfileList;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button CloseBtn;
    }
}