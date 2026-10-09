using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Session;

/// <summary>
/// What the panel shows right now (blueprint §6.4): not persisted, owned by <see cref="SessionStore"/> on the Surfaces
/// role and changed only through <see cref="PanelSessionReducer"/>, so it never holds an invalid state (PAN-001,
/// PAN-008). This is the walking-skeleton subset of M2: presence and the profile in view. Pages, the primary layer,
/// the context menu, edit mode, the Tab and the frozen layout join later; the profile grid joined in M3 (SEL-002).
/// </summary>
/// <param name="Presence">Whether the panel is on screen.</param>
/// <param name="View">The profile in view; never Always visible (PER-001).</param>
/// <param name="Version">Raised on every change, so projections can be memoized.</param>
/// <param name="PickerOpen">The profile grid is open (SEL-002); it closes with the panel and on a profile change.</param>
public sealed record PanelSession(
    PanelPresence Presence,
    ProfileId View,
    long Version,
    bool PickerOpen = false
)
{
    /// <summary>The session of a new process: the panel visible on <paramref name="view"/> (NFR-001).</summary>
    /// <param name="view">The profile to show first.</param>
    public static PanelSession Initial(ProfileId view) => new(PanelPresence.Visible, view, 0);
}
