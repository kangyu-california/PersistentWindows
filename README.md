# PersistentWindows
The code is forked from http://www.ninjacrab.com/persistent-windows/ to solve a long-standing issue on Windows 7/10.
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
- Keeps track of window position change in real time (including taskbar window), and automatically restores window layout to last matching monitor setup.
- Support remote desktop session with multiple display configurations.
- Support desktop layout captures (up to 8192) on hard drive in liteDB format, so that closed windows can be restored after reboot.
- Take desktop layout snapshots in memory (max 36 for each display configuration), window z-order is preserved in snapshot. This feature can be used as an alternative to virtual desktops on Windows 10.
- Pause/resume auto restore.
- Support automatic upgrade.

# Installation
- Download the latest PersistentWindows*.zip file from https://github.com/kangyu-california/PersistentWindows/releases
- Unzip the file into any directory
- It is highly recommended to create a task in Task Scheduler to automatically start PersistentWindows triggered by user log on.
  - **Make sure to select "Run only when user is logged on", and disable "Stop the task if it runs longer than (3 days)" in task property settings.**

  - **Specify command option "-delay_start 10" in Edit Action dialog to avoid startup failure after Windows upgrade**

# Use instructions
- Run PersistentWindows.exe as normal user, a splash window will pop up, indicating the program has started successfully. 
- PersistentWindows minimizes itself as an icon in the systray area on task bar.
- In taskbar settings, turn on PW to let the icon always appear on taskbar, the icon will change to red color during restore, providing visual hint to user to avoid maneuver window
  ![taskbar setting](showicon.png)
- To help restoring taskbar window, make sure taskbar is unlocked (i.e. it can be dragged using mouse), also please do NOT move mouse during window recovery.
![image](https://user-images.githubusercontent.com/59128756/116501499-c24e3280-a865-11eb-9bc9-78aa545a239c.png)
- Do monthly check on Help menu which is renamed when new PW upgrade is available.

# Known issues
 - **A PC reboot triggered by Windows feature/security upgrade has recently caused PW icon to disappear, please add PW command option "-delay_start 10" in task scheduler and reboot again**
- Some applications (such as Task Manager, Event Viewer etc) require running PersistentWindows with admin privilege to fully recover window layout. There is an option to "Run with highest priviledges" when you create auto start PersistentWindows task in Task Scheduler.
- **PW may stuck at busy status when restoring previous layout if your running Microsoft Visual Studio Code window has a pending update mark. The root cause is unknown, simply proceed to update VScode will let PW resume to normal again.** 
- **Some Windows built-in apps (such as Sticky Notes) can not be simply launched (when restore from disk), user need to manually launch them**

# Tips to digest before reporting a bug
- Window z-order can be restored in addition to two-dementional layout. This feature is is enabled for snapshot restore only.
- To help me diagnose a bug, please run Event Viewer, locate to "Windows Logs" -> "Application" section, then search for Event ID 9990 and 9999, and copy paste the content of these events to new issue report.

