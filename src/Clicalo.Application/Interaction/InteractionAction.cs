using Clicalo.Domain.Dimming;
using Clicalo.Domain.PanelLayout;

namespace Clicalo.Application.Interaction;

/// <summary>
/// An intention that changes the <see cref="InteractionState"/> (blueprint §6.4): the surfaces, their view models and
/// the coordinators send these to <see cref="InteractionStore.Dispatch"/>; the Workspace role sends them through the
/// Surfaces role. Nobody sets the state directly.
/// </summary>
public abstract record InteractionAction
{
    private InteractionAction() { }

    /// <summary>«−» in the header: the panel becomes the bubble (PAN-001 a).</summary>
    public sealed record Minimize : InteractionAction;

    /// <summary>
    /// A tap on the bubble without dragging, or the tray showing the panel: back to the form it had (PAN-001 b, e).
    /// </summary>
    public sealed record Restore : InteractionAction;

    /// <summary>A tap on the handle: the bar opens (PES-002).</summary>
    public sealed record OpenDock : InteractionAction;

    /// <summary>Close on the bar or its automatic collapse: the bar folds and its side windows close (PES-005).</summary>
    public sealed record CloseDock : InteractionAction;

    /// <summary>A button of the bar opens its side window, or closes it when it is the one open (PES-008, PES-011).</summary>
    /// <param name="Flyout">The window.</param>
    public sealed record ToggleFlyout(DockFlyout Flyout) : InteractionAction;

    /// <summary>The side window closes (after using one of its shortcuts, a profile chosen: PES-010, SEL-004).</summary>
    public sealed record CloseFlyout : InteractionAction;

    /// <summary>
    /// The view changed (Quick settings › View, Expand or Search on the bar): out of the bubble, the bar folded and no
    /// side window (PAN-001 c, d).
    /// </summary>
    public sealed record ViewChanged : InteractionAction;

    /// <summary>[next] in the guide: the next step (PES-015); after the last one it is back at 0 for another time.</summary>
    public sealed record CoachNext : InteractionAction;

    /// <summary>The guide closes ([coachSkip] or [understood]): back to its first step (PES-015).</summary>
    public sealed record CoachReset : InteractionAction;

    /// <summary>
    /// Something that keeps the surfaces awake opened or closed elsewhere (docs/04): Quick settings, a context menu,
    /// edit mode, the Control Center, the welcome, the search, the profile grid or the panic.
    /// </summary>
    /// <param name="What">One or more flags.</param>
    /// <param name="IsOpen">Whether it is open now.</param>
    public sealed record SetOpen(DimExceptions What, bool IsOpen) : InteractionAction;

    /// <summary>A finger, the pen or the pointer came onto a surface of the panel: it wakes (GEN-009).</summary>
    public sealed record PointerEntered : InteractionAction;

    /// <summary>The last finger, pen or pointer left the surfaces: they dim a while later (GEN-009).</summary>
    public sealed record PointerLeft : InteractionAction;

    /// <summary>A surface appeared: it is awake and dims a while later unless a finger comes onto it (GEN-009).</summary>
    public sealed record SurfaceShown : InteractionAction;

    /// <summary>A notice to show now, for <paramref name="Duration"/> (AVI-002).</summary>
    /// <param name="Notice">The notice.</param>
    /// <param name="Duration">How long it shows.</param>
    public sealed record PostNotice(Notice Notice, TimeSpan Duration) : InteractionAction;

    /// <summary>A fixed notice that lasts while the state of <paramref name="Owner"/> lasts (AVI-002).</summary>
    /// <param name="Owner">Whose state it tells; compared by reference.</param>
    /// <param name="Notice">The notice.</param>
    public sealed record ShowStickyNotice(object Owner, Notice Notice) : InteractionAction;

    /// <summary>The state of <paramref name="Owner"/> ended: its fixed notice goes away (AVI-002).</summary>
    /// <param name="Owner">The owner given to <see cref="ShowStickyNotice"/>.</param>
    public sealed record ClearStickyNotice(object Owner) : InteractionAction;

    /// <summary>The notices of <paramref name="Kind"/> no longer apply: the operation was undone (AVI-003).</summary>
    /// <param name="Kind">The kind to drop.</param>
    public sealed record DismissNotices(NoticeKind Kind) : InteractionAction;

    /// <summary>The notice on show reached its end: the next one shows (AVI-002).</summary>
    public sealed record NoticeTick : InteractionAction;
}
