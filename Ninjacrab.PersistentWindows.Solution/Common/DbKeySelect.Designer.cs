
namespace PersistentWindows.Common
{
    partial class DbKeySelect
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
            this.ListLayout = new System.Windows.Forms.ListBox();
            this.Ok = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // ListLayout
            // 
            this.ListLayout.FormattingEnabled = true;
            this.ListLayout.ItemHeight = 16;
            this.ListLayout.Location = new System.Drawing.Point(59, 72);
            this.ListLayout.Name = "ListLayout";
            this.ListLayout.Size = new System.Drawing.Size(691, 228);
            this.ListLayout.TabIndex = 0;
            // 
            // Ok
            // 
            this.Ok.Location = new System.Drawing.Point(345, 360);
            this.Ok.Name = "Ok";
            this.Ok.Size = new System.Drawing.Size(105, 36);
            this.Ok.TabIndex = 1;
            this.Ok.Text = "OK";
            this.Ok.UseVisualStyleBackColor = true;
            this.Ok.Click += new System.EventHandler(this.Ok_Click);
            // 
            // DbKeySelect
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.Ok);
            this.Controls.Add(this.ListLayout);
            this.Name = "DbKeySelect";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Select a desktop layout to restore";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.ListBox ListLayout;
        private System.Windows.Forms.Button Ok;
    }
}