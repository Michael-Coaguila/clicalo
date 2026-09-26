using Clicalo.Application.Ports;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// What <c>SurfaceRegistry</c> (UI.Wpf) does for <c>ForegroundOrchestrator</c>, over one <see cref="TestWindow"/>:
/// it is the surface <see cref="Surface"/>, and a lease really removes and restores its <c>WS_EX_NOACTIVATE</c>.
/// Every call is counted so the tests can check that the style always goes back.
/// </summary>
internal sealed class TestSurfaces(TestWindow window) : ISurfaceActivationStyle, ISurfaceLookup
{
    /// <summary>The id the test window has as a surface.</summary>
    public static readonly SurfaceId Surface = new(SurfaceKind.Panel, 0);

    private int _allowed;
    private int _restored;

    /// <summary>Number of <see cref="AllowActivation"/> calls.</summary>
    public int Allowed => Volatile.Read(ref _allowed);

    /// <summary>Number of <see cref="RestoreNoActivate"/> calls.</summary>
    public int Restored => Volatile.Read(ref _restored);

    public void AllowActivation(SurfaceId surface)
    {
        if (surface == Surface)
        {
            Interlocked.Increment(ref _allowed);
            window.SetNonActivating(false);
        }
    }

    public void RestoreNoActivate(SurfaceId surface)
    {
        if (surface == Surface)
        {
            Interlocked.Increment(ref _restored);
            window.SetNonActivating(true);
        }
    }

    public bool TryGetSurface(WindowToken token, out SurfaceId surface)
    {
        surface = Surface;
        return token == window.Token;
    }
}
