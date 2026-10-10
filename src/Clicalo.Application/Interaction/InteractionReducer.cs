using Clicalo.Domain.Dimming;
using Clicalo.Domain.PanelLayout;

namespace Clicalo.Application.Interaction;

/// <summary>
/// The only way an <see cref="InteractionState"/> changes (blueprint §6.4): a pure function covered by a table of
/// transitions. An action that changes nothing returns the same instance.
/// </summary>
public static class InteractionReducer
{
    /// <summary>Applies <paramref name="action"/> to <paramref name="state"/> at <paramref name="now"/>.</summary>
    /// <param name="state">The current state.</param>
    /// <param name="action">The intention.</param>
    /// <param name="now">The current time (the dimming counts from the last leave).</param>
    /// <returns>The next state, or <paramref name="state"/> itself when nothing changes.</returns>
    public static InteractionState Reduce(
        InteractionState state,
        InteractionAction action,
        DateTimeOffset now
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(action);
        var next = action switch
        {
            // «−» on the Full view of a search from the bar only ends it: the Tab view has no bubble (PAN-001).
            InteractionAction.Minimize => state.SearchPeek
                ? state with
                {
                    SearchPeek = false,
                }
                : state with
                {
                    Minimized = true,
                    Flyout = DockFlyout.None,
                },
            InteractionAction.PeekSearch => state with
            {
                SearchPeek = true,
                Flyout = DockFlyout.None,
            },
            InteractionAction.EndSearchPeek => state with { SearchPeek = false },
            InteractionAction.Restore => state with { Minimized = false },
            InteractionAction.OpenDock => state with { DockOpen = true },
            InteractionAction.CloseDock => state with
            {
                DockOpen = false,
                Flyout = DockFlyout.None,
            },
            InteractionAction.ToggleFlyout toggle => state.DockOpen
                ? state with
                {
                    Flyout = state.Flyout == toggle.Flyout ? DockFlyout.None : toggle.Flyout,
                }
                : state,
            InteractionAction.CloseFlyout => state with { Flyout = DockFlyout.None },
            InteractionAction.ViewChanged => state with
            {
                SearchPeek = false,
                Minimized = false,
                DockOpen = false,
                Flyout = DockFlyout.None,
            },
            InteractionAction.CoachNext => state with
            {
                CoachStep = DockRules.NextCoachStep(state.CoachStep) ?? 0,
            },
            InteractionAction.CoachReset => state with { CoachStep = 0 },
            InteractionAction.SetOpen open => state with
            {
                Open = open.IsOpen ? state.Open | open.What : state.Open & ~open.What,
            },
            InteractionAction.PointerEntered => state with { PointerInside = true },
            InteractionAction.PointerLeft => state with { PointerInside = false, LastLeave = now },
            InteractionAction.SurfaceShown => state.PointerInside
                ? state
                : state with
                {
                    LastLeave = now,
                },
            InteractionAction.PostNotice post => state with
            {
                Notices = state.Notices.Post(post.Notice, post.Duration, now),
            },
            InteractionAction.ShowStickyNotice sticky => state with
            {
                Notices = state.Notices.ShowSticky(sticky.Owner, sticky.Notice, now),
            },
            InteractionAction.ClearStickyNotice clear => state with
            {
                Notices = state.Notices.ClearSticky(clear.Owner),
            },
            InteractionAction.DismissNotices dismiss => state with
            {
                Notices = state.Notices.Dismiss(dismiss.Kind, now),
            },
            InteractionAction.NoticeTick => state with { Notices = state.Notices.Advance(now) },
            _ => throw new ArgumentOutOfRangeException(
                nameof(action),
                action.GetType().Name,
                "Unknown interaction action."
            ),
        };

        // Once the last exception closes, the surfaces wait the whole delay again before dimming: «Release all» never
        // fades while it is needed, nor right after it (SEG-002).
        if (
            next.Exceptions == DimExceptions.None
            && state.Exceptions != DimExceptions.None
            && next.LastLeave is not null
            && !next.PointerInside
        )
        {
            next = next with { LastLeave = now };
        }

        return next == state ? state : next with { Version = state.Version + 1 };
    }
}
