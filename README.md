# PersistentWindows
The code is forked from http://www.ninjacrab.com/persistent-windows/ with massive enhancements to achieve more reliable user experience.

It seems to be a perfect solution to this unsolved Windows problem since Windows 7 era
https://answers.microsoft.com/en-us/windows/forum/windows_10-hardware/windows-10-multiple-display-windows-are-moved-and/2b9d5a18-45cc-4c50-b16e-fd95dbf27ff3?page=1&auth=1


# Original description
```
What is PersistentWindows?
A poorly named utility that persists window positions and size when the monitor display count/resolution adjusts 
and restores back to it’s previous settings.

For those of you with multi-monitors running on a mixture of DisplayPort and any other connection, you can run 
this tool and not have to worry about re-arranging when all is back to normal.

```
# Key features 
- Keeps track of window position change in real time, and automatically restores window layout to last matching monitor setup.
- Support remote desktop session with multiple virtual monitor resolutions.
- Can be auto started as a task or startup menu item.
- Support manual save/restore window position to/from persistent database on hard drive, making it possible to revert unintended or temporary window moves, or restore closed windows even after reboot.
- Support manual pause/resume auto restore.
- **Support snapshot capture/restore (since version 5.x)**

# Installation
- Download the latest PersistentWindows*.zip file from https://github.com/kangyu-california/PersistentWindows/releases
- Unzip the file into any directory
- It is highly recommended to create a task in Task Scheduler to automatically start PersistentWindows triggered by user log on.
  **Make sure to disable "Stop the task if it runs longer than (3 days)" in task property settings.**
  
- Alternatively for users who prefer to auto start PersistentWindows using startup menu, this can be achieved by creating a shortcut to PersistentWindows.exe in C:\Users\\<your_user_id>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup. But this method of auto start may not work as expected for slow computers (see issue #34), it is recommended to delay start by specifying -delay_start \<seconds\> on the command line, 30 seconds seems to be a safe bet.

# Use instructions
- Run PersistentWindows.exe as normal user, a splash window will pop up, indicating the program has started successfully. 
- PersistentWindows minimizes itself as an icon in the systray area on task bar.
- To save current window layout to persistent storage, right click the icon and select "Capture windows to disk" 
- To restore saved window layout from persistent storage, or to recover closed windows after reboot, right click the icon and select "Restore windows from disk"
- To help restoring taskbar window, make sure taskbar is unlocked (i.e. it can be dragged using mouse), also please do NOT move mouse during window recovery.
- To pause PersistentWindows, select menu "Pause auto restore"; To resume PersistentWindows, select menu "Resume auto restore", and window layout will be restored to the moment when pause is executed.
- **Double click PersistentWindows icon to take a snapshot of current window layout, the snapshot is saved in memory instead of db file. To restore the snapshot, just click PW icon. To undo restore snapshot, hold ALT key pressed and click PW icon.** The snapshot feature can be used as a convenient alternative to virtual desktop of Windows 10.

# Tips to digest before reporting a bug
- Some applications (such as Task Manager, Event Viewer etc) require running PersistentWindows with admin privilege to fully recover window layout. There is an option to "Run with highest priviledges" when you create auto start PersistentWindows task in Task Scheduler.
- Starting from release 4.26, there is an experimental feature to automatically restore window z-order in addition to two-dementional layout. This feature is disabled by default. To turn on this feature, run PersistentWindows.exe -fix_zorder
- **Restore snapshot automatically turns on zorder fix (just for snapshot recovery), which occasionally causes some window to become sticky at top z-order. The root cause of the issue is still unknown, however release 5.2 provides a remedy to fix this problem: Click on the sticky topmost window first, then hold CTRL key pressed and click on the window (or minimized icon on taskbar) you want to bring to top.**
- To help me diagnose a bug, please run Event Viewer, locate to "Windows Logs" -> "Application" section, then search for Event ID 9990 and 9999, and copy paste the content of these events to new issue report.

