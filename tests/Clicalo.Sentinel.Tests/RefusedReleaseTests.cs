using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// User decision D3 of 2026-10-03 (ADR-0018): when Clícalo dies with the session locked and a key down, Sentinel
/// resends the release every heartbeat until the desktop accepts it (at the unlock) and only then relaunches Clícalo.
/// A refusal by the secure desktop is retried without limit; any other one, for at most <c>RefusedReleaseWait</c>.
/// Over a desktop that refuses and then accepts, with a fake clock: nothing is injected.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
[Trait("Req", "REG-03")]
public sealed class RefusedReleaseTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);
    private static readonly DateTimeOffset DiedAt = new(2026, 10, 3, 22, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Heartbeat = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan RefusedReleaseWait = TimeSpan.FromSeconds(30);

    private static readonly SentinelStartInfo Info = new(
        1,
        2,
        3,
        Heartbeat,
        3,
        TimeSpan.FromMinutes(10),
        RefusedReleaseWait
    );

    private static readonly LowLevelInput[] ReleaseCtrlShift =
    [
        LowLevelInput.KeyUp(Shift),
        LowLevelInput.KeyUp(Ctrl),
    ];

    private static (KeyLedgerSection Engine, KeyLedgerSection View) HoldingCtrlShift(
        LedgerMarks marks = LedgerMarks.EngineAlive
    )
    {
        var engine = KeyLedgerSection.CreateInMemory();
        foreach (var key in new[] { Ctrl, Shift })
        {
            engine.TryBeginDown(key, out var slot);
            engine.CommitDown(slot);
        }

        engine.SetMarks(marks);
        return (engine, engine.ReadOnlyView());
    }

    private static (
        GuardianLoop Loop,
        FakeGuardianEnvironment Environment,
        List<string> Log
    ) Machine(KeyLedgerSection view, RefusingSender sender)
    {
        var log = new List<string>();
        var time = new FakeTimeProvider(DiedAt);
        sender.Log = log;
        var environment = new FakeGuardianEnvironment { Time = time, Log = log };
        return (new GuardianLoop(Info, view, sender, time, environment), environment, log);
    }

    [Fact]
    public void A_release_refused_while_the_session_is_locked_is_resent_every_heartbeat_and_only_then_clicalo_is_relaunched()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RefusingSender().LockedFor(5);
            var (loop, environment, log) = Machine(view, sender);

            var exit = loop.Run();

            // Without the retry there would be one refused batch and a relaunch with Ctrl and Shift still down.
            exit.ShouldBe(SentinelExitCode.ReleasedAndRelaunched);
            sender.Batches.Count.ShouldBe(6);
            sender.Batches.ShouldAllBe(batch => batch.SequenceEqual(ReleaseCtrlShift));
            environment.Waits.ShouldBe(Enumerable.Repeat(Heartbeat, 5));
            loop.ReleasedEvents.ShouldBe(2);
            loop.RefusedSends.ShouldBe(5);
            loop.GaveUpOnRefusal.ShouldBeFalse();
            log.ShouldBe([.. Enumerable.Repeat("refused", 5), "accepted", "relaunch"]);
        }
    }

    [Fact]
    public void A_lock_that_lasts_all_night_is_waited_for_without_limit()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            // Ten hours locked: far beyond RefusedReleaseWait, which never applies to the secure desktop.
            var heartbeats = (int)(TimeSpan.FromHours(10) / Heartbeat);
            var sender = new RefusingSender().LockedFor(heartbeats);
            var (loop, environment, log) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            loop.GaveUpOnRefusal.ShouldBeFalse();
            environment.Waits.Count.ShouldBe(heartbeats);
            log.IndexOf("accepted").ShouldBe(heartbeats);
            log.IndexOf("relaunch").ShouldBe(heartbeats + 1);
            log.Count.ShouldBe(heartbeats + 2);
        }
    }

    [Fact]
    [Trait("Req", "SIS-004")]
    public void The_relaunch_after_the_unlock_carries_the_time_of_the_death()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RefusingSender().LockedFor(600);
            var (loop, environment, _) = Machine(view, sender);

            loop.Run();

            environment
                .Relaunches.ShouldHaveSingleItem()
                .ShouldBe(CrashJournal.RelaunchArguments(DiedAt, safeMode: false));
        }
    }

    [Fact]
    public void A_refusal_that_is_not_the_secure_desktops_is_retried_for_at_most_the_wait_and_then_clicalo_comes_back()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            const int InvalidParameter = 87;
            var sender = new RefusingSender().RefuseWith(InvalidParameter, 1000);
            var (loop, environment, log) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            // Refused at 0 s, 1 s, … 30 s: 31 sends and 30 pauses, then the relaunch and nothing else.
            var pauses = (int)(RefusedReleaseWait / Heartbeat);
            loop.GaveUpOnRefusal.ShouldBeTrue();
            sender.Batches.Count.ShouldBe(pauses + 1);
            environment.Waits.Count.ShouldBe(pauses);
            log.Count.ShouldBe(pauses + 2);
            log[^1].ShouldBe("relaunch");
        }
    }

    [Fact]
    public void A_secure_desktop_refusal_restarts_the_limit_of_the_other_refusals()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            const int OtherError = 1;
            var sender = new RefusingSender()
                .RefuseWith(OtherError, 20)
                .LockedFor(100)
                .RefuseWith(OtherError, 20);
            var (loop, _, log) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            // Two runs of 20 other refusals, each shorter than 30 s: never given up, accepted at the end.
            loop.GaveUpOnRefusal.ShouldBeFalse();
            log.Count.ShouldBe(20 + 100 + 20 + 2);
            log[^2].ShouldBe("accepted");
            log[^1].ShouldBe("relaunch");
        }
    }

    [Fact]
    public void A_partial_send_resends_only_what_did_not_go()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RefusingSender().AcceptOnly(1);
            var (loop, _, log) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            sender.Batches.Count.ShouldBe(2);
            sender.Batches[0].ShouldBe(ReleaseCtrlShift);
            sender.Batches[1].ShouldBe([LowLevelInput.KeyUp(Ctrl)]);
            loop.ReleasedEvents.ShouldBe(2);
            log.ShouldBe(["refused", "accepted", "relaunch"]);
        }
    }

    [Fact]
    public void An_update_that_dies_locked_still_waits_to_release_and_relaunches_nothing()
    {
        var (engine, view) = HoldingCtrlShift(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch);
        using (engine)
        using (view)
        {
            var sender = new RefusingSender().LockedFor(3);
            var (loop, environment, _) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedWithoutRelaunch);

            sender.Batches.Count.ShouldBe(4);
            loop.ReleasedEvents.ShouldBe(2);
            environment.Relaunches.ShouldBeEmpty();
        }
    }

    [Fact]
    public void An_accepted_release_neither_pauses_nor_retries()
    {
        var (engine, view) = HoldingCtrlShift();
        using (engine)
        using (view)
        {
            var sender = new RefusingSender();
            var (loop, environment, log) = Machine(view, sender);

            loop.Run().ShouldBe(SentinelExitCode.ReleasedAndRelaunched);

            sender.Batches.ShouldHaveSingleItem();
            environment.Waits.ShouldBeEmpty();
            log.ShouldBe(["accepted", "relaunch"]);
        }
    }
}
