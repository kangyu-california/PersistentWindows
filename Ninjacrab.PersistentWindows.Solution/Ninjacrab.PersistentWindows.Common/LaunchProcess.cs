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
        public string buttonSelect;
        string processName;

        public LaunchProcess(string process_name)
        {
            processName = process_name;
            InitializeComponent();
        }

        private void RunProcess_Load(object sender, EventArgs e)
        {
            ProcessName.Text = processName;
        }

        private void Yes_Click(object sender, EventArgs e)
        {
            buttonSelect = "Yes";
            Close();
        }

        private void YesToAll_Click(object sender, EventArgs e)
        {
            buttonSelect = "YesToAll";
            Close();
        }

        private void No_Click(object sender, EventArgs e)
        {
            buttonSelect = "No";
            Close();
        }

        private void NoToAll_Click(object sender, EventArgs e)
        {
            buttonSelect = "NoToAll";
            Close();
        }

    }
}
