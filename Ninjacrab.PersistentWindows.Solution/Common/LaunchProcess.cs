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
            User32.SetThreadDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

            InitializeComponent();

            // Creating and setting the label 
            Label process_name = new Label();
            process_name.Font = new Font("Calibri", 13);
            process_name.AutoSize = true;
            process_name.BorderStyle = BorderStyle.Fixed3D;
            process_name.Padding = new Padding(6);
            process_name.Text = process;
            process_name.Top = 100;
            process_name.Left = this.Width / 2 - process_name.PreferredWidth / 2;
            //process_name.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(process_name);

            Label window_title = new Label();
            window_title.Font = new Font("Calibri", 13);
            window_title.AutoSize = true;
            window_title.BorderStyle = BorderStyle.Fixed3D;
            window_title.Padding = new Padding(6);
            window_title.Text = title;
            window_title.Top = 150;
            window_title.Left = this.Width / 2 - window_title.PreferredWidth / 2;
            //window_title.TextAlign = ContentAlignment.MiddleCenter;
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
