

## PersistentWindows Quick Help
### Command Line Options
  | Command line option | Meaning |
  | --- | --- |
  | -gui=0 | Do not display the PersistentWindows icon on the System Tray. Effectively runs PersistentWindows as a service
  | -splash=0       | No splash window at PersistentWindows startup
  | -legacy_icon    | Switch to the original icon (as of 5.49 release)
  | -silent         | No splash window, no balloon tip hint, no event logging
  | -ignore_process "notepad.exe;foo" | Avoid restoring windows for the processes notepad.exe and foo
  | -debug_process "notepad.exe;foo" | Print the window positioning event logs in Event Viewer for the processes *notepad.exe* and *foo*
  | -foreground_background_dual_position=0 | Turn off dual position switching
  | -hotkey_window=0 | Turn off hotkey window for web browsing
  | -ctrl_minimize_to_tray=0 | Turn off ctrl minimize window to notification tray
  | -prompt_session_restore | Ask the user before restoring the window layout upon resuming the last session. This may help reduce the total restore time for remote desktop sessions on slow internet connections.
  | -delay_auto_capture 1.0 | Adjust the lag between window move event and auto-capture to 1.0 second, the default lag is 3~4 seconds.
  | *-delay_auto_restore 2.5* | Adjust the lag between monitor on/off event and auto-restore to 2.5 seconds (the default lag is 1 second). This is in case the restore is incomplete or the monitor fails to go to sleep due to the restore starting too early.
  | -redraw_desktop | Redraw the whole desktop after a restore, in case some window workarea is not refreshed
  | -fix_zorder=1   | Preserve the window Z-order for automatic restores. The Z-order of a window indicates the window's position in a stack of overlapping windows.
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
  | Capture snapshot X | Double click the PersistentWindows icon, then immediately press key X (X represents a digit [0-9] or a letter [a-z])
  | Restore snapshot X | Click the PersistentWindows icon, then immediately press key X
  | Undo the last snapshot restore | Alt-click the PersistentWindows icon

### Shortcuts for capture/restore windows on disk
  * To save a named capture to disk, Ctrl-click the "Capture windows to disk" menu option, then enter a name in the pop-up dialog
  * To restore the named capture from disk, Ctrl-click the "Restore windows from disk" menu option, then enter the name of the previously saved capture in the dialog
  * To restore capture from a different display config, Shift-click the "Restore windows from disk" menu option.

---
### Shortcuts to manipulate positions of a window
* Dual Position Switching allows a window to switch between foreground and background mode of different positions and sizes

  * To activate Dual Position Switching: 
    * move or resize the window while pressing the Ctrl key.

  * Dual Position Switching functionality:
    * Click the desktop window to bring the foreground window to its previous background position and Z-order.
    * Shift-click the desktop window to bring the foreground window to its *second* last background position. This is useful if the previous background position is mis-captured due to invoking start menu or other popups. 
    * Ctrl-click the desktop window to bring the foreground window to its previous Z-order while keeping the current location and size.

  * To cancel Dual Position Switching:
    * move or resize the window without pressing the Ctrl key.
  * To bring a background DPS window to foreground *without* restoring to the previous foreground position:
    * press any of Ctrl/Shift/Alt key when activating the window
* To restore a new window to its last closing position:
  * Ctrl-click the PersistentWindows icon
* To put the foreground window behind all other windows (similar to the Alt+Esc shortcut provided by the Windows OS, which, however, does not work for windows inside a remote desktop window):
  * Alt-click the desktop window.
  * Ctrl-Alt-click the PersistentWindows icon if the foreground window is maximized.
* To hide a window to the notification area in the taskbar:
  * Press the Ctrl key when clicking the minimize button
* To enable/disable auto restore for non-toplevel windows (such as a child or dialog window):
  * To include a child/dialog window for auto capture/restore, move the window once using the mouse
  * To exclude a window from auto capture/restore, press the Ctrl-Shift keys when moving the window

### Hotkey window for efficient web browsing
* A hotkey window is used to translate simplified keyboard/mouse commands and relay them to the underlying web browser window.
* Press Alt + Q to activate/deactivate the hotkey window.
* Once the hotkey window is activated, the following command shortcuts are ready to use
  | Keyboard shortcut| Translation | Meaning|
  | --- | --- | --- |
  | TAB | Ctrl + TAB | next tab
  | Q | Shift + Ctrl + TAB | previous tab
  | W | Ctrl + W | close tab
  | T | Ctrl + T | new tab
  | 1-8 | Ctrl + #n | goto tab #n
  | 9 || goto last tab
  | C | | Copy tab (duplicate tab)
  | R | | Reload web page
  | A | | enter web Address
  | S | | Search in web page
  | E | Home | jump to head of page
  | D | End | jump end of page
  | F | Alt + Right | go Forward to next web page
  | B | Alt + Left | go Backward to previous web page
  | G | | Go to the list of tabs (for google chrome only)
  | P | | page up
  | N | | page down
  | J | | scroll down
  | K | | scroll up
  | Z | | toggle the size of hotkey window, Zoom in/out
  | X | | toggle the color of hotkey window
  | left mouse button click in the blank area of hotkey window || page down
  | right mouse button click in the blank area hotkey window || page up

### Other features
* To replace the default app icon with your customized one:
  * Rename your .ico (or .png) file as `pwIcon.ico` (or `pwIcon.png`) and copy it to the PersistentWindows program folder, or alternatively to `C:/Users/<YOUR_ID>/AppData/Local/PersistentWindows/`.
  * Copy another icon file to the same directory and rename it to `pwIconBusy.*`. This icon is displayed when PersistentWindows is busy restoring windows.

