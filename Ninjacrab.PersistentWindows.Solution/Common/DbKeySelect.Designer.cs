
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
            this.selected = new System.Windows.Forms.Label();
            this.Cancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // LayoutList
            // 
            this.ListLayout.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.ListLayout.Font = new System.Drawing.Font("Microsoft Sans Serif", 10.2F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ListLayout.FormattingEnabled = true;
            this.ListLayout.ItemHeight = 17;
            this.ListLayout.Location = new System.Drawing.Point(12, 12);
            this.ListLayout.Name = "LayoutList";
            this.ListLayout.Size = new System.Drawing.Size(776, 293);
            this.ListLayout.TabIndex = 0;
            this.ListLayout.SelectedIndexChanged += new System.EventHandler(this.ListLayout_SelectedIndexChanged);
            // 
            // Ok
            // 
            this.Ok.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Ok.Location = new System.Drawing.Point(572, 402);
            this.Ok.Name = "Ok";
            this.Ok.Size = new System.Drawing.Size(105, 36);
            this.Ok.TabIndex = 1;
            this.Ok.Text = "OK";
            this.Ok.UseVisualStyleBackColor = true;
            this.Ok.Click += new System.EventHandler(this.Ok_Click);
            // 
            // LayoutItemSelected
            // 
            this.selected.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.selected.Location = new System.Drawing.Point(12, 311);
            this.selected.Margin = new System.Windows.Forms.Padding(3);
            this.selected.Name = "LayoutItemSelected";
            this.selected.Size = new System.Drawing.Size(776, 85);
            this.selected.TabIndex = 2;
            // 
            // Cancel
            // 
            this.Cancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.Cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.Cancel.Location = new System.Drawing.Point(683, 402);
            this.Cancel.Name = "Cancel";
            this.Cancel.Size = new System.Drawing.Size(105, 36);
            this.Cancel.TabIndex = 3;
            this.Cancel.Text = "Cancel";
            this.Cancel.UseVisualStyleBackColor = true;
            this.Cancel.Click += new System.EventHandler(this.Cancel_Click);
            // 
            // DbKeySelect
            // 
            this.AcceptButton = this.Ok;
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Inherit;
            this.CancelButton = this.Cancel;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.Cancel);
            this.Controls.Add(this.selected);
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
        private System.Windows.Forms.Label selected;
        private System.Windows.Forms.Button Cancel;
    }
}