using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SplashForm : Form
    {
        public SplashForm()
        {
            InitializeComponent();
        }

        private void label1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.ProjectUrl);
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            progressBar1.PerformStep();
            if (progressBar1.Value == progressBar1.Maximum)
            {
                this.Close();
            }
        }

        private void SplashForm_Load(object sender, EventArgs e)
        {
            this.label1.Text =
    $@"
    Persistent Windows
    Version {Application.ProductVersion}
                
    Author:        Min Yong Kim
    Contributors:  Kang Yu, Sean Aitken";

        }
    }
}
