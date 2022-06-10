# PersistentWindows
The code is forked from [ninjacrab.com/persistent-windows](http://www.ninjacrab.com/persistent-windows/) to solve a long-standing [issue](https://answers.microsoft.com/en-us/windows/forum/windows_10-hardware/windows-10-multiple-display-windows-are-moved-and/2b9d5a18-45cc-4c50-b16e-fd95dbf27ff3?page=1&auth=1) on Windows 7/10.

## Original Description
> What is PersistentWindows?
>
> A poorly named utility that persists window positions and size when the monitor display count/resolution adjusts 
and restores back to it’s previous settings.
> 
> For those of you with multi-monitors running on a mixture of DisplayPort and any other connection, you can run 
this tool and not have to worry about re-arranging when all is back to normal.

## Key Features 
- Keeps track of window position change in real time (including taskbar window), and automatically restores window layout to last matching monitor setup.
- Support remote desktop session with multiple display configurations.
- Support desktop layout captures (>= 32) on hard drive in liteDB format, so that closed windows can be restored after reboot.
- Take desktop layout snapshots in memory (max 36 for each display configuration), window z-order is preserved in snapshot. This feature can be used as an alternative to virtual desktops on Windows 10.
- Pause/resume auto restore.
- Support automatic upgrade.
- For more Features and Commands, take a look at the [Quick Help page](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md)

## Installation
- Download the latest PersistentWindows*.zip file from the [Releases](https://github.com/kangyu-california/PersistentWindows/releases) page
- Unzip the file into any directory
- It is highly recommended to create a task in Task Scheduler to automatically start PersistentWindows triggered by user log on.
  - **Make sure to select "Run only when user is logged on", and disable "Stop the task if it runs longer than (3 days)" in task property settings.**

  - **Specify command option "-delay_start 10" in Edit Action dialog to avoid startup failure after Windows upgrade**

## Use Instructions
- Run PersistentWindows.exe as normal user, a splash window will pop up, indicating the program has started successfully. 
- PersistentWindows minimizes itself as an icon in the systray area on task bar.
- In taskbar settings, turn on PW to let the icon always appear on taskbar, the icon will change to red color during restore, providing visual hint to user to avoid maneuver window

  <img src="showicon.png" alt="taskbar setting" width="250" />

- To help restoring taskbar window, make sure taskbar is unlocked (i.e. it can be dragged using mouse), also please do NOT move mouse during window recovery.

  <img src="https://user-images.githubusercontent.com/59128756/116501499-c24e3280-a865-11eb-9bc9-78aa545a239c.png" alt="image" width="350"/>

- Check menu every month for software upgrade notice.

## Known Issues
 - **A PC reboot triggered by Windows feature/security upgrade has recently caused PW icon to disappear, please add PW command option "-delay_start 10" in task scheduler and reboot again**
- Some applications (such as Task Manager, Event Viewer etc) require running PersistentWindows with admin privilege to fully recover window layout. There is an option to "Run with highest priviledges" when you create auto start PersistentWindows task in Task Scheduler.
- **PW may stuck at busy status during restore if one of the windows becomes unresponsive. You may find out the culprit window in Task Manager using "analyze wait chain". The unresponsive app might need a immediate hot-upgrade, or need be killed to let PW proceed**

  <img src="https://user-images.githubusercontent.com/59128756/184041561-5389f540-c61a-4ee7-90ff-f9f725ba3682.png" alt="image" width="350"/>

- **Some Windows built-in apps (such as Sticky Notes) can not be simply launched (when restore from disk), user need to manually launch them**

## Tips To Digest Before Reporting A Bug
- Window z-order can be restored in addition to two-dementional layout. This feature is enabled for snapshot restore only.
- To help me diagnose a bug, please run Event Viewer, locate to "Windows Logs" -> "Application" section, then search for Event ID 9990 and 9999, and copy paste the content of these events to new issue report.

