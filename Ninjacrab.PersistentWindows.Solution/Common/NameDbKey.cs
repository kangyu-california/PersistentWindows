using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PersistentWindows.Common
{
    public partial class NameDbEntry : Form
    {
        public string db_entry_name = null;
        public NameDbEntry()
        {
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
