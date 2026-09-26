using System.Collections.Concurrent;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// The windows of the laboratory surfaces by handle, so the foreground watcher can tell «a lab surface took the
/// foreground» (a REG-01 failure) from «an app of the maintainer came to the front».
/// </summary>
internal sealed class SurfaceDirectory
{
    private readonly ConcurrentDictionary<nint, string> _names = new();

    /// <summary>Records that <paramref name="window"/> is the surface <paramref name="name"/>.</summary>
    public void Register(nint window, string name)
    {
        if (window != 0)
        {
            _names[window] = name;
        }
    }

    /// <summary>Forgets <paramref name="window"/>.</summary>
    public void Unregister(nint window) => _names.TryRemove(window, out _);

    /// <summary>The surface name of <paramref name="window"/>, or null when it is not a lab surface.</summary>
    public string? NameOf(nint window) => _names.TryGetValue(window, out var name) ? name : null;
}
