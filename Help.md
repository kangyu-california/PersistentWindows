

## PersistentWindows Quick Help
### Command Line Options
  | Command line option | Meaning |
  | --- | --- |
  | -gui=0 | Do not display the PersistentWindows icon on the System Tray. Effectively runs PersistentWindows as a service
  | -splash=0       | No splash window at PersistentWindows startup
  | -legacy_icon    | Switch to the original icon ![pwIcon2small](https://github.com/user-attachments/assets/4827f67a-2ce1-4a83-86da-b4bfa6835026)
  | -silent         | No splash window, no balloon tip hint, no event logging
  | -capture_floating_window=0 | Disable capture floating child window and dialog window position
  | -ignore_process "notepad.exe;foo" | Avoid restoring windows for the processes notepad.exe and foo
  | -debug_process "notepad.exe;foo" | Print the window positioning event logs in Event Viewer for the processes *notepad.exe* and *foo*
  | -foreground_background_dual_position=0 | Turn off dual position switching
  | -webpage_commander_window=0 | Unregister Alt+W hotkey and turn off the webpage commander feature
  | -hotkey "Q" | register Alt + Q as the hotkey to (de)activate webpage commander window, default hotkey is "W" (Alt + W)
  | -ctrl_minimize_to_tray=0 | Turn off ctrl minimize window to notification tray
  | -prompt_session_restore | Ask the user before restoring the window layout upon resuming the last session. This may help reduce the total restore time for remote desktop sessions on slow internet connections.
  | -delay_restart 5 | Restart PersistentWindows in 5 seconds. This option should only be used in case normal start of PersistentWindows fails.
  | -delay_auto_capture 1.0 | Adjust the lag between window move event and auto-capture to 1.0 second, the default lag is 3~4 seconds.
  | *-delay_auto_restore 2.5* | Adjust the lag between monitor on/off event and auto-restore to 2.5 seconds (the default lag is 1 second). This is in case the restore is incomplete or the monitor fails to go to sleep due to the restore starting too early.
  | -redraw_desktop | Redraw the whole desktop after a restore, in case some window workarea is not refreshed
  | -fix_zorder=1   | Preserve the window Z-order for automatic restores. The Z-order of a window indicates the window's position in a stack of overlapping windows.
  | -fix_offscreen_window=0 | Turn off auto correction of off-screen windows
  | -fix_unminimized_window=0 | Turn off auto restore of unminimized windows. Use this switch to avoid undesirable window shifting during window activation, which comes with Event id 9999 : "restore minimized window ...." in event viewer.
  |-auto_restore_new_display_session_from_db=0| Disable window restore from DB upon PC startup or switching display for the first time
  |-auto_restore_existing_window_to_last_capture=1 | Turn on auto restore existing window from last capture upon PW start
  |-auto_restore_new_window_to_last_capture=0 | Turn off auto restore new window to last killed position
  |-pos_match_threshold 80 | Auto correct new window position to last killed position within range of 80 pixels, default is 40
  | ‑auto_restore_missing_windows=1 | When restoring from disk, restore missing windows without prompting the user
  | ‑auto_restore_missing_windows=2 | At startup, automatically restore missing windows from disk. The user will be prompted before restoring each missing window
  | ‑auto_restore_missing_windows=3 | At startup, automatically restore missing windows from disk without prompting the user
  | -invoke_multi_window_process_only_once=0 | Launch an application multiple times when multiple windows of the same process need to be restored from the database.
  | -check_upgrade=0 | Disable the PersistentWindows upgrade check
  | -auto_upgrade=1 | Upgrade PersistentWindows automatically without user interaction
  | -dump_window_position_history=0 | Disable window position history dump
  | -restore_snapshot "0" | restore snapshot 0 and exit. The range of snapshot id is [0-9a-z], as well as "~" or "`", the last two special ids represent the last auto restore. Note the window z-order can not be fully retored using this method, due to lack of capability to do multi-pass restore.
---

### Shortcuts to capture/restore snapshots
  | Snapshot command | Shortcut|
  | --- | --- |
  | Capture snapshot 0 | Double click the PersistentWindows icon
  | Restore snapshot 0 | Click the PersistentWindows icon
  | Capture snapshot X | Double click the PersistentWindows icon, then immediately press key X (X represents a digit [0-9] or a letter [a-z])
  | Restore snapshot X | Click the PersistentWindows icon, then immediately press key X
  | Undo the last snapshot restore | Alt + Click the PersistentWindows icon

### Shortcuts for capture/restore windows on disk
  * To save a named capture to disk, Ctrl + Click the "Capture windows to disk" menu option, then enter a name in the pop-up dialog
  * To restore the named capture from disk, Ctrl + Click the "Restore windows from disk" menu option, then enter the name of the previously saved capture in the dialog
  * To restore capture from a different display config, Shift + Click the "Restore windows from disk" menu option.

---
### Shortcuts to manipulate positions of a window
* Dual Position Switching allows a window to switch between foreground and background mode of different positions and sizes

  * To activate Dual Position Switching: 
    * Ctrl + Move or resize the window.

  * Dual Position Switching functionality:
    * Click the desktop window to bring the foreground window to its previous background position and Z-order.
    * Ctrl + Click the desktop window to bring the foreground window to its previous Z-order while keeping the current location and size.

  * To cancel Dual Position Switching:
    * move or resize the window (without pressing the Ctrl key).
  * To bring a background DPS window to foreground *without* restoring to the previous foreground position:
    * press any of Ctrl/Shift/Alt key when activating the window
* To move (invisible or off-screen) foreground window to the center of main monitor
  * Shift + Click the PersistentWindows icon
* To put the foreground window behind all other windows (similar to the Alt+Esc shortcut provided by the Windows OS, which, however, does not work for windows inside a remote desktop window):
  * Alt + Click the desktop window to bring foreground window to bottom Z-order.
  * Ctrl + Alt + Click the PersistentWindows icon to bring *maximized* foreground window to bottom Z-order.
* To hide a window to the notification area in the taskbar:
  * Ctrl + Click the minimize button
* To close a window and permanently forget it's position history
  * Ctrl + Close the window
* To enable/disable auto restore for non-toplevel windows (such as a child or dialog window):
  * To include a child/dialog window for auto capture/restore, move the window once using the mouse
  * To exclude a window from auto capture/restore, Ctrl + Shift + move the window

### Webpage commander window
* A webpage commander window captures command shortcuts (which is carefully designed to be done single handedly) and translates them to the underlying web browser window, maximizing the efficiency of web browsing.
* Press Alt + W to activate/deactivate the webpage commander window inside any browser window (Chrome, Edge, Firefox, Opera, Brave, Vivaldi etc)
* Once activated, a tiny commander window in blue color is displayed in synchronization with the mouse cursor.
  | Command shortcut| Translation | Meaning|
  | --- | --- | --- |
  | 1-8 | Ctrl + #n | select tab #n
  | 9 | Ctrl + 9 | select the right-most tab
  | TAB | Ctrl + TAB | select the next tab to the right
  | Q | Shift + Ctrl + TAB | select the previous tab to the left
  | W | Ctrl + W | close the tab
  | T | Ctrl + T | new tab
  | R | Ctrl + R | Reload the web page
  | A | Ctrl + L | edit the web Address
  | S | Ctrl + F | Search in current page
  | X (or /) | / | search the web
  | E | Home | scroll to page head
  | D | End | scroll to page end
  | F | Alt + Right | go Forward to the next url
  | B | Alt + Left | go Backward to the previous url
  | G | Ctrl + Shift + A | Goto (select) one tab (for Chrome/Edge/Brave only)
  | V | | goto the last Visited tab
  | C | | Copy(duplicate) the current tab
  | U (or Shift + T) | Ctrl + Shift + T | Undo close tab
  | N | Ctrl + N | New browser window
  | H | Left | scroll left
  | J | Down | scroll down
  | K | Up | scroll up
  | L | Right | scroll right
  | Space (or left click in the commander window) | PgDn | page down
  | P (or right click in the commander window) | PgUp | page up
  | Z | | Zoom in/out the commander window
  | ~ | | toggle the color of commander window
  | Alt + click commander window || send the mouse click to the underlying browser window

### Other features
* To replace the default app icons with customized one:
  * Rename customized .ico (or .png) file as `pwIcon.ico` (or `pwIcon.png`) and copy it to the PersistentWindows program folder, or alternatively to `C:/Users/<YOUR_ID>/AppData/Local/PersistentWindows/`.
  * Copy another ico/png file to the same directory and rename it to `pwIconBusy.*`. This icon is displayed when PersistentWindows is busy restoring windows.
  * Copy yet another ico/png file to the same directory and rename it to `pwIconUpdate.*`. This icon is displayed when a new PersistentWindows release is available.

