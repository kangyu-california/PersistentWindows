using System;
using System.Threading;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;

using PersistentWindows.Common;
using PersistentWindows.Common.WinApiBridge;
using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.SystrayShell
{
    public class HotKeyForm : Form
    {
        static bool init = true;
        static HotKeyWindow hkwin = null;
        static Thread messageLoop;

        public static void Start(uint hotkey)
        {
            messageLoop = new Thread(() =>
            {
                hkwin = new HotKeyWindow(hotkey);
                Application.Run(new HotKeyForm(hotkey));
            })
            {
                Name = "MessageLoopThread",
                IsBackground = true
            };

            messageLoop.Start();
        }

        public static void Stop()
        {
            try
            {
                if (messageLoop.IsAlive)
                    messageLoop.Abort();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }

        public HotKeyForm(uint hotkey)
        {
            //InitializeComponent();
            var r = User32.RegisterHotKey(this.Handle, 0, (int)User32.KeyModifier.Alt, hotkey); // Register Alt + W 
        }

        protected override void WndProc(ref Message m)
        {
            bool r;

            if (m.Msg == 0x0312)
            {
                /* Note that the three lines below are not needed if you only want to register one hotkey.
                 * The below lines are useful in case you want to register multiple keys, which you can use a switch with the id as argument, or if you want to know which key/modifier was pressed for some particular reason. */

                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                User32.KeyModifier modifier = (User32.KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.

                IntPtr fgWnd = PersistentWindowProcessor.GetForegroundWindow(strict : true);
                hkwin.HotKeyPressed(from_menu : false);
                if (PersistentWindowProcessor.IsBrowserWindow(fgWnd))
                {
                    Program.HideRestoreTip(false); //hide icon
                    Program.HideRestoreTip(); //show icon

                    if (init)
                    {
                        init = false;
                        string webpage_commander_notification = Path.Combine(Program.AppdataFolder, "webpage_commander_notification");
                        if (File.Exists(webpage_commander_notification))
                        {
                            Program.systrayForm.notifyIconMain.ShowBalloonTip(8000, "webpage commander is invoked via hotkey", "Press the hotkey (Alt + W) again to revoke", ToolTipIcon.Info);
                        }
                        else
                        {
                            try
                            {
                                File.Create(webpage_commander_notification);

                                uint processId;
                                User32.GetWindowThreadProcessId(fgWnd, out processId);
                                string procPath = PersistentWindowProcessor.GetProcExePath(processId);
                                Process.Start(procPath, Program.ProjectUrl + "/blob/master/webpage_commander.md");
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex.ToString());
                                Program.systrayForm.notifyIconMain.ShowBalloonTip(8000, "webpage commander is invoked via hotkey", "Press the hotkey (Alt + W) again to revoke", ToolTipIcon.Info);
                                Process.Start(Program.ProjectUrl + "/blob/master/webpage_commander.md");
                            }
                        }
                    }
                }

                return;
            }
            else if (m.Msg == 0x0010 || m.Msg == 0x0002)
            {
                r = User32.UnregisterHotKey(this.Handle, 0);
                Log.Event($"unregister hotkey {r}");
            }

            base.WndProc(ref m);
        }

        public static void InvokeFromMenu()
        {
            Program.HideRestoreTip(false); //hide icon
            Program.HideRestoreTip(); //show icon
            hkwin.HotKeyPressed(from_menu: true);
        }
        
        protected override void SetVisibleCore(bool value)
        {
            // Ensure the window never becomes visible
            base.SetVisibleCore(false);
        }

#region IDisposable
        ~HotKeyForm()
        {
            if (messageLoop.IsAlive)
                messageLoop.Abort();
        }
#endregion

    }
}

