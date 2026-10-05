namespace Clicalo.App.Lifecycle;

/// <summary>
/// Releases whatever Windows reports down, keys and buttons, with the menu mask before Alt or Win (ADR-0023), without
/// the engine: the preventive release of the start (SEG-006, REG-03, blueprint §3.1), when a previous process may have
/// died before its guardian ran, and «Soltar todo» of the tray, which must work with a hung engine. Implemented over
/// <c>Platform.Core/Injection</c>; it keeps no state, so any thread may call it.
/// </summary>
internal interface IPressedRelease
{
    /// <summary>Releases what is down; returns how many events were sent.</summary>
    int ReleasePressed();
}
