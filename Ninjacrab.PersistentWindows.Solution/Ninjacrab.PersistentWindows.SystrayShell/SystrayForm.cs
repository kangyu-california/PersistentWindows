using System;
using System.Windows.Forms;
using Ninjacrab.PersistentWindows.WpfShell;

namespace Ninjacrab.PersistentWindows.SystrayShell
{
    public partial class SystrayForm : Form
    {
        public MainWindow MainView { get; set; }

        public SystrayForm()
        {
            InitializeComponent();
        }

        private void DiagnosticsToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            bool shouldShow = false;
            if (this.MainView == null ||
                this.MainView.IsClosed)
            {
                this.MainView = new MainWindow();
                shouldShow = true;
            }

            if (shouldShow)
            {
                this.MainView.Show();
            }
        }

        private void ExitToolStripMenuItemClickHandler(object sender, EventArgs e)
        {
            this.notifyIconMain.Visible = false;
            this.notifyIconMain.Icon = null;
            Application.Exit();
        }

    }
}
