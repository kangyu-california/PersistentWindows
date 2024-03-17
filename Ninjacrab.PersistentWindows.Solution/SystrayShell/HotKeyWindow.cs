using System;
using System.Timers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.SystrayShell
{
    public partial class HotKeyWindow : Form
    {
        private System.Timers.Timer aliveTimer;
        private System.Timers.Timer clickDelayTimer;
        private bool stay = false;

        public HotKeyWindow()
        {
            InitializeComponent();

            KeyDown += new KeyEventHandler(FormKeyDown);
            MouseDown += new MouseEventHandler(FormMouseDown);
            Move += new EventHandler(FormMove);
            FormClosing += new FormClosingEventHandler(FormClose);


            aliveTimer = new System.Timers.Timer(2000);
            aliveTimer.Elapsed += AliveTimerCallBack;
            aliveTimer.SynchronizingObject = this;
            aliveTimer.AutoReset = false;
            aliveTimer.Enabled = false;

            clickDelayTimer = new System.Timers.Timer(1000);
            clickDelayTimer.Elapsed += ClickTimerCallBack;
            clickDelayTimer.SynchronizingObject = this;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = false;
        }

        private void FormMove(object sender, EventArgs e)
        {
            if (!Visible)
                return;

            stay = true;
        }

        private void FormClose(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
        }

        private void FormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                //prev link, alt + left
            }
            else if (e.Button == MouseButtons.Right)
            {
                //next link, alt + right
            }
        }

        void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Q)
            {
                //kill tab, ctrl + w
            }
        }

        public void HotKeyPressed()
        {
            if (InvokeRequired)
                BeginInvoke((Action) delegate ()
                {
                    HotKeyPressed();
                });
            else
            {
                if (!User32.IsWindowVisible(Handle))
                {
                    POINT cursor;
                    User32.GetCursorPos(out cursor);
                    Left = cursor.X - Size.Width / 2;
                    Top = cursor.Y - Size.Height / 2;
                    Show();
                    User32.SetForegroundWindow(Handle);
                    StartAliveTimer();
                }
                else
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }

        }

        public void StartAliveTimer(int milliseconds = 2000)
        {
            aliveTimer.Interval = milliseconds;
            aliveTimer.AutoReset = false;
            aliveTimer.Enabled = true;
        }

        public void StartClickDelayTimer(int milliseconds)
        {
            clickDelayTimer.Interval = milliseconds;
            clickDelayTimer.AutoReset = false;
            clickDelayTimer.Enabled = true;
        }

        private void ClickTimerCallBack(Object source, ElapsedEventArgs e)
        {
        }
        private void AliveTimerCallBack(Object source, ElapsedEventArgs e)
        {
            if (stay)
                return;

            // TODO: restart timer if alt key pressed

            {
                if (User32.IsWindowVisible(Handle))
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }
        }

        private void buttonPrevTab_Click(object sender, EventArgs e)
        {

        }

        private void buttonNextTab_Click(object sender, EventArgs e)
        {

        }

        private void buttonPrevUrl_Click(object sender, EventArgs e)
        {

        }

        private void buttonNextUrl_Click(object sender, EventArgs e)
        {

        }

        private void buttonCloseTab_Click(object sender, EventArgs e)
        {

        }

        private void buttonNewTab_Click(object sender, EventArgs e)
        {

        }

        private void buttonHome_Click(object sender, EventArgs e)
        {

        }

        private void buttonEnd_Click(object sender, EventArgs e)
        {

        }
    }
}
