

## PersistentWindows Quick Help
### Command Line Options
  | Command line option | Meaning |
  | --- | --- |
  | -delay_start 10 | Delay the application startup by 10 seconds. Useful if PersistentWindows autostart fails to show the icon because of Windows Update.
  | -redirect_appdata | Use the current directory instead of the User AppData directory to store the database file. This option is also useful for launching multiple PersistentWindows instances.
  | -gui=0 | Do not display the PersistentWindows icon on the System Tray. Effectively runs PersistentWindows as a service
  | -splash=0       | No splash window at PersistentWindows startup
  | -notification=1 | Turn on balloon tip and sound notification when restoring windows
  | -silent         | No splash window, no balloon tip hint, no event logging
  | -ignore_process "notepad.exe;foo" | Avoid restoring windows for the processes notepad.exe and foo
  | -debug_process "notepad.exe;foo" | Print window positioning event logs for the processes notepad.exe and foo in event viewer
  | -foreground_background_dual_position=0 | turn off dual position switching
  | -prompt_session_restore | Ask the user before restoring the window layout upon resuming the last session. This may help to reduce the total restore time for remote desktop sessions on slow internet connections.
  | -delay_auto_capture 1.0 | Adjust the lag between window move event and auto-capture to 1.0 second, the default lag is 3~4 seconds.
  | *-delay_auto_restore 2.5* | Adjust the lag between monitor on/off event and auto-restore to 2.5 seconds (the default lag is 1 second), in case restore is incomplete or monitor fails to go to sleep due to restore starts too early.
  | -redraw_desktop | Redraw the whole desktop after a restore in case some window workarea is not refreshed
  | -fix_zorder=1   | Preserve window Z-order for automatic restore. The Z-order of a window indicates the window's position in a stack of overlapping windows.
  | -fix_offscreen_window=0 | Turn off auto correction of off-screen windows
  | -fix_unminimized_window=0 | Turn off auto restore of unminimized windows. Use this switch to avoid undesirable window shifting during window activation, which comes with Event id 9999 : "restore minimized window ...." in event viewer.
  | ‑auto_restore_missing_windows=1 | When restoring from disk, restore missing windows without prompting the user
  | ‑auto_restore_missing_windows=2 | At startup, automatically restore missing windows from disk. The user will be prompted before restoring each missing window
  | ‑auto_restore_missing_windows=3 | At startup, automatically restore missing windows from disk without prompting the user
  | -invoke_multi_window_process_only_once=0 | Launch an application multiple times when multiple windows of the same process need to be restored from the database.
  | -check_upgrade=0 | Disable the PersistentWindows upgrade check
  | -auto_upgrade=1 | Upgrade PersistentWindows automatically without user interaction

---

### Shortcuts to capture/restore snapshots
  | Snapshot command | Shortcut|
  | --- | --- |
  | Capture snapshot 0 | Double click the PersistentWindows icon
  | Restore snapshot 0 | Click the PersistentWindows icon
  | Capture snapshot X | Double click the PersistentWindows icon,  then immediately press key X (X represents a digit [0-9] or a letter [a-z])
  | Restore snapshot X | Click the PersistentWindows icon, then immediately press key X
  | Undo the last snapshot restore | Alt + click the PersistentWindows icon

### Shortcuts for capture/restore windows on disk
  * To save a named capture to disk, Ctrl click the "Capture windows to disk" menu option, then enter a name in the pop-up dialog
  * To restore the named capture from disk, Ctrl click the "Restore windows from disk" menu option, then enter the name of the previously saved capture in the dialog
  * To restore capture from a different display config, Shift click "Restore windows from disk" menu.

---
### Shortcuts to manipulate positions of one window
* Switching a window between foreground and background mode with potentially different positions and sizes, aka Dual Position Switching.
    * Click the desktop window to bring the foreground window to its previous background position and z-order.
    * Shift click the desktop window to bring the foreground window to its *second* last background position. This is useful if the previous background position is mis-captured due to invoking start menu or other popups. 
    * Ctrl click the desktop window to bring the foreground window to its previous z-order while keeping the current location and size.
  * To bring a background window to foreground *without* restoring to previous foreground position.
    * press any of Ctrl/Shift/Alt key when activating the window.
* To restore a new window to its last closing position
  * Ctrl click the PW icon
* To put the foreground window behind all other windows (similar to the Alt+Esc shortcut provided by Windows OS, which however does not work for windows inside a remote desktop window)
  * Alt click the desktop window.
  * Ctrl Alt click the PW icon if the foreground window is maximized.
* To momentarily suppress window capture, for example during start menu invocation
  * press Ctrl Alt keys
* To enable/disable auto restore for non-toplevel window (such as a child or dialog window)
  * To include a child/dialog window for auto capture/restore, move the window once using mouse
  * To exclude a window from auto capture/restore, press Ctrl Shift keys when moving the window

### Other features
* Replace the default app icon with your customized one
  * Save your icon file as `pwIcon.ico` and copy it to `C:/Users/\<YOUR_ID>/AppData/Local/PersistentWindows/`, or copy it to the directory where you are invoking PersistentWindows from using the `-redirect_appdata` command argument.
  * Save the second icon file as `pwIconBusy.ico` in the same directory. This icon is displayed when PersistentWindows is busy restoring windows.

