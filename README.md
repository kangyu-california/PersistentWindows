# PersistentWindows
This project addresses a long-standing [issue](https://answers.microsoft.com/en-us/windows/forum/windows_10-hardware/windows-10-multiple-display-windows-are-moved-and/2b9d5a18-45cc-4c50-b16e-fd95dbf27ff3?page=1&auth=1) in Windows 7, 10, and 11, where windows get repositioned after events such as the system waking up, external monitor connections or disconnections, changes in monitor resolution (e.g., exiting full-screen gaming), or during RDP reconnections. The code was forked from [ninjacrab.com/persistent-windows](http://www.ninjacrab.com/persistent-windows/).

## Original Description
> What is PersistentWindows?
>
> A poorly named utility that persists window positions and size when the monitor display count/resolution adjusts 
and restores back to its previous settings.
>
> For those of you with multi-monitors running on a mixture of DisplayPort and any other connection, you can run 
this tool and not have to worry about re-arranging when all is back to normal.

## Key Features
- Keeps track of window position changes, and automatically restores the desktop layout, including the taskbar position, to the last matching monitor setup.
- Supports remote desktop sessions with multiple display configurations.
- Capture windows to disk: saves desktop layout capture to hard drive in liteDB format, so that closed windows can be restored after PC reboot, with virtual desktop observed.
- Capture snapshot to ram: saves desktop layout in memory using one char from [0-9a-z] as the name. The window Z-order is preserved in the snapshot.
- Automatically save window position history as xml files, so that snapshots and auto-restore points will continue to function after PC reboot or PersistentWindows upgrade/restart.
- Webpage commander to improve the efficiency of web browsing for all major web browsers using one-letter commands like in vi editor.
- Efficient window switching between foreground and background dual positions.
- Pause/resume auto restore.
- Automatic upgrade support.
- For more Features and Commands, take a look at the [Quick Help page](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md)

## Installation
- Download the latest PersistentWindows*.zip file from the [Releases](https://github.com/kangyu-california/PersistentWindows/releases) page
- Unzip the file into any directory.
- You can remove the version number from the folder name, because when the program is updated to newer versions, the folder remains the same 
> Note: the program can be run from any directory, but the program saves its data in 
> *C:\Users\\[User]\AppData\Local\PersistentWindows*

### To set up PersistentWindows to automatically start at user login:
This can be done by creating a task in **Task Scheduler**, or by adding a shortcut to the **Startup Folder** (shell:startup).

For PersistentWindows to be able to restore windows with elevated privileges (for tools like Task Manager or Event Viewer), it needs to be run with Administrator privileges.

Choose **one** of the three options:

**Task Scheduler (Windows 10/11)**
* Run *auto_start_pw.bat* file (preferably as administrator) to create a task in the Task Scheduler.
        <img src="https://github.com/kangyu-california/PersistentWindows/assets/59128756/e323086a-8373-4e8a-b439-3c7087550cb0" alt="auto_start_pw as administrator" width="400" />

**Task Scheduler (Windows 7/10/11)**
* Create a pw.bat file in the installation folder with following content
```
  start "" /B "%~dp0PersistentWindows.exe" -splash=0
```
* Launch a DOS window (cmd.exe) with admin privileges, goto (cd) the PW installation folder, and run the following command
```
schtasks /create /sc onlogon /tn "StartPersistentWindows" /f /tr "'%~dp0pw.bat'" /rl HIGHEST
REM Override High DPI Scaling
REG ADD "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /v "%~dp0PersistentWindows.exe" /t REG_SZ /d "~ HIGHDPIAWARE" /f
``` 
**Startup Folder (Windows 7/10/11)**
* Create a shortcut in the startup folder:
  * `Win + R`, type `shell:startup`
  * Create a shortcut to *PersistentWindows.exe* and place it in the Startup folder
* For Administrator Privileges:
  * instead of a shortcut, create a .vb file (you can call it *PersistentWindows as Administrator.vb*) and add this to it:
    ```
    Set objShell = CreateObject("Shell.Application")
    objShell.ShellExecute "C:\path\to\PersistentWindows.exe", "", "", "runas", 1
    ```
  * replace in the script the path to the *PersistentWindows.exe* file (the location where the PersistentWindows folder was saved)

  <br>

  >  Note: It is possible for set shortcuts to be run as administrator, through the shortcut properties menu. However, this doesn’t work when opening the shortcut through the Startup folder, which is why we use this workaround with the .vb script


## Usage Instructions
- Run `PersistentWindows.exe` (preferably as administrator). Note that this app has no main window and its icon is hidden in the System Tray area on the taskbar by default.
- To have the icon always appear on the taskbar, flip on the PersistentWindows item in the taskbar settings.

  <img src="showicon.png" alt="taskbar setting" width="400" />
- Right click the PersistentWindows icon to show the menu, where the capture and restore actions can be selected.
  ![image](https://github.com/kangyu-california/PersistentWindows/assets/59128756/6a196d75-7d86-4bd3-8873-4a4d65cb3c30)

- To restore the taskbar position, avoid moving mouse when the icon turns red.
- When software upgrades are available, a notice will show up in the menu.

## Privacy Statement
- PersistentWindows performs its duty by collecting following information:
  * window position
  * window size
  * window Z-order
  * window caption text
  * window class name   
  * process id and command line of the window
  * Ctrl, Alt, Shift key strokes when clicking or moving a window
  * Ctrl, Alt, Shift key strokes when selecting PersistentWindows menu items
  * key-stroke events when interacting with the PersistentWindows icon on taskbar
  * key-stroke events (only as webpage command shortcut), mouse click/scroll events and cursor position/shape in web browser when webpage commander window is activated (Alt + W)
- The history of keyboard/mouse events is typically erased 1 second after received
- Window information history is kept in ram or on the hard drive in LiteDB file format, waiting to be recalled by auto/manual restore
- PersistentWindows periodically checks the github repository for software version upgrades. This can be disabled in the options menu.
  
## Known Issues
-  PersistentWindows may malfunction on fractionally scaled display (such as 125%, 150% etc), it is strongly suggested to override the high DPI scaling property of PersistentWindows.exe to "Application" via Properties->Compatibility->Change high DPI settings dialog from explorer, user needs to capture windows to disk immediately after relaunching PW w/ the new DPI setting.
![image](https://github.com/kangyu-california/PersistentWindows/assets/59128756/d410aa87-4552-42da-b7a4-e9d7ab1947b1)
- PersistentWindows can get stuck in a "busy" state (with a red icon in the System Tray) during a restore if one of the windows becomes unresponsive. You may find out the culprit window in Task Manager using "Analyze wait chain". The unresponsive app might need an immediate hot-upgrade, or need to be killed to let PersistentWindows proceed

  <img src="https://user-images.githubusercontent.com/59128756/184041561-5389f540-c61a-4ee7-90ff-f9f725ba3682.png" alt="image" width="500"/>
  <img src="https://user-images.githubusercontent.com/59128756/187988981-b2564618-2724-4e1e-a718-cd0786a4251e.png" alt="wait chain" width="500"/>

## Tips To Digest Before Reporting A Bug or Enhancement Request
- PersistentWindows provides a rich set of command line options for customization, check out [Quick Help page](https://www.github.com/kangyu-california/PersistentWindows/blob/master/Help.md) for a complete list of available options. [how to customize command line options](https://github.com/kangyu-california/PersistentWindows/discussions/313)
- The window Z-order can be restored in addition to the two-dimentional layout. This feature is enabled for manual snapshot restore only. To turn on Z-order fix for automatic restore, run PersistentWindows with -fix_zorder=1
- To help me diagnose a bug, please run Event Viewer, locate the "Windows Logs" -> "Application" section, then search for Event ID 9990 and 9999, and copy-paste the content of these events to the new issue report, as shown in the following example
  <img src="https://user-images.githubusercontent.com/59128756/190280503-a96ce57f-a6f0-4aad-9748-221bbb4f9207.png" alt="image" width="800"/>
- If there are too many events to report, click "Filter current log" from the Action panel in Event Viewer, choose all 9990 and 9999 events in last hour, then click "Save Filtered Log File As", select "Text (*.txt)" format, and attach the saved events file to the bug report
  
  ![image](https://github.com/kangyu-california/PersistentWindows/assets/59128756/ce4ee2e7-8662-4eb5-9a49-cbe53d30f911)
  




