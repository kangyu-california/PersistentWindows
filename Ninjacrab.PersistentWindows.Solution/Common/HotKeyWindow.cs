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

namespace PersistentWindows.Common
{
    public partial class HotKeyWindow : Form
    {
        public static IntPtr handle = IntPtr.Zero;

        private System.Timers.Timer aliveTimer;
        private System.Timers.Timer mouseScrollDelayTimer;
        private bool stay = true;
        private bool init = true;
        private int mouseOffset = 0;

        public HotKeyWindow()
        {
            InitializeComponent();

            KeyUp += new KeyEventHandler(FormKeyUp);
            MouseDown += new MouseEventHandler(FormMouseDown);
            MouseWheel += new MouseEventHandler(FormMouseWheel);
            Move += new EventHandler(FormMove);
            FormClosing += new FormClosingEventHandler(FormClose);

            Icon = PersistentWindowProcessor.icon;

            aliveTimer = new System.Timers.Timer(2000);
            aliveTimer.Elapsed += AliveTimerCallBack;
            aliveTimer.SynchronizingObject = this;
            aliveTimer.AutoReset = false;
            aliveTimer.Enabled = false;

            mouseScrollDelayTimer = new System.Timers.Timer(250);
            mouseScrollDelayTimer.Elapsed += MouseScrollCallBack;
            mouseScrollDelayTimer.AutoReset = false;
            mouseScrollDelayTimer.Enabled = false;

            handle = Handle;
        }

        //hack to resolve failure to repeatively set cursor pos to same value in rdp session
        private void ResetCursorPos()
        {
            User32.SetCursorPos(Left + Size.Width / 2 + mouseOffset, Top + Size.Height / 2);
            mouseOffset++;
            if (mouseOffset == 3)
                mouseOffset = -2;
        }

        private void SetCursorPos()
        {
            IntPtr fgwnd = GetForegroundWindow();
            RECT fgwinPos = new RECT();
            User32.GetWindowRect(fgwnd, ref fgwinPos);
            User32.SetCursorPos(fgwinPos.Left + fgwinPos.Width / 2, fgwinPos.Top + fgwinPos.Height / 2);
        }

        private void FormMove(object sender, EventArgs e)
        {
            if (!Visible)
                return;

            stay = true;
        }

        private void FormClose(object sender, FormClosingEventArgs e)
        {
            if (InvokeRequired)
                BeginInvoke((Action) delegate ()
                {
                    FormClose(sender, e);
                });
            else
            {
                e.Cancel = true;
                if (User32.IsWindow(Handle))
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }
        }

        private void FormMouseDown(object sender, MouseEventArgs e)
        {
            StartAliveTimer();

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
                //refresh current webpage
                SendKeys.Send("{F5}");
            }

            User32.SetForegroundWindow(Handle);
            ResetCursorPos();
        }

        private void FormMouseWheel(object sender, MouseEventArgs e)
        {
            StartAliveTimer();

            //User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SetCursorPos();

            int delta = e.Delta;
            User32.mouse_event(MouseAction.MOUSEEVENTF_WHEEL, 0, 0, delta, UIntPtr.Zero);
            //Show();

            StartMouseScrollTimer();
        }

        bool IsBrowserWindow(IntPtr hwnd)
        {
            return PersistentWindowProcessor.IsBrowserWindow(hwnd);
        }

