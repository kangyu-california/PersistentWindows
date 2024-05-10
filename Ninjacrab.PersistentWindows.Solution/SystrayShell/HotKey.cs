using System;
using System.Threading;
using System.Windows.Forms;

using PersistentWindows.Common;
using PersistentWindows.Common.WinApiBridge;

namespace PersistentWindows.SystrayShell
{
    public class HotKeyForm : Form
    {
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

                Program.HideRestoreTip(false); //hide icon
                Program.HideRestoreTip(); //show icon
                hkwin.HotKeyPressed(from_menu : false);
                return;
            }
            else if (m.Msg == 0x0010 || m.Msg == 0x0002)
            {
                r = User32.UnregisterHotKey(this.Handle, 0);
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

