using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// The generation fence over the host's own writes to the ledger (blueprint §3.2, rule 6; INV-11): a host an emergency
/// replaced, and that resumes with nothing to send, learns it from the heartbeat. It never renews the heartbeat of the
/// engine that replaced it (which would hide a hang of that engine from the emergency releaser), never touches its
/// marks and never publishes to the observer they share.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-007")]
public sealed class EngineHostFenceTests
{
    [Fact]
    public void A_host_that_resumes_after_an_emergency_never_renews_the_heartbeat_and_stops()
    {
        using var world = new HostWorld(HostWorld.HoldingShift());
        world.Host.Pump();
        world.Ledger.Heartbeats.Count.ShouldBe(1);
        var snapshots = world.Observer.Snapshots.Count;

        // The emergency raised the generation while this host was hung outside the gate.
        world.Ledger.CurrentGeneration = new EngineGeneration(HostWorld.Generation.Value + 1);
        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));
        for (var i = 0; i < 8; i++)
        {
            world.Time.Advance(Timings.Engine.LedgerHeartbeatInterval);
            world.Host.Pump();
        }

        world.Ledger.Heartbeats.Count.ShouldBe(1);
        world.Host.IsStopped.ShouldBeTrue();
        world.Seen.ShouldBeEmpty();
        world.Injector.Batches.ShouldBeEmpty();
        world.Observer.Snapshots.Count.ShouldBe(snapshots);
    }

    [Fact]
    public void A_host_that_resumes_after_an_emergency_runs_none_of_its_timers()
    {
        using var world = new HostWorld();
        world.Handle(
            new EngineEvent.SessionResumed(),
            new EngineEffect.Schedule(new TimerKey("macro"), world.Time.GetTimestamp() + 1)
        );
        world.Seen.Clear();

        world.Ledger.CurrentGeneration = new EngineGeneration(HostWorld.Generation.Value + 1);
        world.Time.Advance(Timings.Engine.LedgerHeartbeatInterval);
        world.Host.Pump();

        world.Seen.ShouldBeEmpty();
        world.Host.IsStopped.ShouldBeTrue();
    }

    [Fact]
    public async Task A_host_cancelled_after_an_emergency_neither_marks_a_clean_shutdown_nor_clears_the_alive_mark()
    {
        using var world = new HostWorld(realReducer: true);
        using var stop = new CancellationTokenSource();
        var engine = world.Host.StartOnDedicatedThread(stop.Token);
        await WaitUntil(() => world.Ledger.Marks.HasFlag(KeyLedgerMarks.EngineAlive));

        // The emergency's new engine owns the ledger (and its EngineAlive mark) from now on.
        world.Ledger.CurrentGeneration = new EngineGeneration(HostWorld.Generation.Value + 1);
        await stop.CancelAsync();
        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        world.Ledger.Marks.ShouldBe(KeyLedgerMarks.EngineAlive);
        world.Host.IsStopped.ShouldBeTrue();
    }

    [Fact]
    public void A_host_born_after_its_generation_was_raised_never_runs()
    {
        using var world = new HostWorld();
        world.Ledger.CurrentGeneration = new EngineGeneration(HostWorld.Generation.Value + 1);
        world.Host.Post(new EngineEvent.ReleaseAll(ReleaseReason.User));

        // A host that were not fenced would wait for events until the token ends.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken
        );
        limit.CancelAfter(TimeSpan.FromSeconds(5));
        world.Host.Run(limit.Token);

        world.Seen.ShouldBeEmpty();
        world.Ledger.Marks.ShouldBe(KeyLedgerMarks.None);
        world.Ledger.Heartbeats.ShouldBeEmpty();
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
