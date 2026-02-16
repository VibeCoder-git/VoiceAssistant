$ErrorActionPreference = "Stop"

$AppName = "JarvisVoiceAssistant"
$ExePath = Join-Path $PSScriptRoot "VoiceAssistant.App.exe"

if (-not (Test-Path $ExePath)) {
    Write-Host "Error: Executable not found at $ExePath" -ForegroundColor Red
    exit 1
}

$Action = New-ScheduledTaskAction -Execute $ExePath -WorkingDirectory $PSScriptRoot
$Trigger = New-ScheduledTaskTrigger -AtLogon
$Principal = New-ScheduledTaskPrincipal -UserId $env:USERNAME -LogonType Interactive
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit 0

$TaskName = "JarvisAssistantAutoStart"

Write-Host "Registering Task Scheduler task: $TaskName"
Register-ScheduledTask -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings -TaskName $TaskName -Description "Auto-start for Jarvis Voice Assistant" -Force

Write-Host "Done! Jarvis will start automatically on login." -ForegroundColor Green
