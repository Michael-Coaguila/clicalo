using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Domain.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The monitors of the desktop as the placement rules need them (blueprint §3.7, PAN-006): <c>EnumDisplayMonitors</c>,
/// <c>GetMonitorInfo</c> (bounds, <c>rcWork</c> and the device name, which survives a restart) and
/// <c>GetDpiForMonitor</c>, in physical pixels of a per-monitor DPI aware process. Read on the UI thread when a surface
/// is placed, after a change of display, work area or DPI.
/// </summary>
public static class DisplayMonitors
{
    private const double BaseDpi = 96;

    [ThreadStatic]
    private static List<DisplayMonitor>? t_found;

    /// <summary>
    /// The monitors connected now; never empty: without an answer from Windows, one monitor with the work area WPF
    /// reports.
    /// </summary>
    public static unsafe ImmutableArray<DisplayMonitor> Snapshot()
    {
        var found = new List<DisplayMonitor>();
        t_found = found;
        try
        {
            _ = PInvoke.EnumDisplayMonitors(HDC.Null, (RECT*)null, &OnMonitor, 0);
        }
        finally
        {
            t_found = null;
        }

        return found.Count > 0 ? [.. found] : [Fallback()];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL OnMonitor(HMONITOR monitor, HDC dc, RECT* clip, LPARAM data)
    {
        var info = new MONITORINFOEXW();
        info.monitorInfo.cbSize = (uint)sizeof(MONITORINFOEXW);
        if (PInvoke.GetMonitorInfo(monitor, (MONITORINFO*)&info))
        {
            var scale = 1d;
            if (
                PInvoke
                    .GetDpiForMonitor(
                        monitor,
                        MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI,
                        out var dpiX,
                        out _
                    )
                    .Succeeded
                && dpiX > 0
            )
            {
                scale = dpiX / BaseDpi;
            }

            var bounds = info.monitorInfo.rcMonitor;
            var work = info.monitorInfo.rcWork;
            t_found?.Add(
                new DisplayMonitor(
                    info.szDevice.ToString(),
                    PhysicalRect.FromEdges(bounds.left, bounds.top, bounds.right, bounds.bottom),
                    PhysicalRect.FromEdges(work.left, work.top, work.right, work.bottom),
                    (info.monitorInfo.dwFlags & PInvoke.MONITORINFOF_PRIMARY) != 0,
                    scale
                )
            );
        }

        return true;
    }

    private static DisplayMonitor Fallback()
    {
        var work = System.Windows.SystemParameters.WorkArea;
        var rect = PhysicalRect.FromEdges(
            (int)work.Left,
            (int)work.Top,
            (int)work.Right,
            (int)work.Bottom
        );
        return new DisplayMonitor(string.Empty, rect, rect, IsPrimary: true, Scale: 1);
    }
}
