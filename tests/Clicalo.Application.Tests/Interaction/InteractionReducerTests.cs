using Clicalo.Application.Interaction;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Application.Tests.Interaction;

/// <summary>
/// <see cref="InteractionReducer"/> and <see cref="InteractionStore"/>: the forms that are not settings (bubble, bar,
/// side windows, guide) and the dimming with every exception of docs/04 «Opacidad y atenuado» (blueprint §6.4).
/// </summary>
public sealed class InteractionReducerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DimSettings Dim = new(AutoDim: true, Opacity: 0.92, DimTo: 0.35);

    /// <summary>Every exception of docs/04, one by one.</summary>
    public static TheoryData<DimExceptions> Exceptions =>
        [
            DimExceptions.Panic,
            DimExceptions.QuickSettings,
            DimExceptions.ContextMenu,
            DimExceptions.ProfileGrid,
            DimExceptions.Search,
            DimExceptions.EditMode,
            DimExceptions.ControlCenterOpen,
            DimExceptions.WelcomeOpen,
        ];

    /// <summary>Every surface that dims.</summary>
    public static TheoryData<DimSurface> Surfaces =>
        [DimSurface.Panel, DimSurface.Dock, DimSurface.DockHandle, DimSurface.Bubble];

    [Fact]
    [Trait("Req", "PAN-001")]
    public void Minimize_and_restore_go_to_the_bubble_and_back()
    {
        var bubble = Reduce(InteractionState.Initial, new InteractionAction.Minimize());
        bubble.Minimized.ShouldBeTrue();
        bubble.Version.ShouldBe(1);

        Reduce(bubble, new InteractionAction.Restore()).Minimized.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PAN-001")]
    public void A_view_change_leaves_the_bubble_folds_the_bar_and_closes_its_windows()
    {
        var open = Reduce(
            Reduce(InteractionState.Initial, new InteractionAction.OpenDock()),
            new InteractionAction.ToggleFlyout(DockFlyout.Pinned)
        ) with
        {
            Minimized = true,
        };

        var changed = Reduce(open, new InteractionAction.ViewChanged());

        changed.Minimized.ShouldBeFalse();
        changed.DockOpen.ShouldBeFalse();
        changed.Flyout.ShouldBe(DockFlyout.None);
    }

    [Fact]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-008")]
    public void One_side_window_at_a_time_and_only_with_the_bar_open()
    {
        Reduce(InteractionState.Initial, new InteractionAction.ToggleFlyout(DockFlyout.Pinned))
            .ShouldBeSameAs(InteractionState.Initial);

        var open = Reduce(InteractionState.Initial, new InteractionAction.OpenDock());
        var pinned = Reduce(open, new InteractionAction.ToggleFlyout(DockFlyout.Pinned));
        pinned.Flyout.ShouldBe(DockFlyout.Pinned);
        Reduce(pinned, new InteractionAction.ToggleFlyout(DockFlyout.Sticky))
            .Flyout.ShouldBe(DockFlyout.Sticky);
        Reduce(pinned, new InteractionAction.ToggleFlyout(DockFlyout.Pinned))
            .Flyout.ShouldBe(DockFlyout.None);

        var closed = Reduce(pinned, new InteractionAction.CloseDock());
        closed.DockOpen.ShouldBeFalse();
        closed.Flyout.ShouldBe(DockFlyout.None);
    }

    [Fact]
    [Trait("Req", "PES-015")]
    public void The_guide_walks_its_three_steps_and_starts_again()
    {
        var state = InteractionState.Initial;
        state = Reduce(state, new InteractionAction.CoachNext());
        state.CoachStep.ShouldBe(1);
        state = Reduce(state, new InteractionAction.CoachNext());
        state.CoachStep.ShouldBe(2);
        Reduce(state, new InteractionAction.CoachNext()).CoachStep.ShouldBe(0);
        Reduce(state, new InteractionAction.CoachReset()).CoachStep.ShouldBe(0);
    }

    [Fact]
    public void An_action_without_effect_returns_the_same_state()
    {
        var state = InteractionState.Initial;

        Reduce(state, new InteractionAction.Restore()).ShouldBeSameAs(state);
        Reduce(state, new InteractionAction.SetOpen(DimExceptions.Search, false))
            .ShouldBeSameAs(state);
    }

    [Theory]
    [MemberData(nameof(Exceptions))]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "SEG-002")]
    public void Nothing_dims_while_an_exception_is_open(DimExceptions exception)
    {
        var (store, clock) = Store();
        _ = store.Dispatch(new InteractionAction.PointerLeft());
        _ = store.Dispatch(new InteractionAction.SetOpen(exception, true));
        clock.Advance(Timings.Dimming.DimDelay * 4);

        foreach (var surface in new[] { DimSurface.Panel, DimSurface.Dock, DimSurface.DockHandle })
        {
            store
                .Dim(surface, Dim, reduceMotion: false, highContrast: false)
                .Dimmed.ShouldBeFalse();
        }
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "PES-010")]
    public void A_window_beside_the_bar_keeps_every_surface_awake()
    {
        var (store, clock) = Store();
        _ = store.Dispatch(new InteractionAction.OpenDock());
        _ = store.Dispatch(new InteractionAction.ToggleFlyout(DockFlyout.Profiles));
        _ = store.Dispatch(new InteractionAction.PointerLeft());
        clock.Advance(Timings.Dimming.DimDelay * 4);

        store.Current.Exceptions.ShouldBe(DimExceptions.DockSideWindows);
        store.Dim(DimSurface.Dock, Dim, false, false).Dimmed.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    [Trait("Req", "GEN-009")]
    public void Every_surface_dims_two_and_a_half_seconds_after_the_finger_leaves(
        DimSurface surface
    )
    {
        var (store, clock) = Store();
        _ = store.Dispatch(new InteractionAction.PointerEntered());
        _ = store.Dispatch(new InteractionAction.PointerLeft());

        store.Dim(surface, Dim, false, false).Dimmed.ShouldBeFalse();
        clock.Advance(Timings.Dimming.DimDelay);
        var dimmed = store.Dim(surface, Dim, false, false);

        dimmed.Dimmed.ShouldBeTrue();
        dimmed.TargetOpacity.ShouldBe(
            surface is DimSurface.Bubble or DimSurface.DockHandle
                ? Timings.Dimming.BubbleMinOpacity
                : Dim.DimTo
        );
    }

    [Fact]
    [Trait("Req", "BUR-002")]
    [Trait("Req", "SEG-002")]
    public void With_panic_the_bubble_is_at_full_opacity_and_after_it_the_delay_starts_again()
    {
        var (store, clock) = Store();
        _ = store.Dispatch(new InteractionAction.PointerLeft());
        _ = store.Dispatch(new InteractionAction.SetOpen(DimExceptions.Panic, true));
        clock.Advance(Timings.Dimming.DimDelay * 2);

        store.Dim(DimSurface.Bubble, Dim, false, false).TargetOpacity.ShouldBe(1);
        _ = store.Dispatch(new InteractionAction.SetOpen(DimExceptions.Panic, false));
        store.Dim(DimSurface.Panel, Dim, false, false).Dimmed.ShouldBeFalse();
        clock.Advance(Timings.Dimming.DimDelay);
        store.Dim(DimSurface.Panel, Dim, false, false).Dimmed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "TEM-006")]
    public void The_change_lasts_350_ms_or_nothing_with_reduce_motion()
    {
        var (store, _) = Store();

        store
            .Dim(DimSurface.Panel, Dim, false, false)
            .Transition.ShouldBe(Timings.Dimming.DimTransition);
        store.Dim(DimSurface.Panel, Dim, true, false).Transition.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    public void A_surface_that_appears_is_awake_until_the_delay_passes()
    {
        var (store, clock) = Store();
        _ = store.Dispatch(new InteractionAction.SurfaceShown());

        store
            .Dim(DimSurface.Panel, Dim, false, false)
            .NextEvaluationAt.ShouldBe(clock.GetUtcNow() + Timings.Dimming.DimDelay);
    }

    [Fact]
    public void Only_the_thread_that_created_the_store_writes_it()
    {
        var (store, _) = Store();
        Exception? failure = null;
        var other = new Thread(() =>
            failure = Record.Exception(() => store.Dispatch(new InteractionAction.Minimize()))
        );

        other.Start();
        other.Join();

        failure.ShouldBeOfType<InvalidOperationException>();
        store.Current.Minimized.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void The_notices_change_only_through_the_reducer_with_its_clock()
    {
        var owner = new object();
        var sent = new Notice(L.Released, new IconRef("info"));
        var hint = new Notice(L.EditHint, new IconRef("edit"));
        var undo = new Notice(L.Deleted, new IconRef("delete"), Kind: NoticeKind.Undo);
        var duration = Timings.Notices.NoticeDuration;

        var posted = Reduce(
            InteractionState.Initial,
            new InteractionAction.PostNotice(sent, duration)
        );
        posted.Notices.Shown.ShouldBe(sent);
        posted.Notices.EndsAt.ShouldBe(Now + duration);
        posted.Version.ShouldBe(1);

        // A tick before its end changes nothing; at its end the bar rests.
        Reduce(posted, new InteractionAction.NoticeTick()).ShouldBeSameAs(posted);
        InteractionReducer
            .Reduce(posted, new InteractionAction.NoticeTick(), Now + duration)
            .Notices.Shown.ShouldBeNull();

        var sticky = Reduce(posted, new InteractionAction.ShowStickyNotice(owner, hint));
        sticky.Notices.Shown.ShouldBe(hint);
        Reduce(sticky, new InteractionAction.ShowStickyNotice(owner, hint)).ShouldBeSameAs(sticky);
        Reduce(sticky, new InteractionAction.ClearStickyNotice(owner)).Notices.Shown.ShouldBeNull();

        var withUndo = Reduce(sticky, new InteractionAction.PostNotice(undo, duration));
        withUndo.Notices.Shown.ShouldBe(undo);
        Reduce(withUndo, new InteractionAction.DismissNotices(NoticeKind.Undo))
            .Notices.Shown.ShouldBe(hint);
    }

    [Fact]
    [Trait("Req", "AVI-002")]
    public void A_notice_never_wakes_or_dims_the_surfaces()
    {
        var notice = new Notice(L.Released, new IconRef("info"));
        var posted = Reduce(
            InteractionState.Initial,
            new InteractionAction.PostNotice(notice, Timings.Notices.NoticeDuration)
        );

        posted.Exceptions.ShouldBe(DimExceptions.None);
        posted.LastLeave.ShouldBeNull();
        posted.PointerInside.ShouldBeFalse();
    }

    private static InteractionState Reduce(InteractionState state, InteractionAction action) =>
        InteractionReducer.Reduce(state, action, Now);

    private static (InteractionStore Store, FakeTimeProvider Clock) Store()
    {
        var clock = new FakeTimeProvider(Now);
        return (new InteractionStore(InteractionState.Initial, clock), clock);
    }
}
