using System.Diagnostics.CodeAnalysis;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// Repairs what can drift on a surface (blueprint §3.5): every <c>Timings.Foreground.SurfaceIntegrityInterval</c>
/// (30 s) and after each <c>WM_DPICHANGED</c>, <c>WM_DISPLAYCHANGE</c> or theme change, it checks
/// <c>GWL_EXSTYLE</c> (<c>WS_EX_NOACTIVATE</c>, <c>WS_EX_TOPMOST</c>) and the topmost band of each registered
/// surface and fixes them with <c>SWP_NOACTIVATE</c>. Surfaces under an activation lease keep their lease style.
/// Runs on the UI thread.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class SurfaceIntegrityCheck : IDisposable
{
    /// <summary>Creates the check over <paramref name="registry"/>, scheduled with <paramref name="timeProvider"/>.</summary>
    public SurfaceIntegrityCheck(SurfaceRegistry registry, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Registry = registry;
        TimeProvider = timeProvider;
    }

    /// <summary>Repairs made since the process started (logged with the surface and the property).</summary>
    public long Repairs => throw new NotImplementedException("M1 windowing package.");

    private SurfaceRegistry Registry { get; }

    private TimeProvider TimeProvider { get; }

    /// <summary>Starts the periodic check.</summary>
    public void Start() => throw new NotImplementedException("M1 windowing package.");

    /// <summary>Checks every surface now and returns how many properties it repaired.</summary>
    public int CheckNow() => throw new NotImplementedException("M1 windowing package.");

    /// <summary>Stops the periodic check.</summary>
    public void Dispose() { }
}
