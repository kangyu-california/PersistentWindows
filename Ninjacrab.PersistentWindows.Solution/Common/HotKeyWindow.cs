﻿using System;
using System.Timers;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Runtime.InteropServices;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.Common
{
    public partial class HotKeyWindow : Form
    {
        public static IntPtr handle = IntPtr.Zero;

        private static System.Timers.Timer aliveTimer;
        private System.Timers.Timer mouseScrollDelayTimer;
        private bool init = true;
        private bool active = false;
        private static bool tiny = false;
        private int origWidth;
        private int origHeight;
        private int mouseOffset = 0;
        private static POINT lastCursorPos;
        private Color dfltBackColor;
        private bool handCursor = false;

        public HotKeyWindow()
        {
            InitializeComponent();

            origWidth = Width;
            origHeight = Height;
            dfltBackColor = BackColor;

            KeyDown += new KeyEventHandler(FormKeyDown);
            KeyUp += new KeyEventHandler(FormKeyUp);
            MouseDown += new MouseEventHandler(FormMouseDown);
            MouseWheel += new MouseEventHandler(FormMouseWheel);
            FormClosing += new FormClosingEventHandler(FormClose);
            MouseLeave += new EventHandler(FormMouseLeave);

            Icon = PersistentWindowProcessor.icon;

            aliveTimer = new System.Timers.Timer();
            aliveTimer.Elapsed += AliveTimerCallBack;
            aliveTimer.SynchronizingObject = this;
            aliveTimer.AutoReset = false;
            aliveTimer.Enabled = false;

            mouseScrollDelayTimer = new System.Timers.Timer();
            mouseScrollDelayTimer.Elapsed += MouseScrollCallBack;
            mouseScrollDelayTimer.AutoReset = false;
            mouseScrollDelayTimer.Enabled = false;

            handle = Handle;
        }

        private void ToggleWindowSize()
        {
            tiny = !tiny;

            if (tiny)
            {
                FormBorderStyle = FormBorderStyle.None;
                ControlBox = false;
                Width = 8;
                Height = 8;
                Location = new Point(Location.X + origWidth / 2, Location.Y + origHeight / 2);
            }
            else
            {
                FormBorderStyle = FormBorderStyle.Fixed3D;
                ControlBox = true;
                Width = origWidth;
                Height = origHeight;
                Location = new Point(Location.X - origWidth / 2, Location.Y - origHeight / 2);
            }
        }

        private void ResetHotKeyVirtualDesktop()
        {
            //relocate HotKey window to current virtual desktop
            if (!VirtualDesktop.IsWindowOnCurrentVirtualDesktop(Handle))
            {
                IntPtr fgwnd = GetForegroundWindow();
                Guid vd = VirtualDesktop.GetWindowDesktopId(fgwnd);
                VirtualDesktop.MoveWindowToDesktop(Handle, vd);
            }
        }

        private void ResetHotkeyWindowPos()
        {
            POINT cursor;
            User32.GetCursorPos(out cursor);
            Left = cursor.X - Size.Width / 2;
            Top = cursor.Y - Size.Height / 2;
        }

        //hack to resolve failure to repeatively set cursor pos to same value in rdp session
        private void ResetCursorPos()
        {
            User32.SetCursorPos(Left + Size.Width / 2 + mouseOffset + (handCursor ? 10 : 0), Top + Size.Height / 2);
            mouseOffset++;
            if (mouseOffset == 2)
                mouseOffset = -1;
        }

        private void SetCursorPos()
        {
            IntPtr fgwnd = GetForegroundWindow();
            RECT fgwinPos = new RECT();
            User32.GetWindowRect(fgwnd, ref fgwinPos);

            POINT cursor;
            User32.GetCursorPos(out cursor);
            if (User32.PtInRect(ref fgwinPos, cursor))
            {
                if (tiny)
                    Visible = false;
                return; //not need to reposition cursor
            }

            User32.SetCursorPos(fgwinPos.Left + fgwinPos.Width / 2, fgwinPos.Top + fgwinPos.Height / 2);
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
                    Visible = false;
            }
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
                //refresh current webpage
                SendKeys.Send("{F5}");
            }

            User32.SetForegroundWindow(Handle);
            ResetCursorPos();
        }

        private void FormMouseWheel(object sender, MouseEventArgs e)
        {
            IntPtr fgwnd = GetForegroundWindow();
            User32.SetForegroundWindow(fgwnd);
            SetCursorPos();

            int delta = e.Delta;
            User32.mouse_event(MouseAction.MOUSEEVENTF_WHEEL, 0, 0, delta, UIntPtr.Zero);
            //Show();

            StartMouseScrollTimer();
            StartAliveTimer();
        }

        private void FormMouseLeave(object sender, EventArgs e)
        {
            StartAliveTimer();
        }

        bool IsBrowserWindow(IntPtr hwnd)
        {
            return PersistentWindowProcessor.IsBrowserWindow(hwnd);
        }

        void FormKeyUp(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            //allow shift
            if (e.Control || e.Alt)
                return;

            TopMost = true;

            IntPtr fgwnd = GetForegroundWindow();

            bool return_focus_to_hotkey_window = true;
            if (e.KeyCode == Keys.W && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //kill tab, ctrl + w
                SendKeys.Send("^w");
            }
            else if (e.KeyCode == Keys.T && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //new tab, ctrl + t
                if (e.Shift)
                    SendKeys.Send("^+t"); //open last closed tab
                else
                {
                    SendKeys.Send("^t"); //new tab
                    SendKeys.Send("^l"); //focus in address bar
                    return_focus_to_hotkey_window = false;
                    if (tiny)
                        Visible = false;
                }
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

        void FormKeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;

            //allow shift
            if (e.Control || e.Alt)
                return;

            TopMost = true;

            IntPtr fgwnd = GetForegroundWindow();

            bool return_focus_to_hotkey_window = true;
            if (e.KeyCode == Keys.Tab)
            {
                User32.SetForegroundWindow(fgwnd);
                if (e.Shift)
                {
                    SendKeys.Send("^+{TAB}");
                }
                else
                {
                    SendKeys.Send("^{TAB}");
                }
            }
            else if (e.KeyCode == Keys.Q)
            {
                //TODO
            }
            else if (e.KeyCode == Keys.E)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{HOME}");
            }
            else if (e.KeyCode == Keys.R)
            {
                //reload
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{F5}");
            }
            else if (e.KeyCode == Keys.A && IsBrowserWindow(fgwnd))
            {
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                //address, ctrl L
                SendKeys.Send("^l");
                return_focus_to_hotkey_window = false;
                if (tiny)
                    Visible = false;
            }
            else if (e.KeyCode == Keys.S && IsBrowserWindow(fgwnd))
            {
                // search
                User32.SetForegroundWindow(fgwnd);
                //SetCursorPos();
                SendKeys.Send("^k");
                return_focus_to_hotkey_window = false;
                if (tiny)
                    Visible = false;
            }
            else if (e.KeyCode == Keys.D)
            {
                User32.SetForegroundWindow(fgwnd);
                SendKeys.Send("{END}");
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
                if (tiny)
                    Visible = false;
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
                //toggle zoom (tiny) mode
                ToggleWindowSize();
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
                if (!active)
                {
                    if (init)
                    {
                        ResetHotkeyWindowPos();
                        init = false;
                    }
                    else
                        ResetHotKeyVirtualDesktop();

                    if (tiny)
                        ResetHotkeyWindowPos();

                    User32.SetForegroundWindow(Handle);
                    ResetCursorPos();
                    Visible = true;
                    active = true;
                }
                else
                {
                    Visible = false;
                    active = false;
                }
            }

        }


        public static void BrowserActivate(IntPtr hwnd)
        {
            StartAliveTimer();
        }

        private static void StartAliveTimer(int milliseconds = 500)
        {
            User32.GetCursorPos(out lastCursorPos);

            aliveTimer.Interval = milliseconds;
            aliveTimer.AutoReset = false;
            aliveTimer.Enabled = true;
        }

        private static void StopAliveTimer()
        {
            aliveTimer.Enabled = false;
        }

        private void StartMouseScrollTimer(int milliseconds = 250)
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
            else if (!active)
            {
                ;
            }
            else if (Visible)
            {
                User32.SetForegroundWindow(Handle);
                ResetCursorPos();
            }    
            else if (tiny)
            {
                //Visible = true; keep hiding hotkey window, let OS update cursor shape, and alive timer callback show correct hotkey window position
                User32.SetForegroundWindow(Handle);
                ResetCursorPos();
            }
        }

        private static IntPtr GetCursor()
        {
            User32.CURSORINFO cursor_info;
            cursor_info.cbSize = Marshal.SizeOf(typeof(User32.CURSORINFO));
            User32.GetCursorInfo(out cursor_info);
            return cursor_info.hCursor;
        }

        private void AliveTimerCallBack(Object source, ElapsedEventArgs e)
        {
            if (!active)
                return;

            if (tiny)
            {
                IntPtr fgwnd = GetForegroundWindow();
                if (!PersistentWindowProcessor.IsBrowserWindow(fgwnd))
                    return;

                POINT cursorPos;
                User32.GetCursorPos(out cursorPos);
                IntPtr cursorWnd = User32.WindowFromPoint(cursorPos);
                if (cursorWnd != Handle && cursorWnd != fgwnd && fgwnd != User32.GetAncestor(cursorWnd, User32.GetAncestorRoot))
                {
                    //yield focus
                    //User32.SetForegroundWindow(fgwnd);
                    Visible = false;
                } 
                else if (Math.Abs(cursorPos.X - lastCursorPos.X) > 3 || Math.Abs(cursorPos.Y - lastCursorPos.Y) > 3)
                {
                    //mouse moving, continue monitor
                }
                else
                {
                    IntPtr hCursor = GetCursor();
                    if (hCursor == Cursors.IBeam.Handle)
                    {
                        StartAliveTimer();
                        return;
                    }

                    // let tiny hotkey window follow cursor position
                    ResetHotKeyVirtualDesktop();
                    ResetHotkeyWindowPos();

                    if (hCursor == Cursors.Default.Handle)
                        handCursor = false;

                    if (!Visible)
                        Visible = true;
                    else if (!handCursor)
                        User32.SetForegroundWindow(Handle);

                    if (hCursor == Cursors.Default.Handle)
                    {
                        //arrow cursor
                        BackColor = dfltBackColor;
                        Cursor = Cursors.Default;
                        return;
                    }

                    if (!handCursor)
                    {
                        BackColor = Color.Red;
                        Cursor = new Cursor(hCursor);

                        Left -= 10;
                        handCursor = true;
                    }
                    else
                    {
                        Left -= 10;
                    }
                }

                StartAliveTimer();
            }
            else
                ResetHotKeyVirtualDesktop();
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
