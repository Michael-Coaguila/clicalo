using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Foreground;

/// <summary>
/// The two surface-facing ports that <c>SurfaceRegistry</c> implements in UI.Wpf: which windows are surfaces, and
/// whether each one currently accepts activation (<c>WS_EX_NOACTIVATE</c> removed).
/// </summary>
internal sealed class FakeSurfaceActivationStyle(ForegroundWorld world)
    : ISurfaceActivationStyle,
        ISurfaceLookup
{
    private readonly Dictionary<WindowToken, SurfaceId> _surfaces = [];

    /// <summary>Surfaces whose activation is allowed right now.</summary>
    public HashSet<SurfaceId> Activatable { get; } = [];

    public void Register(WindowToken window, SurfaceId surface) => _surfaces[window] = surface;

    public bool TryGetSurface(WindowToken window, out SurfaceId surface) =>
        _surfaces.TryGetValue(window, out surface);

    public void AllowActivation(SurfaceId surface)
    {
        Activatable.Add(surface);
        world.Write("allow " + surface);
    }

    public void RestoreNoActivate(SurfaceId surface)
    {
        Activatable.Remove(surface);
        world.Write("noactivate " + surface);
    }
}
