using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The kernel handle of the guardian contract (blueprint §3.1, ADR-0023): the main process's handle to itself for
/// Sentinel (<c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c>, inheritable), the wait Sentinel blocks in and the
/// exit code it reads. Handles are passed as values (<see cref="nint"/>) because they cross the command line.
/// </summary>
public static unsafe class GuardianHandles
{
    private const uint Synchronize = 0x0010_0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Infinite = 0xFFFF_FFFF;
    private const uint StillActive = 259;

    /// <summary>
    /// The current process as an inheritable handle with <c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c> only,
    /// for Sentinel's handle list. The caller closes it after the launch.
    /// </summary>
    public static nint DuplicateCurrentProcessForChild()
    {
        HANDLE duplicate;
        var self = PInvoke.GetCurrentProcess();
        if (
            !PInvoke.DuplicateHandle(
                self,
                self,
                self,
                &duplicate,
                Synchronize | ProcessQueryLimitedInformation,
                true,
                default
            )
        )
        {
            throw new Win32Exception(Marshal.GetLastSystemError());
        }

        return (nint)duplicate.Value;
    }

    /// <summary>Closes a handle this process owns.</summary>
    /// <param name="handle">The handle.</param>
    public static void Close(nint handle)
    {
        if (handle != 0)
        {
            PInvoke.CloseHandle(new HANDLE(handle));
        }
    }

    /// <summary>Whether <paramref name="process"/> ends within <paramref name="timeout"/>.</summary>
    /// <param name="process">A process handle with <c>SYNCHRONIZE</c>.</param>
    /// <param name="timeout">How long to wait; <see cref="Timeout.InfiniteTimeSpan"/> blocks until it ends.</param>
    public static bool WaitForExit(nint process, TimeSpan timeout)
    {
        var milliseconds =
            timeout == Timeout.InfiniteTimeSpan
                ? Infinite
                : (uint)Math.Clamp((long)timeout.TotalMilliseconds, 0, Infinite - 1);
        return PInvoke.WaitForSingleObject(new HANDLE(process), milliseconds)
            == WAIT_EVENT.WAIT_OBJECT_0;
    }

    /// <summary>The exit code of an ended process, or <see langword="null"/> while it runs or when it cannot be read.</summary>
    /// <param name="process">A process handle with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
    public static int? ExitCode(nint process)
    {
        uint code;
        return PInvoke.GetExitCodeProcess(new HANDLE(process), &code) && code != StillActive
            ? unchecked((int)code)
            : null;
    }
}
