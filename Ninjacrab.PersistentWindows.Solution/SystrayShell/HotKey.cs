using System;
using System.Threading;
using System.Windows.Forms;

using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.SystrayShell
{
    public class HotKeyForm : Form
    {
        static HotKeyWindow hkwin = new HotKeyWindow();

        public static void Start()
        {
            Thread messageLoop = new Thread(() =>
            {
                Application.Run(new HotKeyForm());
            })
            {
                Name = "MessageLoopThread",
                IsBackground = true
            };

            messageLoop.Start();
        }
        public HotKeyForm()
        {
            //InitializeComponent();

            var r = User32.RegisterHotKey(this.Handle, 0, (int)User32.KeyModifier.Alt, 0x51);       // Register Alt + Q 
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312)
            {
                /* Note that the three lines below are not needed if you only want to register one hotkey.
                 * The below lines are useful in case you want to register multiple keys, which you can use a switch with the id as argument, or if you want to know which key/modifier was pressed for some particular reason. */

                Keys key = (Keys)(((int)m.LParam >> 16) & 0xFFFF);                  // The key of the hotkey that was pressed.
                User32.KeyModifier modifier = (User32.KeyModifier)((int)m.LParam & 0xFFFF);       // The modifier of the hotkey that was pressed.
                int id = m.WParam.ToInt32();                                        // The id of the hotkey that was pressed.

                if (!User32.IsWindowVisible(hkwin.Handle))
                    hkwin.Show();
                else
                    User32.ShowWindow(hkwin.Handle, (int)ShowWindowCommands.Hide);

                return;
            }

            base.WndProc(ref m);
        }

        protected override void SetVisibleCore(bool value)
        {
            // Ensure the window never becomes visible
            base.SetVisibleCore(false);
        }

        public void Dispose()
        {
            User32.UnregisterHotKey(this.Handle, 0);
        }
    }
}

