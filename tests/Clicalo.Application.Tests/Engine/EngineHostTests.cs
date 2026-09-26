using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// The engine actor (blueprint Â§7.3, ADR-0004): two lanes, timers on the time provider, the heartbeat, coalesced
/// snapshots, the host's own checks before a press, failures back to the reducer, the fence and the emergency path
/// of an exception (NFR-005).
/// </summary>
[Trait("Req", "SEG-007")]
public sealed class EngineHostTests
{
    [Fact]
    [Trait("Req", "SEG-003")]
    public void The_priority_lane_is_handled_before_the_normal_one()
    {
        using var world = new HostWorld();
        world.Host.Post(new EngineEvent.TimerFired(new TimerKey("macro")));
        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));

        world.Host.Pump();

        world.Seen[0].ShouldBeOfType<EngineEvent.ReleaseAll>();
        world.Seen[1].ShouldBeOfType<EngineEvent.TimerFired>();
    }

    [Fact]
    public void A_press_for_the_current_foreground_goes_to_the_injector_with_the_generation()
    {
        using var world = new HostWorld();

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Press());

        world.Injector.Batches.ShouldHaveSingleItem().Generation.ShouldBe(HostWorld.Generation);
    }

    [Fact]
    public void A_press_for_another_epoch_is_never_sent_and_comes_back_as_a_failure()
    {
        using var world = new HostWorld();

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Press(epoch: 2));
        world.Host.Pump();

        world.Injector.Batches.ShouldBeEmpty();
        world
            .Seen.OfType<EngineEvent.InjectFailed>()
            .ShouldHaveSingleItem()
            .Effect.ShouldBe(new EffectId(42));
    }

    [Fact]
    [Trait("Req", "EJE-013")]
    public void A_press_for_an_elevated_target_is_never_sent()
    {
        using var world = new HostWorld(
            EngineState.Empty with
            {
                Foreground = HostWorld.Notepad with { Elevation = ElevationState.TargetElevated },
            }
        );

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Press());

        world.Injector.Batches.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "TAC-008")]
    public void Nothing_but_releases_goes_out_in_test_mode()
    {
        using var world = new HostWorld(
            HostWorld.WithForeground(EngineState.Empty with { TestMode = true })
        );

        world.Handle(
            new EngineEvent.SessionResumed(),
            HostWorld.Press(),
            new EngineEffect.TypeText(new EffectId(1), SecretText.From("x"), 3, null),
            new EngineEffect.MouseAction(MouseOp.RightClick, ScrollSpeed.Normal, null, 3),
            HostWorld.Release()
        );

        world.Injector.Batches.ShouldHaveSingleItem().Events.ShouldAllBe(e => e.IsRelease);
        world.Injector.Texts.ShouldBeEmpty();
        world.Injector.MouseActions.ShouldBeEmpty();
    }

    [Fact]
    public void A_release_goes_out_whatever_the_foreground()
    {
        using var world = new HostWorld(EngineState.Empty);

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Release());

        world.Injector.Batches.ShouldHaveSingleItem();
    }

    [Fact]
    public void A_press_send_input_refuses_comes_back_as_a_failure()
    {
        using var world = new HostWorld();
        world.Injector.NextStatus = InjectionStatus.Failed;

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Press());
        world.Host.Pump();

        world
            .Seen.OfType<EngineEvent.InjectFailed>()
            .ShouldHaveSingleItem()
            .Win32Error.ShouldBe(87);
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void A_release_the_secure_desktop_refuses_comes_back_to_be_sent_again()
    {
        using var world = new HostWorld();
        world.Injector.NextStatus = InjectionStatus.Blocked;

        world.Handle(new EngineEvent.Terminal(TerminalReason.Lock), HostWorld.Release());
        world.Host.Pump();

        world
            .Seen.OfType<EngineEvent.ReleasesBlocked>()
            .ShouldHaveSingleItem()
            .Events.Length.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void A_release_send_input_takes_only_in_part_comes_back_to_be_sent_again()
    {
        using var world = new HostWorld();
        world.Injector.NextStatus = InjectionStatus.Failed;

        world.Handle(new EngineEvent.Terminal(TerminalReason.Lock), HostWorld.Release());
        world.Host.Pump();

        world
            .Seen.OfType<EngineEvent.ReleasesBlocked>()
            .ShouldHaveSingleItem()
            .Events.ShouldBe([InjectedEvent.KeyUp(HostWorld.Ctrl)]);
        world.Seen.OfType<EngineEvent.InjectFailed>().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public async Task A_fenced_loop_leaves_the_engine_alive_mark_to_the_engine_that_replaced_it()
    {
        using var world = new HostWorld();
        world.Injector.NextStatus = InjectionStatus.Fenced;
        world.Answers.Enqueue([HostWorld.Press()]);
        using var stop = new CancellationTokenSource();
        var engine = world.Host.StartOnDedicatedThread(stop.Token);

        world.Host.Post(new EngineEvent.SessionResumed());
        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        world.Host.IsStopped.ShouldBeTrue();
        world.Ledger.Marks.HasFlag(KeyLedgerMarks.EngineAlive).ShouldBeTrue();
        await stop.CancelAsync();
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void A_fenced_host_stops_and_sends_nothing_more()
    {
        using var world = new HostWorld();
        world.Injector.NextStatus = InjectionStatus.Fenced;

        world.Handle(new EngineEvent.SessionResumed(), HostWorld.Press(), HostWorld.Press());

        world.Host.IsStopped.ShouldBeTrue();
        world.Injector.Batches.Count.ShouldBe(1);
        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));
        world.Host.Pump();
        world.Seen.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void Text_is_revealed_only_to_the_injector_and_the_clipboard()
    {
        using var world = new HostWorld();

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.TypeText(
                new EffectId(1),
                SecretText.From("Â¡Hola, Ã±andÃº!"),
                3,
                null
            ),
            new EngineEffect.ClipboardPaste(new EffectId(2), SecretText.From("pegar"), 3)
        );

        world.Injector.Texts.ShouldBe(["Â¡Hola, Ã±andÃº!"]);
        var paste = world.Shell.Pastes.ShouldHaveSingleItem();
        paste.Text.ShouldBe("pegar");
        paste.ReplyTo.ShouldBeSameAs(world.Host);
    }

    [Fact]
    [Trait("Req", "EJE-011")]
    public void Launches_and_system_commands_go_to_the_shell_thread_with_the_generation()
    {
        using var world = new HostWorld();

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.Launch(
                new EffectId(1),
                new LaunchRequest.OpenUrl(new Uri("https://example.com"))
            ),
            new EngineEffect.SystemCommand(new EffectId(2), new SystemCommandId("lock"))
        );

        var launch = world.Shell.Launches.ShouldHaveSingleItem();
        launch.Generation.ShouldBe(HostWorld.Generation);
        launch.ReplyTo.ShouldBeSameAs(world.Host);
        world.Shell.Commands.ShouldHaveSingleItem().Command.ShouldBe(new SystemCommandId("lock"));
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    public void Mouse_actions_go_to_the_injector_at_their_point()
    {
        using var world = new HostWorld();

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.MouseAction(
                MouseOp.RightClick,
                ScrollSpeed.Normal,
                new PhysicalPoint(10, 20),
                3
            )
        );

        world.Injector.MouseActions.ShouldBe([
            (MouseOp.RightClick, (PhysicalPoint?)new PhysicalPoint(10, 20)),
        ]);
    }

    [Fact]
    [Trait("Req", "EJE-010")]
    public void A_timer_fires_on_the_time_provider_and_never_early()
    {
        using var world = new HostWorld();
        var due = world.Time.GetTimestamp() + TimeSpan.FromMilliseconds(500).Ticks;

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.Schedule(new TimerKey("macro"), due)
        );
        world.Time.Advance(TimeSpan.FromMilliseconds(499));
        world.Host.Pump();
        world.Seen.OfType<EngineEvent.TimerFired>().ShouldBeEmpty();

        world.Time.Advance(TimeSpan.FromMilliseconds(1));
        world.Host.Pump();
        world
            .Seen.OfType<EngineEvent.TimerFired>()
            .ShouldHaveSingleItem()
            .Key.ShouldBe(new TimerKey("macro"));
    }

    [Fact]
    public void A_cancelled_timer_never_fires()
    {
        using var world = new HostWorld();
        var due = world.Time.GetTimestamp() + TimeSpan.FromMilliseconds(100).Ticks;

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.Schedule(new TimerKey("scroll"), due)
        );
        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.CancelTimer(new TimerKey("scroll"))
        );
        world.Time.Advance(TimeSpan.FromSeconds(1));
        world.Host.Pump();

        world.Seen.OfType<EngineEvent.TimerFired>().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "REG-03")]
    public void The_heartbeat_is_written_on_every_turn_that_moved_the_clock()
    {
        using var world = new HostWorld();

        world.Host.Pump();
        world.Time.Advance(Timings.Engine.LedgerHeartbeatInterval);
        world.Host.Pump();

        world.Ledger.Heartbeats.Count.ShouldBe(2);
        (world.Ledger.Heartbeats[1] - world.Ledger.Heartbeats[0]).ShouldBe(
            Timings.Engine.LedgerHeartbeatInterval.Ticks
        );
    }

    [Fact]
    [Trait("Req", "NFR-001")]
    public void Snapshots_reach_the_ui_at_most_once_per_frame()
    {
        using var world = new HostWorld(HostWorld.HoldingShift());

        world.Handle(new EngineEvent.SessionResumed());
        world.Handle(new EngineEvent.SessionResumed());
        world.Observer.Snapshots.Count.ShouldBe(1);
        world
            .Observer.Snapshots[0]
            .Held.ShouldHaveSingleItem()
            .Holder.ShouldBe(HolderId.ForContact(1));

        world.Time.Advance(Timings.Engine.SnapshotCoalescing);
        world.Host.Pump();
        world.Observer.Snapshots.Count.ShouldBe(2);
        world.Host.Snapshot.Version.ShouldBe(world.Host.State.Version);
    }

    [Fact]
    [Trait("Req", "NFR-005")]
    public void An_exception_releases_what_was_held_resets_the_state_and_says_so()
    {
        using var world = new HostWorld(HostWorld.HoldingShift())
        {
            ThrowOn = typeof(EngineEvent.SessionResumed),
        };

        world.Handle(new EngineEvent.SessionResumed());

        world
            .Injector.Batches.ShouldHaveSingleItem()
            .Events.ShouldBe([InjectedEvent.KeyUp(HostWorld.Shift)]);
        world.Host.State.IsQuiet.ShouldBeTrue();
        world.Host.State.Foreground.ShouldBe(HostWorld.Notepad);
        world.Observer.Notices.ShouldHaveSingleItem().Urgency.ShouldBe(NoticeUrgency.Assertive);
        world.Host.IsStopped.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-005")]
    public void An_exception_releases_from_the_physical_ledger_when_it_can()
    {
        var released = new List<EngineGeneration>();
        using var world = new HostWorld(
            HostWorld.HoldingShift(),
            releaseRecorded: generation =>
            {
                released.Add(generation);
                return true;
            }
        )
        {
            ThrowOn = typeof(EngineEvent.SessionResumed),
        };

        world.Handle(new EngineEvent.SessionResumed());

        released.ShouldBe([HostWorld.Generation]);
        world.Injector.Batches.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "FRE-002")]
    [Trait("Req", "AVI-004")]
    public void Notices_usage_and_the_last_action_reach_the_observer()
    {
        using var world = new HostWorld();
        var id = new ShortcutId("copy");

        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.Notice(L.ReleasedAll, NoticeUrgency.Polite),
            new EngineEffect.CountUsage(id, world.Time.GetUtcNow()),
            new EngineEffect.SetLastAction(id)
        );

        world.Observer.Notices.ShouldHaveSingleItem();
        world.Observer.Usage.ShouldBe([id]);
        world.Observer.LastActions.ShouldBe([id]);
    }

    [Theory]
    [InlineData(TerminalReason.Exit, KeyLedgerMarks.CleanShutdown)]
    [InlineData(TerminalReason.SessionEnd, KeyLedgerMarks.CleanShutdown)]
    [InlineData(TerminalReason.Relaunch, KeyLedgerMarks.CleanShutdown | KeyLedgerMarks.NoRelaunch)]
    [InlineData(TerminalReason.Update, KeyLedgerMarks.CleanShutdown | KeyLedgerMarks.NoRelaunch)]
    [InlineData(TerminalReason.Lock, KeyLedgerMarks.None)]
    [Trait("Req", "SEG-006")]
    public void Terminal_events_leave_the_marks_sentinel_reads(
        TerminalReason reason,
        KeyLedgerMarks marks
    )
    {
        using var world = new HostWorld();

        world.Handle(new EngineEvent.Terminal(reason));

        world.Ledger.Marks.ShouldBe(marks);
        world.Host.IsStopped.ShouldBe(reason == TerminalReason.Exit);
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public async Task The_loop_marks_the_engine_alive_and_cancelling_it_releases_everything()
    {
        using var world = new HostWorld(HostWorld.HoldingShift(), realReducer: true);
        using var stop = new CancellationTokenSource();
        var engine = world.Host.StartOnDedicatedThread(stop.Token);
        await WaitUntil(() => world.Ledger.Marks.HasFlag(KeyLedgerMarks.EngineAlive));
        await stop.CancelAsync();
        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        world.Ledger.Marks.HasFlag(KeyLedgerMarks.EngineAlive).ShouldBeFalse();
        world.Ledger.Marks.HasFlag(KeyLedgerMarks.CleanShutdown).ShouldBeTrue();
        world
            .Injector.Batches.SelectMany(static b => b.Events)
            .ShouldContain(InjectedEvent.KeyUp(HostWorld.Shift));
        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User)).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SEG-003")]
    public async Task Release_all_posted_from_another_thread_reaches_the_running_engine()
    {
        using var world = new HostWorld(HostWorld.HoldingShift(), realReducer: true);
        using var stop = new CancellationTokenSource();
        var engine = world.Host.StartOnDedicatedThread(stop.Token);

        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User)).ShouldBeTrue();
        await WaitUntil(() => world.Injector.Batches.Count > 0);
        world.Host.Post(new EngineEvent.Terminal(TerminalReason.Exit));

        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        world.Injector.Batches[0].Events.ShouldBe([InjectedEvent.KeyUp(HostWorld.Shift)]);
        world.Observer.Notices.ShouldNotBeEmpty();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), timeout.Token);
        }
    }
}
