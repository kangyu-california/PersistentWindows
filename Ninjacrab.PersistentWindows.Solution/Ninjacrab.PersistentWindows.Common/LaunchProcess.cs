using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ninjacrab.PersistentWindows.Common
{
    public partial class LaunchProcess : Form
    {
        public string buttonName = "None";

        public LaunchProcess(string title)
        {
            InitializeComponent();

            // Creating and setting the label 
            Label title_label = new Label();
            title_label.Text = title;
            title_label.Location = new Point(240 - title.Length * 4, 100);
            title_label.AutoSize = true;
            title_label.BorderStyle = BorderStyle.Fixed3D;
            title_label.Font = new Font("Calibri", 13);
            title_label.Padding = new Padding(6);
            title_label.TextAlign = ContentAlignment.MiddleCenter;

            // Adding this control to the form 
            this.Controls.Add(title_label);
        }

        private void RunProcess_Load(object sender, EventArgs e)
        {
        }

        private void Yes_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            buttonName = button.Name;
            Close();
        }

        private void YesToAll_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            buttonName = button.Name;
            Close();
        }

        private void No_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            buttonName = button.Name;
            Close();
        }

        private void NoToAll_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            buttonName = button.Name;
            Close();
        }

    }
}
