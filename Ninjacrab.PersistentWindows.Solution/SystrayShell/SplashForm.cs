using System;
using System.Windows.Forms;

namespace PersistentWindows.SystrayShell
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
    Contributors:  Kang Yu, Sean Aitken
    ";

        }

        private void label2_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(Program.Contributors);
        }
    }
}
