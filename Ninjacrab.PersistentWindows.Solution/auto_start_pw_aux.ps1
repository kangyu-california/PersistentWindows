## Replace with your desired command arguments
$arguments = "-splash=0"

$executablePath = $PSScriptRoot + "\PersistentWindows.exe"

## create registry to run PersistentWindows.exe in high dpi aware mode
$regPath = "HKCU:\Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers"
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}
Set-ItemProperty -Path $regPath -Name $executablePath -Value "~ HIGHDPIAWARE"

## rename the task as you like
$taskName = "StartPersistentWindows" + $env:username
$taskDescription = "This task starts automatically when " + $env:username + " login."

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue

if ($existingTask -ne $null) {
    Write-Host "Remove existing task."
	Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$action = New-ScheduledTaskAction -Execute `"$executablePath`"
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

$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    $principal = New-ScheduledTaskPrincipal -UserId $env:username -RunLevel Highest
} else {
    $principal = New-ScheduledTaskPrincipal -UserId $env:username
    Write-Warning "❌ It is recommended to run as Administrator to avoid Access-Is-Denied failure."
}

$task.Principal = $principal
Set-ScheduledTask -TaskName $taskName -TaskPath $task.TaskPath -Principal $principal