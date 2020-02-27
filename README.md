# PersistentWindows
The code is forked from http://www.ninjacrab.com/persistent-windows/ with enhancements on more robust remote desktop support and much lower cpu usage.

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
- Keeps track of windows layout, automatically restores last windows layout with matching monitor setup
- Manages different monitor setups automatically (dual monitor setup, single monitor setup etc)
- Remote desktop session also benefits from running this software on target machine, whether monitor setup matches or not.
- Can be run as Windows startup job

# Installation
- Download latest zip file from https://github.com/kangyu-california/PersistentWindows/releases
- Unzip the file into any directory, do NOT choose C:\Program Files\ unless you want to run the program with admin privilege.
- Optionally create a shortcut to PersistentWindows.exe in C:\Users<your user>\AppData\Roaming\Microsoft\Windows\Start Menu\Programs\Startup to automatically start the program when computer powers up.

# Use instructions
- Run PersistentWindows.exe, a splash window will pop up, indicating the program has started successfully.
- PersistentWindows will automatically save window positions and restore them when monitor setup is changed or when user login to previous session.
- There will be an icon in the system tray area on task bar. Right click the icon and select "ShutDown" to exit the program. Please ignore other menu options which is intended for DEBUG purpose only. 
