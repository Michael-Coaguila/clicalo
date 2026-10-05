# Keeps the Windows compatibility appraiser (CompatTelRunner.exe, started by the Application Experience scheduled
# tasks) from running on a hosted CI runner. It takes three of the four CPUs of the runner for seconds at a time and
# starves the desktop and performance tests: it was the busiest process every time a REG-01 restore went over its
# budget on a saturated runner (docs/testing/spikes/S1.md, finding 19).
#
# CI runners only: the workflows call it before cl desk and cl perf. Never run it on a developer's machine.
# Some of those tasks refuse to be disabled even by an administrator, so the executable itself is blocked through
# Image File Execution Options (any new launch runs a harmless stand-in that exits at once). A running instance is
# stopped, or, when Windows refuses that too, waited for (it cannot start again). The script fails if one is still
# running after that.

$ErrorActionPreference = 'Stop'
$waitSeconds = 600

$image = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\CompatTelRunner.exe'
New-Item -Path $image -Force | Out-Null
Set-ItemProperty -Path $image -Name Debugger -Value "$env:SystemRoot\System32\systray.exe"
Write-Host "CompatTelRunner.exe now starts $((Get-ItemProperty -Path $image).Debugger) instead."

foreach ($task in Get-ScheduledTask -TaskPath '\Microsoft\Windows\Application Experience\' -ErrorAction SilentlyContinue) {
    try {
        $task | Disable-ScheduledTask | Out-Null
        Write-Host "Disabled the scheduled task $($task.TaskName)."
    }
    catch {
        Write-Host "Kept the scheduled task $($task.TaskName) (Windows refused to disable it: $($_.Exception.Message.Trim()))."
    }
}

foreach ($process in @(Get-Process -Name CompatTelRunner -ErrorAction SilentlyContinue)) {
    try {
        Stop-Process -Id $process.Id -Force
        Write-Host "Stopped CompatTelRunner (pid $($process.Id))."
    }
    catch {
        Write-Host "Could not stop CompatTelRunner (pid $($process.Id)): $($_.Exception.Message.Trim()). Waiting for it to end."
    }
}

$started = Get-Date
while (Get-Process -Name CompatTelRunner -ErrorAction SilentlyContinue) {
    if (((Get-Date) - $started).TotalSeconds -ge $waitSeconds) {
        throw "CompatTelRunner is still running after $waitSeconds s."
    }

    Start-Sleep -Seconds 2
}

Write-Host "CompatTelRunner is not running and cannot start again on this runner (waited $([int]((Get-Date) - $started).TotalSeconds) s)."
