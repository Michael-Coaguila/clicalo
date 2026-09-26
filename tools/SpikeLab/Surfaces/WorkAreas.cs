using Clicalo.Domain.Geometry;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>The work area (the monitor without the taskbar) of the monitor nearest to a rectangle, in physical pixels.</summary>
internal static class WorkAreas
{
    /// <summary>The work area of the monitor nearest to <paramref name="bounds"/>; empty when Windows does not say.</summary>
    public static PhysicalRect Of(PhysicalRect bounds)
    {
        var rect = new RECT
        {
            left = bounds.Left,
            top = bounds.Top,
            right = bounds.Right,
            bottom = bounds.Bottom,
        };
        var monitor = PInvoke.MonitorFromRect(rect, MONITOR_FROM_FLAGS.MONITOR_DEFAULTTONEAREST);
        return Of(monitor);
    }

    /// <summary>The work area of <paramref name="monitor"/>; empty when Windows does not say.</summary>
    public static unsafe PhysicalRect Of(HMONITOR monitor)
    {
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (monitor == HMONITOR.Null || !PInvoke.GetMonitorInfo(monitor, &info))
        {
            return PhysicalRect.Empty;
        }

        var work = info.rcWork;
        return PhysicalRect.FromEdges(work.left, work.top, work.right, work.bottom);
    }
}
