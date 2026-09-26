using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The kernel handles of the guardian contract (blueprint §3.1, ADR-0018): the main process's handle to itself for
/// Sentinel (<c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c>, inheritable), the anonymous heartbeat pipe, and
/// the waits Sentinel blocks in. Handles are passed as values (<see cref="nint"/>) because they cross the command line.
/// </summary>
public static unsafe class GuardianHandles
{
    private const uint Synchronize = 0x0010_0000;
    private const uint ProcessQueryLimitedInformation = 0x1000;
    private const uint Infinite = 0xFFFF_FFFF;

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

    /// <summary>
    /// An anonymous pipe for the heartbeat: the read end is inheritable (Sentinel's), the write end is not (the main
    /// process's). The caller closes the read end after the launch.
    /// </summary>
    public static (nint Read, nint Write) CreateHeartbeatPipe()
    {
        HANDLE read;
        HANDLE write;
        if (!PInvoke.CreatePipe(&read, &write, null, 0))
        {
            throw new Win32Exception(Marshal.GetLastSystemError());
        }

        if (
            !PInvoke.SetHandleInformation(
                read,
                (uint)HANDLE_FLAGS.HANDLE_FLAG_INHERIT,
                HANDLE_FLAGS.HANDLE_FLAG_INHERIT
            )
        )
        {
            var error = Marshal.GetLastSystemError();
            PInvoke.CloseHandle(read);
            PInvoke.CloseHandle(write);
            throw new Win32Exception(error);
        }

        return ((nint)read.Value, (nint)write.Value);
    }

    /// <summary>Writes one heartbeat byte; <see langword="false"/> when Sentinel's end is gone (the pipe broke).</summary>
    /// <param name="writeEnd">The main process's end of the pipe.</param>
    public static bool WriteHeartbeat(nint writeEnd)
    {
        byte beat = 1;
        uint written;
        return PInvoke.WriteFile(new HANDLE(writeEnd), &beat, 1, &written, null) && written == 1;
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

    /// <summary>
    /// Sentinel's wait: blocks until the parent process ends or the heartbeat pipe breaks, using no CPU. A background
    /// thread reads the pipe (the bytes themselves are ignored) and signals when the read fails.
    /// </summary>
    /// <param name="parentProcess">The parent process handle.</param>
    /// <param name="heartbeatPipe">Sentinel's end of the pipe.</param>
    public static GuardianWake WaitForExitOrBrokenPipe(nint parentProcess, nint heartbeatPipe)
    {
        var broken = PInvoke.CreateEvent(
            (Windows.Win32.Security.SECURITY_ATTRIBUTES*)null,
            true,
            false,
            default(PCWSTR)
        );
        if (broken.IsNull)
        {
            throw new Win32Exception(Marshal.GetLastSystemError());
        }

        var reader = new Thread(() => DrainUntilBroken(heartbeatPipe, (nint)broken.Value))
        {
            IsBackground = true,
            Name = "Clicalo.Sentinel.Pipe",
        };
        reader.Start();

        var handles = stackalloc HANDLE[2];
        handles[0] = new HANDLE(parentProcess);
        handles[1] = broken;
        var woke = PInvoke.WaitForMultipleObjects(2, handles, false, Infinite);

        // The reader may still be blocked on a live pipe; it is a background thread and dies with the process.
        return woke == WAIT_EVENT.WAIT_OBJECT_0
            ? GuardianWake.ParentExited
            : GuardianWake.PipeBroken;
    }

    /// <summary>Whether <paramref name="process"/> ends within <paramref name="timeout"/>.</summary>
    /// <param name="process">A process handle with <c>SYNCHRONIZE</c>.</param>
    /// <param name="timeout">How long to wait.</param>
    public static bool WaitForExit(nint process, TimeSpan timeout)
    {
        var milliseconds =
            timeout == Timeout.InfiniteTimeSpan
                ? Infinite
                : (uint)Math.Clamp((long)timeout.TotalMilliseconds, 0, Infinite - 1);
        return PInvoke.WaitForSingleObject(new HANDLE(process), milliseconds)
            == WAIT_EVENT.WAIT_OBJECT_0;
    }

    /// <summary>Duplicates a handle of this process with the same access (tests hand a copy to the guardian).</summary>
    /// <param name="handle">The handle.</param>
    /// <param name="inheritable">Whether the copy is inheritable.</param>
    public static nint Duplicate(nint handle, bool inheritable)
    {
        HANDLE duplicate;
        var self = PInvoke.GetCurrentProcess();
        if (
            !PInvoke.DuplicateHandle(
                self,
                new HANDLE(handle),
                self,
                &duplicate,
                0,
                inheritable,
                DUPLICATE_HANDLE_OPTIONS.DUPLICATE_SAME_ACCESS
            )
        )
        {
            throw new Win32Exception(Marshal.GetLastSystemError());
        }

        return (nint)duplicate.Value;
    }

    private static void DrainUntilBroken(nint pipe, nint brokenEvent)
    {
        var buffer = stackalloc byte[16];
        uint read;
        while (PInvoke.ReadFile(new HANDLE(pipe), buffer, 16, &read, null) && read > 0)
        {
            // Heartbeats carry no data: only the pipe's life matters.
        }

        PInvoke.SetEvent(new HANDLE(brokenEvent));
    }
}
