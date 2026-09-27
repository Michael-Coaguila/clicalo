namespace Clicalo.App.Lifecycle;

/// <summary>
/// The preventive release of the start (SEG-006, REG-03, blueprint §3.1): if the previous process died before its
/// guardian ran, a modifier may still be down. Before the engine accepts anything, every modifier that
/// <c>GetAsyncKeyState</c> reports down is released with the menu mask before Alt or Win. Implemented by the engine
/// package in <c>Platform.Windows/Input</c>, through the same injection path as everything else.
/// </summary>
internal interface IStartupRelease
{
    /// <summary>Releases the modifiers that are down; returns how many were released.</summary>
    int ReleaseStuckModifiers();
}
