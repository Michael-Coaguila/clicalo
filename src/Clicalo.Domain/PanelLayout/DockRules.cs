namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The rules of the Tab view that are not geometry (docs/04 «Vista pestaña», PES-008, PES-010, PES-012, PES-015). Pure.
/// </summary>
public static class DockRules
{
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
    /// Whether the open bar folds once its wait after a use passed (PES-012, PAN-009): never under a finger, with its
    /// menu or quick settings open, or while a macro still runs (it waits again once the macro ends, D21 review).
    /// </summary>
    /// <param name="pinOpen">The lock of the bar is «Open».</param>
    /// <param name="touched">A finger, the pen or the pointer is on the bar.</param>
    /// <param name="sheetOpen">The menu or quick settings are open beside the bar.</param>
    /// <param name="macroRunning">A macro is running.</param>
    public static bool FoldsNow(bool pinOpen, bool touched, bool sheetOpen, bool macroRunning) =>
        !pinOpen && !touched && !sheetOpen && !macroRunning;

    /// <summary>What releasing a held button of the bar did (PES-012): a tool of the bar only moved through the document.</summary>
    /// <param name="barTool">The button is a tool of the bar (⏶ ⏷, an arrow), not a shortcut.</param>
    public static DockUse UseOfRelease(bool barTool) =>
        barTool ? DockUse.Navigated : DockUse.HoldReleased;

    /// <summary>
    /// Whether the floating «Release all» of the Tab view and the bubble shows (PES-013, D20 review): for anything held
    /// with no finger on it (a latched Toggle, a locked sticky key, a drag). A Mantener under a finger ends when it
    /// lifts; a pill over the screen for it only covered what the person was working on.
    /// </summary>
    /// <param name="heldContacts">The contact of each item held, null for one that outlasts its contact.</param>
    public static bool ShowsFloatingRelease(IEnumerable<int?> heldContacts)
    {
        ArgumentNullException.ThrowIfNull(heldContacts);
        return heldContacts.Any(static contact => contact is null);
    }

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

    /// <summary>
    /// PES-014: the notice surface of the Tab view shows beside the bar, or beside the handle when the bar is closed,
    /// while there is something to tell: a notice, the administrator notice or test mode. D21: a plain confirmation
    /// («Ctrl+S enviado a Word») is not something to tell there, it covered the app after every shortcut, and it is only
    /// said. The Compact view, which has no status row, shows every notice for a moment in the same surface under the
    /// panel, confirmations included; its administrator notice and test mode stay inside the panel. The bubble shows the
    /// notices that are not confirmations beside it, so a safety notice is never lost while the panel is minimized
    /// (AVI-002).
    /// </summary>
    /// <param name="form">The form of the panel.</param>
    /// <param name="notice">A notice is on show (AVI-002).</param>
    /// <param name="confirmation">It is a plain confirmation: no warning, no [undo] and no [cancel].</param>
    /// <param name="adminNotice">The app in front is elevated and Clícalo is not (EJE-013).</param>
    /// <param name="testMode">Test mode is on (TAC-008).</param>
    public static bool ShowsNotices(
        PanelForm form,
        bool notice,
        bool confirmation,
        bool adminNotice,
        bool testMode
    ) =>
        PanelForms.IsDock(form) ? (notice && !confirmation) || adminNotice || testMode
        : form == PanelForm.Bubble ? notice && !confirmation
        : form == PanelForm.Compact && notice;

    /// <summary>
    /// PES-009, CUA-014: Quick settings and the menu of a shortcut open beside the bar only while it is open.
    /// </summary>
    /// <param name="form">The form of the panel.</param>
    /// <param name="open">Quick settings or the menu is open.</param>
    public static bool ShowsBesideBar(PanelForm form, bool open) =>
        form == PanelForm.DockOpen && open;

    /// <summary>
    /// AJR-001 (D21): in the Full and Compact views Quick settings open beside the panel, in the tall window the Tab view
    /// opens beside its bar, so they are seldom scrolled and the panel keeps its size.
    /// </summary>
    /// <param name="form">The form of the panel.</param>
    /// <param name="open">Quick settings are open.</param>
    public static bool ShowsQuickBesidePanel(PanelForm form, bool open) =>
        PanelForms.IsPanel(form) && open;
}
