# PersistentWindows
The code is forked from [ninjacrab.com/persistent-windows](http://www.ninjacrab.com/persistent-windows/) to solve a long-standing [issue](https://answers.microsoft.com/en-us/windows/forum/windows_10-hardware/windows-10-multiple-display-windows-are-moved-and/2b9d5a18-45cc-4c50-b16e-fd95dbf27ff3?page=1&auth=1) on Windows which causes the windows to get repositioned.

## Original Description
> What is PersistentWindows?
>
> A poorly named utility that persists window positions and size when the monitor display count/resolution adjusts 
and restores back to its previous settings.
>
> For those of you with multi-monitors running on a mixture of DisplayPort and any other connection, you can run 
this tool and not have to worry about re-arranging when all is back to normal.

## Key Features
- Keeps track of changes in window positions, and automatically restores the window layout to the last matching monitor setup. It also restores the taskbar positioning.
- Supports remote desktop sessions with multiple display configurations.
- Capture windows to disk: saves desktop layout captures (>= 32) to the hard drive in liteDB format, so that closed windows can be restored after a reboot.
- Capture snapshot: saves desktop layout snapshots in memory (max 36 for each display configuration). The window z-order is preserved in the snapshot. This feature can be used as an alternative to virtual desktops on Windows 10.
- Auto Restore can be paused/resumed as desired.
- Supports automatic upgrades.
- For more Features and Commands, take a look at the [Quick Help page](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md)

## Installation
- Download the latest PersistentWindows*.zip file from the [Releases](https://github.com/kangyu-california/PersistentWindows/releases) page
- Unzip the file into any directory.
    * Note, the program can be run from any directory, but the program saves its data in `C:\Users\[User]\AppData\Local\PersistentWindows`
- It is highly recommended to create a task in Task Scheduler to automatically start PersistentWindows when a user logs on.
  - Make sure to select `Run only when user is logged on`, and disable `Stop the task if it runs longer than (3 days)` in the task property settings.
  - Specify the command option `-delay_start 10` in the Edit Action dialog to avoid startup failures after Windows upgrades

## Usage Instructions
- Run PersistentWindows.exe. A pop up will show up indicating the program started successfully. It will start minimized as an icon in the System Tray area on the taskbar.
- Right click the icon to show the program menu, where the capture and restore options can be selected
- To have the icon always appear on the taskbar, turn on PersistentWindows in the taskbar settings. 

  <img src="showicon.png" alt="taskbar setting" width="250" />

- The icon will turn red during a restore. Avoid moving the windows until it changes back to green
- To be able to restore the taskbar:
  - Make sure the taskbar is unlocked (i.e. it can be dragged using  the mouse).

    <img src="https://user-images.githubusercontent.com/59128756/116501499-c24e3280-a865-11eb-9bc9-78aa545a239c.png" alt="image" width="350"/>

   - Also, please do NOT move the mouse during recovery.
- When software upgrades are available, a notice will show up in the menu.

## Known Issues
 - **A computer reboot triggered by Windows Update has recently caused the PersistentWindows icon to disappear. To fix it, please add the command option "-delay_start 10" in Task Scheduler and reboot**
- Some applications (such as Task Manager, Event Viewer etc) require running PersistentWindows with administrator privileges to fully recover the window layout. There is an option to "Run with highest privileges" when you add PersistentWindows in Task Scheduler.
- **PersistentWindows can get stuck in a "busy" state (with a red icon in the System Tray) during a restore if one of the windows becomes unresponsive. You may find out the culprit window in Task Manager using "Analyze wait chain". The unresponsive app might need an immediate hot-upgrade, or need to be killed to let PersistentWindows proceed**

  <img src="https://user-images.githubusercontent.com/59128756/184041561-5389f540-c61a-4ee7-90ff-f9f725ba3682.png" alt="image" width="350"/>

- **Some Windows built-in apps (such as Sticky Notes) cannot be easily launched when restoring from disk. The user needs to manually launch them**

## Tips To Digest Before Reporting A Bug
- The window z-order can be restored in addition to the two-dimentional layout. This feature is enabled for snapshot restore only.
- To help me diagnose a bug, please run Event Viewer, locate the "Windows Logs" -> "Application" section, then search for Event ID 9990 and 9999, and copy-paste the content of these events to the new issue report.

