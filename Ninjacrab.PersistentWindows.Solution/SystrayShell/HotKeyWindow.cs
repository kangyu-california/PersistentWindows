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
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);

            if (e.Button == MouseButtons.Left)
            {
                //page down
                SendKeys.Send("{PGDN}");
            }
            else if (e.Button == MouseButtons.Right)
            {
                //page up
                SendKeys.Send("{PGUP}");
            }
            else if (e.Button == MouseButtons.Middle)
            {

            }

            User32.SetForegroundWindow(Handle);
        }

        void FormKeyDown(object sender, KeyEventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);

            if (e.KeyCode == Keys.Q)
            {
                //kill tab, ctrl + w
                SendKeys.Send("^w");
            }
            else if (e.KeyCode == Keys.W)
            {
                //new tab, ctrl + t
                if (e.Shift)
                    SendKeys.Send("^T"); //open last closed tab
                else
                    SendKeys.Send("^t"); //new tab
            }
            else if (e.KeyCode == Keys.A)
            {
                //prev url
                SendKeys.Send("%{LEFT}");
            }
            else if (e.KeyCode == Keys.S)
            {
                //next url
                SendKeys.Send("%{RIGHT}");
            }
            else if (e.KeyCode == Keys.Tab)
            {
                //toggle stay
                stay = !stay;
                if (!stay)
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }

            User32.SetForegroundWindow(Handle);
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
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("^+{TAB}");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonNextTab_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("^{TAB}");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonPrevUrl_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("%{LEFT}");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonNextUrl_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("%{RIGHT}");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonCloseTab_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("^w");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonNewTab_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            bool shift_key_pressed = (User32.GetKeyState(0x10) & 0x8000) != 0;
            if (shift_key_pressed)
                SendKeys.Send("^T");
            else
                SendKeys.Send("^t");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonHome_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("{HOME}");
            User32.SetForegroundWindow(Handle);
        }

        private void buttonEnd_Click(object sender, EventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SendKeys.Send("{END}");
            User32.SetForegroundWindow(Handle);
        }

        private void HotKeyWindow_Load(object sender, EventArgs e)
        {
            Icon = Program.IdleIcon;
        }

        static IntPtr GetForegroundWindow()
        {
            return Program.GetForegroundWindow();
        }
    }
}
