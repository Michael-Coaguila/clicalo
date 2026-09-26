using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.HiDpi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Tools.SpikeLab.Reporting;

/// <summary>
/// Reads what the report says about the machine: the Windows build with its update revision (UBR) and its version
/// name, the monitors with their resolution and scale, and the input hardware. Nothing that names the user or the
/// machine.
/// </summary>
internal static class HardwareProbe
{
    private const uint PrimaryMonitor = 1;
    private const int IntegratedTouch = 0x01;
    private const int ExternalTouch = 0x02;
    private const int IntegratedPen = 0x04;
    private const int ExternalPen = 0x08;
    private const int DigitizerReady = 0x80;

    /// <summary>«10.0.26200.6584» and «25H2» (null when the registry does not say).</summary>
    public static (string Build, string? DisplayVersion) WindowsVersion()
    {
        var version = Environment.OSVersion.Version;
        int? revision = null;
        string? displayVersion = null;
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion"
            );
            revision = key?.GetValue("UBR") as int?;
            displayVersion = key?.GetValue("DisplayVersion") as string;
        }
        catch (Exception ex)
            when (ex
                    is System.Security.SecurityException
                        or UnauthorizedAccessException
                        or IOException
            )
        {
            // Without the registry the report keeps the build without its revision.
        }

        var build = string.Create(
            CultureInfo.InvariantCulture,
            $"{version.Major}.{version.Minor}.{version.Build}.{revision ?? version.Revision}"
        );
        return (build, displayVersion);
    }

    /// <summary>The input hardware as Windows reports it.</summary>
    public static InputHardware Input()
    {
        var digitizer = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_DIGITIZER);
        return new InputHardware(
            (digitizer & IntegratedTouch) != 0,
            (digitizer & ExternalTouch) != 0,
            (digitizer & IntegratedPen) != 0,
            (digitizer & ExternalPen) != 0,
            (digitizer & DigitizerReady) != 0,
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_MAXIMUMTOUCHES),
            PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_MOUSEPRESENT) != 0
        );
    }

    /// <summary>Every monitor, the primary first, with its resolution, work area and effective DPI.</summary>
    public static unsafe ImmutableArray<MonitorDescription> Monitors()
    {
        var handles = new List<HMONITOR>();
        var handle = GCHandle.Alloc(handles);
        try
        {
            _ = PInvoke.EnumDisplayMonitors(
                HDC.Null,
                (RECT*)null,
                &Collect,
                new LPARAM(GCHandle.ToIntPtr(handle))
            );
        }
        finally
        {
            handle.Free();
        }

        var monitors = ImmutableArray.CreateBuilder<MonitorDescription>(handles.Count);
        foreach (var monitor in handles)
        {
            var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
            if (!PInvoke.GetMonitorInfo(monitor, &info))
            {
                continue;
            }

            uint dpiX = 96;
            uint dpiY = 96;
            if (
                PInvoke
                    .GetDpiForMonitor(monitor, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, &dpiX, &dpiY)
                    .Failed
            )
            {
                dpiX = 96;
            }

            var screen = info.rcMonitor;
            var work = info.rcWork;
            monitors.Add(
                new MonitorDescription(
                    screen.left,
                    screen.top,
                    screen.right - screen.left,
                    screen.bottom - screen.top,
                    work.right - work.left,
                    work.bottom - work.top,
                    (int)dpiX,
                    (info.dwFlags & PrimaryMonitor) != 0
                )
            );
        }

        return [.. monitors.OrderByDescending(monitor => monitor.IsPrimary)];
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe BOOL Collect(HMONITOR monitor, HDC context, RECT* bounds, LPARAM data)
    {
        if (GCHandle.FromIntPtr(data.Value).Target is List<HMONITOR> handles)
        {
            handles.Add(monitor);
        }

        return true;
    }
}
