namespace Clicalo.Application.Ports;

/// <summary>
/// Maps the window of a lease target to its surface, so <c>ForegroundOrchestrator</c> can call
/// <see cref="ISurfaceActivationStyle"/> for a <c>TextInput</c> or <c>KeyboardNavigation</c> lease whose
/// <c>LeaseRequest.Target</c> is a surface window. Implemented by <c>Clicalo.UI.Wpf.Windowing.SurfaceRegistry</c>;
/// thread-safe (it reads a published immutable map).
/// </summary>
/// <remarks>Not in the code block of blueprint §3.6: it is the missing link between its two surface-facing ports.</remarks>
public interface ISurfaceLookup
{
    /// <summary>Finds the surface whose window is <paramref name="window"/>; false if it is not a surface.</summary>
    bool TryGetSurface(WindowToken window, out SurfaceId surface);
}
