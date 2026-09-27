using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.SingleInstance;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// What <see cref="ForegroundMonitor"/> needs to know about the process behind a window (blueprint §7.9): its image
/// name, the real app behind <c>ApplicationFrameHost.exe</c>, and whether it runs elevated. The image and the token are
/// read by <see cref="ProcessIdentity"/> with <c>PROCESS_QUERY_LIMITED_INFORMATION</c>; every query answers "unknown"
/// instead of failing (EC-PER-03).
/// </summary>
internal static unsafe class ProcessInspector
{
    private const string FrameHostImage = "ApplicationFrameHost.exe";

    /// <summary>The file name of the process image (<c>notepad.exe</c>), or null when it cannot be read.</summary>
    public static string? ImageFileName(uint processId) => ProcessIdentity.ImageFileName(processId);

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
    public static ProcessElevation Elevation(uint processId) =>
        ProcessIdentity.IntegrityLevel(processId) switch
        {
            null => ProcessElevation.Unknown,
            { } level when ProcessIdentity.IsElevated(level) => ProcessElevation.Elevated,
            _ => ProcessElevation.NotElevated,
        };

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
