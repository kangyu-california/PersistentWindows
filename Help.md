
## PersistentWindows quick help
* PW command line options
  | Command line option | Meaning |
  | --- | --- |
  | -delay_start \<seconds\> | Delay application startup by specified seconds, useful if PW autostart fails to show icon due to Windows upgrade.
  | -redirect_appdata | Use current dir instead of user appdata dir to store database file, this option is also useful for launching multiple PW instances.
  | -splash=0       | No splash window at PW startup
  | -ignore_process "notepad.exe;foo" | avoid restore window for process notepad and foo
  | -prompt_session_restore | Upon resuming last session, ask user before restore window layout, this may help to reduce total restore time for remote desktop session on slow internet connection.
  | -notification=1 | Turn on balloon tip and sound notification when restoring windows
  | -halt_restore \<seconds\> | Delay auto restore by specified seconds in case monitor fails to go to sleep due to fast off-on-off switching.
  | -redraw_desktop | redraw whole desktop windows after restore
  | -fix_zorder=1   | Turn on z-order fix for automatic restore
  | -fix_offscreen_window=0 | Turn off auto correction of off-screen window
  | -fix_unminimized_window=0 | Turn off auto restore of unminimized window. Use this switch to avoid undesirable window shifting during window activation, which comes with Event id 9999 : "restore minimized window ...." in event viewer.
  | ‑auto_restore_missing_windows=1 | Restore missing windows from disk without prompting user
  | ‑auto_restore_missing_windows=2 | Automatic restore missing windows from disk at startup, user will be prompted before restore each missing window
  | ‑auto_restore_missing_windows=3 | Automatic restore missing windows from disk at startup without prompting user
  | -invoke_multi_window_process_only_once=0 | Launch an application multiple times to restore multiple windows of the same process from DB.
  | -check_upgrade=0 | Disable PW upgrade check from beginning
  | -auto_upgrade=1 | Upgrade PW automatically without user interaction

---

* Command shortcut to capture/restore snapshot
  | Snapshot command | Shortcut|
  | --- | --- |
  | Capture snapshot 0 | Double click PW icon
  | Restore snapshot 0 | Click PW icon
  | Capture snapshot X | Double click PW icon then immediately press key X (X represents a digit [0-9] or a letter [a-z])
  | Restore snapshot X | Click PW icon then immediately press key X
  | Undo last snapshot restore | Alt click PW icon

---

* Run PW with customized icon
  * Rename your customized icon file as pwIcon.ico and copy it to 'C:/Users/\<YOUR_ID>/AppData/Local/PersistentWindows/', or copy it to current directory if running (second inst of) PW with -redirect_appdata switch from command console.
  * Add second icon file, rename it to pwIconBusy.ico, this icon is displayed when PW is busy restoring windows.

* Manipulate window z-order
  * Bring a window to top z-order by clear the topmost flag of obstructive window
    * Click on other window first to defocus, then Ctrl click the window (or the corresponding icon on taskbar) that you want to bring to top.
  * Send remote desktop or vncviewer window to bottom z-order (because Alt+Esc hotkey does not work for them)
    * Defocus rdp window first, then hold Ctrl+Win keys and click the rdp window

* Enable/Disable auto restore for any (child or dialog) window
  * To add such window for auto capture/restore, move the window using mouse
  * To remove such window from auto capture/restore, hold Ctrl+Shift keys then move the window
```
