using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Platform.Windows.SingleInstance;

/// <summary>
/// The process at the other end of a named pipe (blueprint §3.4, ADR-0010): the client checks the server's image and
/// the server checks the client's session, user and integrity. Each query answers <see langword="false"/> instead of
/// failing, and the caller treats that as «not verified».
/// </summary>
public static unsafe class PipePeer
{
    /// <summary>The process that created the server end of <paramref name="pipe"/>.</summary>
    /// <param name="pipe">The client end of a connected pipe.</param>
    /// <param name="processId">The server process, when the answer is <see langword="true"/>.</param>
    public static bool TryGetServerProcessId(SafePipeHandle pipe, out uint processId) =>
        Query(pipe, &PInvoke.GetNamedPipeServerProcessId, out processId);

    /// <summary>The process connected to the server end of <paramref name="pipe"/>.</summary>
    /// <param name="pipe">The server end of a connected pipe.</param>
    /// <param name="processId">The client process, when the answer is <see langword="true"/>.</param>
    public static bool TryGetClientProcessId(SafePipeHandle pipe, out uint processId) =>
        Query(pipe, &PInvoke.GetNamedPipeClientProcessId, out processId);

    /// <summary>The Windows session (WTS) of the client connected to <paramref name="pipe"/>.</summary>
    /// <param name="pipe">The server end of a connected pipe.</param>
    /// <param name="sessionId">The client session, when the answer is <see langword="true"/>.</param>
    public static bool TryGetClientSessionId(SafePipeHandle pipe, out uint sessionId) =>
        Query(pipe, &PInvoke.GetNamedPipeClientSessionId, out sessionId);

    private static bool Query(
        SafePipeHandle pipe,
        delegate* <HANDLE, uint*, BOOL> query,
        out uint value
    )
    {
        ArgumentNullException.ThrowIfNull(pipe);
        uint answer = 0;
        var added = false;
        try
        {
            pipe.DangerousAddRef(ref added);
            var ok = query(new HANDLE(pipe.DangerousGetHandle()), &answer);
            value = answer;
            return ok;
        }
        finally
        {
            if (added)
            {
                pipe.DangerousRelease();
            }
        }
    }
}
