
# Quick help for PW version 5.6
* How to use snapshot feature
```
Capture snapshot 0 : double click PW icon
Restore snapshot 0 : click PW icon

Capture snapshot N : Shift click PW icon N times (N = 1, 2, 3)
Restore snapshot N :  Ctrl click PW icon N times
```

* PW command line options
  * -splash=0       : No splash window at PW startup
  * -notification=1 : Turn on balloon tip and sound notification when restoring windows
  * -delay_start \<seconds\> : Delay PW invoked from auto startup memu by specified seconds
  * -redraw_desktop : redraw whole desktop windows after restore
  * -fix_zorder=1   : Turn on z-order fix for automatic restore
  * -redirect_appdata : Use current dir instead of user appdata dir to store database file, this option allows run second PW instance.
  * -check_upgrade=0 : Disable version upgrade check from beginning 

```