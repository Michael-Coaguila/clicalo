<#
.SYNOPSIS
Runs a command on a hidden desktop of this session, so the windows it opens never show on the screen.

.DESCRIPTION
Runs $Command (cmd.exe syntax) on a desktop of its own that is never switched to: the desktop tests, the gallery and
the app itself can run while the person keeps working, and nothing they open is drawn on the screen or takes the
foreground. Every process of the run is in one job, killed with it. SendInput does not reach the windows of a hidden
desktop, so what needs injected input (the trackpad, real taps) cannot be checked here.

.EXAMPLE
powershell -File tools\qa\hidden-desktop.ps1 -Command '.\cl.cmd check' -TimeoutSeconds 1800

.EXAMPLE
powershell -File tools\qa\hidden-desktop.ps1 -Command 'cmd /c "set CLICALO_DESKTOP_TESTS=1&& dotnet test --project tests\Clicalo.Windowing.IntegrationTests -c Debug --no-build --filter-namespace Clicalo.Windowing.IntegrationTests.TabView"'
#>
param(
    [Parameter(Mandatory)] [string]$Command,
    [string]$Output = "$env:TEMP\clicalo-hidden-out.txt",
    [int]$TimeoutSeconds = 900,
    [string]$WorkDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)
if (-not ('HiddenDesktop' -as [type])) {
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class HiddenDesktop {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct STARTUPINFO {
        public int cb; public string lpReserved; public string lpDesktop; public string lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2; public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct PROCESS_INFORMATION { public IntPtr hProcess, hThread; public int dwProcessId, dwThreadId; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern IntPtr CreateDesktopW(string name, IntPtr device, IntPtr devmode, int flags, uint access, IntPtr sa);
    [DllImport("user32.dll", SetLastError = true)] static extern bool CloseDesktop(IntPtr desktop);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateProcessW(string app, string cmd, IntPtr pa, IntPtr ta, bool inherit, uint flags,
        IntPtr env, string dir, ref STARTUPINFO si, out PROCESS_INFORMATION pi);
    [DllImport("kernel32.dll")] static extern uint WaitForSingleObject(IntPtr handle, uint ms);
    [DllImport("kernel32.dll")] static extern bool GetExitCodeProcess(IntPtr process, out uint code);
    [DllImport("kernel32.dll")] static extern bool TerminateProcess(IntPtr process, uint code);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObjectW(IntPtr sa, string name);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] static extern bool TerminateJobObject(IntPtr job, uint code);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref EXTENDED info, int length);
    [StructLayout(LayoutKind.Sequential)]
    struct EXTENDED {
        public long PerProcessUserTimeLimit, PerJobUserTimeLimit; public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize, MaximumWorkingSetSize; public uint ActiveProcessLimit;
        public UIntPtr Affinity; public uint PriorityClass, SchedulingClass;
        public ulong ReadOperationCount, WriteOperationCount, OtherOperationCount, ReadTransferCount, WriteTransferCount, OtherTransferCount;
        public UIntPtr ProcessMemoryLimit, JobMemoryLimit, PeakProcessMemoryUsed, PeakJobMemoryUsed;
    }
    public static int Run(string name, string commandLine, string dir, int timeoutMs) {
        var desktop = CreateDesktopW(name, IntPtr.Zero, IntPtr.Zero, 0, 0x10000000u, IntPtr.Zero);
        if (desktop == IntPtr.Zero) throw new System.ComponentModel.Win32Exception();
        // Every process of the run is in one job, killed with it: nothing is left on the hidden desktop.
        var job = CreateJobObjectW(IntPtr.Zero, null);
        var limits = new EXTENDED { LimitFlags = 0x2000 };
        SetInformationJobObject(job, 9, ref limits, Marshal.SizeOf<EXTENDED>());
        try {
            var si = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFO>(), lpDesktop = "WinSta0\\" + name };
            PROCESS_INFORMATION pi;
            if (!CreateProcessW(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, 0x08000004u, IntPtr.Zero, dir, ref si, out pi))
                throw new System.ComponentModel.Win32Exception();
            AssignProcessToJobObject(job, pi.hProcess);
            ResumeThread(pi.hThread);
            try {
                if (WaitForSingleObject(pi.hProcess, (uint)timeoutMs) != 0) { TerminateJobObject(job, 124); return 124; }
                uint code; GetExitCodeProcess(pi.hProcess, out code); return (int)code;
            } finally { CloseHandle(pi.hThread); CloseHandle(pi.hProcess); }
        } finally { TerminateJobObject(job, 1); CloseHandle(job); CloseDesktop(desktop); }
    }
}
'@
}
Remove-Item $Output -ErrorAction SilentlyContinue
$line = "cmd.exe /d /c ""$Command > ""$Output"" 2>&1"""
$code = [HiddenDesktop]::Run('clicalo-test', $line, $WorkDir, $TimeoutSeconds * 1000)
"exit code $code"
if (Test-Path $Output) { Get-Content $Output -Tail 40 }
