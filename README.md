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
- Keeps track of window position change in real time for each monitor setup, and automatically restores window layout with matching monitor setup.
- Support remote desktop session(s) with different virtual monitor resolution(s) as well. 
- Can be run as Windows startup job.
- Starting from V4.0, window position can be manually saved to persistent database on disk, making it possible to revert window moves by user, or even restore closed windows after reboot.

# Installation
- Download the latest PersistentWindows*.zip file from https://github.com/kangyu-california/PersistentWindows/releases
- Unzip the file into any directory
- Optionally create a shortcut to PersistentWindows.exe in C:\Users\\<your_user_id>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup to automatically start the program when computer powers up.

# Use instructions
- Run PersistentWindows.exe as normal user, a splash window will pop up, indicating the program has started successfully.
- Some applications do require admin privilege to move window, such as TaskMgr, WSL console etc. 
- The program is then minimized to an icon in the systray area on task bar.
- To save current display session to persistent storage, or if you need to capture taskbar position change, right click the icon and select "Capture windows to disk" 
- To recover window layout from persistent storage, or to recover last saved session after computer reboot, right click the icon and select "Restore windows from disk"
- To help restoring taskbar window, make sure taskbar is unlocked (i.e. it can be dragged using mouse), also please do NOT move mouse during window recovery.
