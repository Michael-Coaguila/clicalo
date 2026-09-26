using System.ComponentModel;
using System.Diagnostics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>The process behind a window, by name only (never the title: LOG-001). Thread-safe, cached per process.</summary>
internal static class ProcessNames
{
    private static readonly Lock Gate = new();
    private static readonly Dictionary<uint, string> Cache = [];

    /// <summary>The process that owns <paramref name="window"/>, and its name.</summary>
    public static unsafe (uint ProcessId, string Name) Of(nint window)
    {
        if (window == 0)
        {
            return (0, "ninguna ventana");
        }

        uint processId;
        _ = PInvoke.GetWindowThreadProcessId((HWND)window, &processId);
        return (processId, Name(processId));
    }

    /// <summary>The name of process <paramref name="processId"/>.</summary>
    public static string Name(uint processId)
    {
        if (processId == 0)
        {
            return "desconocido";
        }

        lock (Gate)
        {
            if (Cache.TryGetValue(processId, out var cached))
            {
                return cached;
            }
        }

        string name;
        try
        {
            using var process = Process.GetProcessById((int)processId);
            name = process.ProcessName;
        }
        catch (Exception ex)
            when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
            name =
                "proceso " + processId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        lock (Gate)
        {
            Cache[processId] = name;
        }

        return name;
    }
}
