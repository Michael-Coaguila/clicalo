namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The rules of the Tab view that are not geometry (docs/04 «Vista pestaña», PES-008, PES-010, PES-012, PES-015). Pure.
/// </summary>
public static class DockRules
{
    /// <summary>Steps of the first-time guide (PES-015).</summary>
    public const int CoachSteps = 3;

    /// <summary>
    /// Whether the bar collapses <c>Timings.Dock.CollapseDelay</c> (900 ms) after <paramref name="use"/> (PES-012): with
    /// «Collapses» it does after an action that ran, a Mantener released included, but not after latching or releasing
    /// an Alternar nor after the first tap of a confirmation; with «Open» it never does by itself.
    /// </summary>
    /// <param name="use">What the button did.</param>
    /// <param name="pinOpen">The lock of the bar is «Open» (docs/02 <c>dock.pinOpen</c>).</param>
    public static bool CollapsesAfter(DockUse use, bool pinOpen) =>
        !pinOpen && use is DockUse.Ran or DockUse.HoldReleased;

    /// <summary>
    /// Whether 📌 «Pinned» shows among the tools (PES-008): only with the Always visible row on and some shortcut in it.
    /// </summary>
    /// <param name="showStripRow">The Always visible row is on (<c>showStripRow</c>).</param>
    /// <param name="pinnedCount">Shortcuts in Always visible.</param>
    public static bool ShowsPinned(bool showStripRow, int pinnedCount) =>
        showStripRow && pinnedCount > 0;

    /// <summary>
    /// Whether the window beside a button of the bar closes after one of its shortcuts is used (PES-010): yes, unless
    /// it holds or latches (Mantener or Alternar).
    /// </summary>
    /// <param name="holdsOrLatches">The shortcut is a Mantener or an Alternar.</param>
    public static bool SideWindowClosesAfterUse(bool holdsOrLatches) => !holdsOrLatches;

    /// <summary>
    /// Whether the first-time guide shows (PES-015): with the bar open, the guide not finished and no window beside the
    /// bar.
    /// </summary>
    /// <param name="barOpen">The bar is open.</param>
    /// <param name="coachDone">The guide was finished or skipped (docs/02 <c>dock.coachDone</c>).</param>
    /// <param name="sideWindowOpen">A window beside the bar is open.</param>
    public static bool ShowsCoach(bool barOpen, bool coachDone, bool sideWindowOpen) =>
        barOpen && !coachDone && !sideWindowOpen;

    /// <summary>The step after <paramref name="step"/>, or <see langword="null"/> when the guide ends (PES-015).</summary>
    /// <param name="step">The step on show, from 0.</param>
    public static int? NextCoachStep(int step) => step + 1 < CoachSteps ? step + 1 : null;
}
