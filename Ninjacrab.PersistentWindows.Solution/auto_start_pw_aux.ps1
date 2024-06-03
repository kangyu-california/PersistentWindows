## Replace with your desired command arguments
$arguments = "-splash=0"

$executablePath = $PSScriptRoot + "\PersistentWindows.exe"

## create registry to run PersistentWindows.exe in high dpi aware mode
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers" -Name $executablePath -Value "~ HIGHDPIAWARE"

## rename the task as you like
$taskName = "StartPersistentWindows" + $env:username
$taskDescription = "This task starts automatically when " + $env:username + " login."

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
    Write-Host "Remove existing task."
	Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute $executablePath
$trigger = New-ScheduledTaskTrigger -AtLogOn -User $env:username

## set PW process priority to below normal
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -Priority 8
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Description $taskDescription

$task = Get-ScheduledTask -TaskName $taskName
$taskSettings = $task.Settings
$taskSettings.ExecutionTimeLimit = "PT0S" # disable time limit
Set-ScheduledTask -TaskName $taskName -Settings $taskSettings

$task.Actions[0].Arguments = $arguments
Set-ScheduledTask -TaskName $taskName -TaskPath $task.TaskPath -Action $task.Actions

## Set the task to run with highest privileges
$principal = New-ScheduledTaskPrincipal -UserId $env:username  -RunLevel Highest
$task.Principal = $principal
Set-ScheduledTask -TaskName $taskName -TaskPath $task.TaskPath -Principal $principal