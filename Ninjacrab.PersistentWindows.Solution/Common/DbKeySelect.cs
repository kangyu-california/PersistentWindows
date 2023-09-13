using System;
using System.Drawing;
using System.Windows.Forms;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    public partial class DbKeySelect : Form
    {
        public string result = "";

        public DbKeySelect()
        {
            User32.SetThreadDpiAwarenessContextSafe();
            InitializeComponent();
        }

        public void InsertCollection(string collection)
        {
            ListLayout.Items.Add(collection);
        }

        private void Ok_Click(object sender, EventArgs e)
        {
            result = selected.Text;
            Close();
        }

        private void ListLayout_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ListLayout.SelectedItem == null)
                return;
            selected.Text = ListLayout.SelectedItem.ToString();
        }
    }
}