        void FormKeyUp(object sender, KeyEventArgs e)
        {
            //allow shift
            if (e.Control || e.Alt)
                return;

            StartAliveTimer();
            TopMost = true;

            IntPtr fgwnd = GetForegroundWindow();

            bool return_focus_to_hotkey_window = true;
            if (e.KeyCode == Keys.Escape)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{ESC}");
                return_focus_to_hotkey_window = false;
            }
            else if (e.KeyCode == Keys.Tab)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{TAB}");
                return_focus_to_hotkey_window = false;
            }
            else if (e.KeyCode == Keys.Q)
            {
                User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
                User32.SetForegroundWindow(fgwnd);
            }
            else if (e.KeyCode == Keys.W && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //kill tab, ctrl + w
                SendKeys.Send("^w");
            }
            else if (e.KeyCode == Keys.E)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{HOME}");

                /*
                //follow cursor
                POINT cursor;
                User32.GetCursorPos(out cursor);

                //activate window under cursor
                IntPtr hwnd = User32.WindowFromPoint(cursor);
                if (!PersistentWindowProcessor.IsDesktopWindow(hwnd))
                    User32.SetForegroundWindow(hwnd);

                //relocate hotkey window
                Left = cursor.X - Size.Width / 2;
                Top = cursor.Y - Size.Height / 2;
                */
            }
            else if (e.KeyCode == Keys.R)
            {
                //reload
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("^r");
            }
            else if (e.KeyCode == Keys.T && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //new tab, ctrl + t
                if (e.Shift)
                    SendKeys.Send("^T"); //open last closed tab
                else
                {
                    SendKeys.Send("^t"); //new tab
                    SendKeys.Send("^l");
                    return_focus_to_hotkey_window = false;
                }
            }
            else if (e.KeyCode == Keys.A && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //address, ctrl L
                SendKeys.Send("^l");
                return_focus_to_hotkey_window = false;
            }
            else if (e.KeyCode == Keys.S && IsBrowserWindow(fgwnd))
            {
                // search
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                SendKeys.Send("^k");
                return_focus_to_hotkey_window = false;
            }
            else if (e.KeyCode == Keys.D)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{END}");

                /*
                //follow cursor
                POINT cursor;
                User32.GetCursorPos(out cursor);

                //relocate hotkey window
                Left = cursor.X - Size.Width / 2;
                Top = cursor.Y - Size.Height / 2;
                */

            }
            else if (e.KeyCode == Keys.F && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //next url
                SendKeys.Send("%{RIGHT}");
            }
            else if (e.KeyCode == Keys.G && IsBrowserWindow(fgwnd))
            {
                //goto tab
                //ctrl shift A (only for chrome)
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("^+a");
            }
            else if (e.KeyCode == Keys.F5)
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //refresh
                SendKeys.Send("{F5}");
            }
            else if (e.KeyCode == Keys.Z)
            {
                //toggle stay
                stay = !stay;
                if (!stay)
                    User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
            }
            else if (e.KeyCode == Keys.X)
            {
                //TODO
            }
            else if (e.KeyCode == Keys.C)
            {
                //copy (duplicate) tab
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("^l");
                SendKeys.Send("%{ENTER}");
            }
            else if (e.KeyCode == Keys.V)
            {
                //TODO
            }
            else if (e.KeyCode == Keys.B && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //backward, prev url
                SendKeys.Send("%{LEFT}");
            }
            else if (e.KeyCode == Keys.J)
            {
                //down one line
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{DOWN}");
            }
            else if (e.KeyCode == Keys.K)
            {
                //up one line
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{UP}");
            }
            else if (e.KeyCode == Keys.P)
            {
                //up one page
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{PGUP}");
            }
            else if (e.KeyCode == Keys.N || e.KeyCode == Keys.Space)
            {
                //down one page
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{PGDN}");
            }
            else if (e.KeyCode == Keys.H)
            {
                //left
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{LEFT}");
            }
            else if (e.KeyCode == Keys.L)
            {
                //right
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{RIGHT}");
            }
            else
            {
                return_focus_to_hotkey_window = false;
            }

            if (return_focus_to_hotkey_window)
            {
                User32.SetForegroundWindow(Handle);
                ResetCursorPos();
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
                    if (init)
                    {
                        init = false;

                        POINT cursor;
                        User32.GetCursorPos(out cursor);
                        Left = cursor.X - Size.Width / 2;
                        Top = cursor.Y - Size.Height / 2;
                    }
                    Show();
                    User32.SetForegroundWindow(Handle);
                    ResetCursorPos();
                    StartAliveTimer();
                }
                else if (stay)
                {
                    User32.SetForegroundWindow(Handle);
                    ResetCursorPos();
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

        public void StartMouseScrollTimer(int milliseconds = 250)
        {
            mouseScrollDelayTimer.Interval = milliseconds;
            mouseScrollDelayTimer.AutoReset = false;
            mouseScrollDelayTimer.Enabled = true;
        }

        private void MouseScrollCallBack(Object source, ElapsedEventArgs e)
        {
            if (InvokeRequired)
                BeginInvoke((Action)delegate ()
                {
                    MouseScrollCallBack(source, e);
                });
            else if (User32.IsWindowVisible(Handle))
            {
                //Show();
                User32.SetForegroundWindow(Handle);
                ResetCursorPos();
            }    
        }

        private void AliveTimerCallBack(Object source, ElapsedEventArgs e)
        {
            if (stay)
                return;

            if (User32.IsWindowVisible(Handle))
                User32.ShowWindow(Handle, (int)ShowWindowCommands.Hide);
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
            {
                SendKeys.Send("^T");
                User32.SetForegroundWindow(Handle);
            }
            else
            {
                SendKeys.Send("^t");
                SendKeys.Send("^l");
            }
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

        private static IntPtr GetForegroundWindow()
        {
            return PersistentWindowProcessor.GetForegroundWindow();
        }

    }
}
