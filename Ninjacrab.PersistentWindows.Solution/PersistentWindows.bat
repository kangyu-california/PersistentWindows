REG ADD "HKCU\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" /V "%~dp0PersistentWindows.exe" /T REG_SZ /D ~HIGHDPIAWARE /F
start "" /B "%~dp0PersistentWindows.exe" %*