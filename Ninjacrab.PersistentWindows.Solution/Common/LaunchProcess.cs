using System;
using System.Drawing;
using System.Windows.Forms;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    public partial class LaunchProcess : Form
    {
        public string buttonName = "None";

        public LaunchProcess(string process, string title)
        {
            User32.SetThreadDpiAwarenessContextSafe();

            InitializeComponent();

            // Creating and setting the label 
            Label process_name = new Label();
            process_name.Font = new Font("Calibri", 13);
            process_name.AutoSize = true;
            process_name.BorderStyle = BorderStyle.Fixed3D;
            process_name.Padding = new Padding(6);
            process_name.Text = process;
            process_name.Top = label1.Top;
            label1.Visible = false;
            process_name.Left = this.Width / 2 - process_name.PreferredWidth / 2;
            //process_name.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(process_name);

            Label window_title = new Label();
            window_title.Font = new Font("Calibri", 13);
            window_title.AutoSize = true;
            window_title.TextAlign = ContentAlignment.TopCenter;
            window_title.BorderStyle = BorderStyle.Fixed3D;
            window_title.Padding = new Padding(6);
            window_title.Text = title;
            window_title.Top = label2.Top;
            label2.Visible = false;
            if (this.Width < window_title.PreferredWidth)
            {
                window_title.Top = label2.Top - 5;
                int rows = window_title.PreferredWidth / this.Width + 1;
                var resize = new Size(this.Width,
                    //window_title.PreferredHeight * (window_title.PreferredWidth / this.Width));
                    window_title.PreferredHeight * rows);
                window_title.AutoSize = false;
                window_title.Size = resize;
            }
            else
            {
                window_title.Left = this.Width / 2 - window_title.PreferredWidth / 2;
            }
            this.Controls.Add(window_title);

        }

        private void RunProcess_Load(object sender, EventArgs e)
        {
        }

        private void Button_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            buttonName = button.Name;
            Close();
        }

        private void Yes_Click(object sender, EventArgs e)
        {
            Button_Click(sender, e);
        }

        private void YesToAll_Click(object sender, EventArgs e)
        {
            Button_Click(sender, e);
        }

        private void No_Click(object sender, EventArgs e)
        {
            Button_Click(sender, e);
        }

        private void NoToAll_Click(object sender, EventArgs e)
        {
            Button_Click(sender, e);
        }
    }
}
