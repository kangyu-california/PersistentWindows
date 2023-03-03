using System;
using System.Drawing;
using System.Windows.Forms;

namespace PersistentWindows.Common
{
    public partial class LaunchProcess : Form
    {
        public string buttonName = "None";

        public LaunchProcess(string process, string title)
        {
            InitializeComponent();

            // Creating and setting the label 
            Label process_name = new Label();
            process_name.Font = new Font("Calibri", 13);
            process_name.Location = new Point(Math.Max(50, 240 - process.Length * 4), 80);
            process_name.AutoSize = true;
            process_name.BorderStyle = BorderStyle.Fixed3D;
            process_name.Padding = new Padding(6);
            process_name.TextAlign = ContentAlignment.MiddleCenter;
            process_name.Text = process;
            this.Controls.Add(process_name);

            TextBox window_title = new TextBox();
            window_title.Font = new Font("Calibri", 13);
            window_title.Location = new Point(Math.Max(50, 240 - title.Length * 4), 130);
            window_title.Width = Math.Min(400, title.Length * 10);
            window_title.TextAlign = HorizontalAlignment.Center;
            window_title.ReadOnly = true;
            window_title.Text = title;
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
