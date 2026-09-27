using System.Runtime.InteropServices;
using Clicalo.Domain.Migration.V1;

namespace Clicalo.App.Interop;

/// <summary>
/// The monitors the v1 migration places <c>window_pos</c> on (MIG-006, blueprint §6.6): the work area of each monitor
/// in physical pixels (the process is per-monitor DPI aware, app.manifest) and its real Windows scale; the placement
/// applies the rounding Qt 5 used.
/// </summary>
internal static class MonitorLayout
{
    private const uint MonitorInfoPrimary = 1;
    private const int EffectiveDpi = 0;

    private delegate bool MonitorEnumProc(nint monitor, nint hdc, nint rect, nint data);

    /// <summary>Every monitor now; empty when Windows does not answer (the placement then uses its fallback).</summary>
    public static IReadOnlyList<V1Monitor> Current()
    {
        var monitors = new List<V1Monitor>();
        var index = 0;
        MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            index++;
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info))
            {
                return true;
            }

            var scale =
                GetDpiForMonitor(monitor, EffectiveDpi, out var dpiX, out _) == 0 && dpiX > 0
                    ? dpiX / 96.0
                    : 1.0;
            var work = info.Work;
            monitors.Add(
                new V1Monitor(
                    "monitor" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    work.Left,
                    work.Top,
                    work.Right - work.Left,
                    work.Bottom - work.Top,
                    scale,
                    (info.Flags & MonitorInfoPrimary) != 0
                )
            );
            return true;
        };
        _ = EnumDisplayMonitors(0, 0, callback, 0);
        GC.KeepAlive(callback);
        return monitors;
    }

    [DllImport("user32.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplayMonitors(
        nint hdc,
        nint clip,
        MonitorEnumProc callback,
        nint data
    );

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [DllImport("shcore.dll")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int GetDpiForMonitor(
        nint monitor,
        int dpiType,
        out uint dpiX,
        out uint dpiY
    );

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect Work;
        public uint Flags;
    }
}
