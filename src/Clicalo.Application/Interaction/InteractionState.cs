using Clicalo.Domain.Dimming;
using Clicalo.Domain.PanelLayout;

namespace Clicalo.Application.Interaction;

/// <summary>
/// The state that crosses the surfaces of the panel (blueprint §6.4, <c>InteractionState</c>): not persisted, owned by
/// <see cref="InteractionStore"/> on the Surfaces role and changed only through <see cref="InteractionReducer"/>. It
/// holds the forms that are not settings (the bubble, the bar open, the window beside it, the step of the guide) and
/// the automatic dimming (docs/04 «Opacidad y atenuado»): what is open elsewhere that keeps every surface awake, and
/// whether a finger or the pointer is on a surface. It also holds the notices (<see cref="Notices"/>, AVI-002), which the
/// notice bar of the panel, the notice surface of the Tab view (PES-014) and the status bar of the control center
/// (CCM-003) read from the same state.
/// </summary>
/// <param name="Minimized">«−» turned the panel into the bubble (PAN-001 a).</param>
/// <param name="DockOpen">The bar of the Tab view is open (PES-005).</param>
/// <param name="Flyout">The window beside the open bar.</param>
/// <param name="CoachStep">The step of the first-time guide on show, from 0 (PES-015).</param>
/// <param name="Open">
/// What other parts of the app say is open or on and keeps every surface from dimming: panic, Quick settings, a context
/// menu, the profile grid, the search, edit mode, the Control Center, the welcome (docs/04). The windows beside the bar
/// come from <paramref name="Flyout"/>.
/// </param>
/// <param name="PointerInside">A finger, the pen or the mouse pointer is on a surface of the panel.</param>
/// <param name="LastLeave">When the last one left, or when a surface appeared; null while none has (it stays awake).</param>
/// <param name="Version">Raised on every change.</param>
public sealed record InteractionState(
    bool Minimized,
    bool DockOpen,
    DockFlyout Flyout,
    int CoachStep,
    DimExceptions Open,
    bool PointerInside,
    DateTimeOffset? LastLeave,
    long Version
)
{
    /// <summary>The state of a new process: the panel as General says, the bar closed, nothing open.</summary>
    public static InteractionState Initial { get; } =
        new(false, false, DockFlyout.None, 0, DimExceptions.None, false, null, 0);

    /// <summary>
    /// 🔍 on the bar of the Tab view opened the search (BUS-006): the panel shows as the Full view until the search
    /// closes or a result runs; the saved view stays the Tab view.
    /// </summary>
    public bool SearchPeek { get; init; }

    /// <summary>The notices and which one shows now (AVI-002).</summary>
    public NoticeQueue Notices { get; init; } = NoticeQueue.Empty;

    /// <summary>Everything that keeps the surfaces from dimming right now, the windows beside the bar included.</summary>
    public DimExceptions Exceptions =>
        Flyout == DockFlyout.None ? Open : Open | DimExceptions.DockSideWindows;
}
