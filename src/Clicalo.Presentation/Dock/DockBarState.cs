using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// Everything the handle and the bar of the Tab view show besides the shortcuts (docs/04 «Vista pestaña»), as the
/// composition reads it from the settings, the session, the interaction store and the engine.
/// </summary>
/// <param name="Dock">The settings of the Tab view (side, per page, lock, guide).</param>
/// <param name="Metrics">The measures of the panel size: the thickness of the bar and its shortcuts (PES-005, PES-007).</param>
/// <param name="ShowStripRow">The Always visible row is on (PES-008).</param>
/// <param name="StickyRow">The sticky modifiers switch is on (PES-008).</param>
/// <param name="VoiceNumbers">«Numbers for voice» is on (ACC-009).</param>
/// <param name="Frequents">Frequents is in view.</param>
/// <param name="ProfileName">The profile in view, or retProf in Frequents (PES-006).</param>
/// <param name="ProfileIcon">Its icon.</param>
/// <param name="IsActiveApp">It is the profile of the active app (the dot).</param>
/// <param name="IsFixed">Fixed (red, lock) or Auto (blue).</param>
/// <param name="CanRepeat">There is a last action to repeat.</param>
/// <param name="Flyout">The window beside the bar.</param>
/// <param name="CoachStep">The step of the guide on show.</param>
/// <param name="BarOpen">The bar is open.</param>
/// <param name="AnythingHeld">Something is held or latched, sticky keys included (PES-001, PES-013).</param>
public sealed record DockBarState(
    DockSettings Dock,
    SizeMetrics Metrics,
    bool ShowStripRow,
    bool StickyRow,
    bool VoiceNumbers,
    bool Frequents,
    string ProfileName,
    string ProfileIcon,
    bool IsActiveApp,
    bool IsFixed,
    bool CanRepeat,
    DockFlyout Flyout,
    int CoachStep,
    bool BarOpen,
    bool AnythingHeld
)
{
    /// <summary>Quick settings are open beside the bar (PES-009).</summary>
    public bool QuickOpen { get; init; }
}
