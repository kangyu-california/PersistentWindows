using System;
using System.Windows.Forms;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    public partial class NameDbEntry : Form
    {
        public string db_entry_name = null;
        public NameDbEntry()
        {
            User32.SetThreadDpiAwarenessContext(User32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            InitializeComponent();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
        }

        private void button1_Click(object sender, EventArgs e)
        {
            db_entry_name = textBox1.Text;
            Close();
        }

        private void hint_Click(object sender, EventArgs e)
        {

        }
    }
}
