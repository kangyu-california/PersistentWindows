$executablePath = $PSScriptRoot + "\PersistentWindows.exe"

## create registry to run PersistentWindows.exe in high dpi aware mode
Remove-ItemProperty -Path "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" -Name $executablePath

## rename the task as you like
$taskName = "StartPersistentWindows" + $env:username
$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
    Write-Host "Remove existing task."
	Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$app_path = $env:LOCALAPPDATA + "\PersistentWindows"
Remove-Item -Path $app_path -Recurse -Force