
namespace PersistentWindows.Common
{
    partial class LaunchProcess
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LaunchProcess));
            this.Yes = new System.Windows.Forms.Button();
            this.No = new System.Windows.Forms.Button();
            this.YesToAll = new System.Windows.Forms.Button();
            this.NoToAll = new System.Windows.Forms.Button();
            this.Notice = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // Yes
            // 
            resources.ApplyResources(this.Yes, "Yes");
            this.Yes.Name = "Yes";
            this.Yes.UseVisualStyleBackColor = true;
            this.Yes.Click += new System.EventHandler(this.Yes_Click);
            // 
            // No
            // 
            resources.ApplyResources(this.No, "No");
            this.No.Name = "No";
            this.No.UseVisualStyleBackColor = true;
            this.No.Click += new System.EventHandler(this.No_Click);
            // 
            // YesToAll
            // 
            resources.ApplyResources(this.YesToAll, "YesToAll");
            this.YesToAll.Name = "YesToAll";
            this.YesToAll.UseVisualStyleBackColor = true;
            this.YesToAll.Click += new System.EventHandler(this.YesToAll_Click);
            // 
            // NoToAll
            // 
            resources.ApplyResources(this.NoToAll, "NoToAll");
            this.NoToAll.Name = "NoToAll";
            this.NoToAll.UseVisualStyleBackColor = true;
            this.NoToAll.Click += new System.EventHandler(this.NoToAll_Click);
            // 
            // Notice
            // 
            resources.ApplyResources(this.Notice, "Notice");
            this.Notice.Name = "Notice";
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // LaunchProcess
            // 
            this.AcceptButton = this.Yes;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            resources.ApplyResources(this, "$this");
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.Notice);
            this.Controls.Add(this.NoToAll);
            this.Controls.Add(this.YesToAll);
            this.Controls.Add(this.No);
            this.Controls.Add(this.Yes);
            this.Name = "LaunchProcess";
            this.Load += new System.EventHandler(this.RunProcess_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Button Yes;
        private System.Windows.Forms.Button No;
        private System.Windows.Forms.Button YesToAll;
        private System.Windows.Forms.Button NoToAll;
        private System.Windows.Forms.Label Notice;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
    }
}