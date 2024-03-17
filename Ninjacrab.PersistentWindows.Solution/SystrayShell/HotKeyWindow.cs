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
        public HotKeyWindow()
        {
            InitializeComponent();

            KeyDown += new KeyEventHandler(FormKeyDown);
            MouseDown += new MouseEventHandler(FormMouseDown);

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

        private void FormMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                int i = 0;
                i++;
            }
            else if (e.Button == MouseButtons.Right)
            {
                int i = 0;
                i++;
            }
        }

        void FormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Q)
            {
                int i = 0;
                i++;
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
            // TODO: restart timer if alt key pressed

            /*
            if (this.InvokeRequired)
                BeginInvoke((Action) delegate ()
                {
                    AliveTimerCallBack(source, e);
                });
            else
            */
            {
                if (User32.IsWindowVisible(Handle))
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }
        }
    }
}
