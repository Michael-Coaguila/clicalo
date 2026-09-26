using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security;
using Windows.Win32.System.Threading;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// What <see cref="ForegroundMonitor"/> needs to know about the process behind a window (blueprint §7.9): its image
/// name, the real app behind <c>ApplicationFrameHost.exe</c>, and whether it runs elevated. Every query uses
/// <c>PROCESS_QUERY_LIMITED_INFORMATION</c> and answers "unknown" instead of failing (EC-PER-03).
/// </summary>
internal static unsafe class ProcessInspector
{
    private const string FrameHostImage = "ApplicationFrameHost.exe";
    private const int MaxPath = 1024;

    /// <summary>The file name of the process image (<c>notepad.exe</c>), or null when it cannot be read.</summary>
    public static string? ImageFileName(uint processId)
    {
        var process = Open(processId);
        if (process.IsNull)
        {
            return null;
        }

        try
        {
            var buffer = stackalloc char[MaxPath];
            var length = (uint)MaxPath;
            return PInvoke.QueryFullProcessImageName(
                process,
                PROCESS_NAME_FORMAT.PROCESS_NAME_WIN32,
                new PWSTR(buffer),
                &length
            )
                ? Path.GetFileName(new string(buffer, 0, (int)length))
                : null;
        }
        finally
        {
            _ = PInvoke.CloseHandle(process);
        }
    }

    /// <summary>True when <paramref name="imageFileName"/> is the host of Store app frames.</summary>
    public static bool IsFrameHost(string? imageFileName) =>
        string.Equals(imageFileName, FrameHostImage, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// For an <c>ApplicationFrameHost.exe</c> frame, the process of the hosted app: the first child window that
    /// belongs to another process. Zero when there is none (the app is suspended or still starting).
    /// </summary>
    public static uint HostedAppProcess(WindowToken frame, uint frameProcessId)
    {
        var search = new HostedSearch { FrameProcessId = frameProcessId };
        _ = PInvoke.EnumChildWindows((HWND)frame.Handle, &FindHostedApp, (LPARAM)(nint)(&search));
        return search.Found;
    }

    /// <summary>Whether the process runs at High integrity or above; unknown when its token cannot be read.</summary>
    public static ProcessElevation Elevation(uint processId)
    {
        var process = Open(processId);
        if (process.IsNull)
        {
            return ProcessElevation.Unknown;
        }

        HANDLE token = default;
        try
        {
            if (!PInvoke.OpenProcessToken(process, TOKEN_ACCESS_MASK.TOKEN_QUERY, &token))
            {
                return ProcessElevation.Unknown;
            }

            uint needed = 0;
            _ = PInvoke.GetTokenInformation(
                token,
                TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
                null,
                0,
                &needed
            );
            if (needed == 0)
            {
                return ProcessElevation.Unknown;
            }

            var buffer = stackalloc byte[(int)needed];
            if (
                !PInvoke.GetTokenInformation(
                    token,
                    TOKEN_INFORMATION_CLASS.TokenIntegrityLevel,
                    buffer,
                    needed,
                    &needed
                )
            )
            {
                return ProcessElevation.Unknown;
            }

            var sid = ((TOKEN_MANDATORY_LABEL*)buffer)->Label.Sid;
            var count = *PInvoke.GetSidSubAuthorityCount(sid);
            if (count == 0)
            {
                return ProcessElevation.Unknown;
            }

            var level = *PInvoke.GetSidSubAuthority(sid, (uint)(count - 1));
            return level >= PInvoke.SECURITY_MANDATORY_HIGH_RID
                ? ProcessElevation.Elevated
                : ProcessElevation.NotElevated;
        }
        finally
        {
            if (!token.IsNull)
            {
                _ = PInvoke.CloseHandle(token);
            }

            _ = PInvoke.CloseHandle(process);
        }
    }

    private static HANDLE Open(uint processId) =>
        processId == 0
            ? default
            : PInvoke.OpenProcess(
                PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                processId
            );

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL FindHostedApp(HWND child, LPARAM state)
    {
        var search = (HostedSearch*)state.Value;
        uint processId = 0;
        _ = PInvoke.GetWindowThreadProcessId(child, &processId);
        if (processId != 0 && processId != search->FrameProcessId)
        {
            search->Found = processId;
            return false;
        }

        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HostedSearch
    {
        public uint FrameProcessId;
        public uint Found;
    }
}
