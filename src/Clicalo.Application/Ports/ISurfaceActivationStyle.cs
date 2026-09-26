namespace Clicalo.Application.Ports;

/// <summary>
/// Lets <c>ForegroundOrchestrator</c> make a surface activatable for the length of a <c>TextInput</c> or
/// <c>KeyboardNavigation</c> lease, and non-activatable again afterwards (blueprint §3.5, §3.6). Implemented by
/// <c>Clicalo.UI.Wpf.Windowing.SurfaceRegistry</c>.
/// </summary>
/// <remarks>
/// Both methods may be called from any thread (the orchestrator runs on SysEvents), never block and return after the
/// extended style has been changed (<c>SetWindowLongPtr</c> works across threads of the same process). Unknown
/// surfaces are ignored.
/// </remarks>
public interface ISurfaceActivationStyle
{
    /// <summary>
    /// Removes <c>WS_EX_NOACTIVATE</c> from <paramref name="surface"/> and lets its <c>WM_WINDOWPOSCHANGING</c> hook
    /// stop adding <c>SWP_NOACTIVATE</c>.
    /// </summary>
    void AllowActivation(SurfaceId surface);

    /// <summary>Puts <c>WS_EX_NOACTIVATE</c> back on <paramref name="surface"/> and re-arms its hook.</summary>
    void RestoreNoActivate(SurfaceId surface);
}
