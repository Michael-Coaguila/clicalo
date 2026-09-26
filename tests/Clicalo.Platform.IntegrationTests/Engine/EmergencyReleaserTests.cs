using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The emergency of a hung engine (blueprint §3.2 rule 6, §7.6; REG-03): after <c>Timings.Engine.EngineStallThreshold</c>
/// without heartbeat it takes the gate, raises the generation, releases everything and starts a new engine; when the
/// gate is held, or on a second hang inside <c>Timings.Engine.EngineHangLoop</c>, it marks <c>EmergencyRestart</c> and
/// escalates to a process restart.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-007")]
public sealed class EmergencyReleaserTests
{
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);

    private sealed class World : IDisposable
    {
        public World()
        {
            Ledger.SetMarks(LedgerMarks.EngineAlive);
            Gate = new InjectionGate(Ledger, System);
            Gate.TryInject(1, [LowLevelInput.KeyDown(Shift)]);
            Releaser = new EmergencyReleaser(Gate, Time, Restarted.Add, () => Escalations++);
        }

        public FakeTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));

        public KeyLedgerSection Ledger { get; } = KeyLedgerSection.CreateInMemory();

        public PhysicalStateInjector System { get; } = new();

        public InjectionGate Gate { get; }

        public EmergencyReleaser Releaser { get; }

        public List<EngineGeneration> Restarted { get; } = [];

        public int Escalations { get; private set; }

        public void Dispose()
        {
            Releaser.Dispose();
            Ledger.Dispose();
        }
    }

    [Fact]
    public void A_beating_engine_is_left_alone()
    {
        using var world = new World();

        for (var i = 1; i <= 10; i++)
        {
            world.Ledger.WriteHeartbeat(i);
            world.Time.Advance(Timings.Engine.EngineStallThreshold);
            world.Releaser.Check().ShouldBeNull();
        }

        world.System.Keys.ShouldBe([Shift]);
    }

    [Fact]
    public void A_stalled_engine_is_fenced_everything_is_released_and_a_new_engine_starts()
    {
        using var world = new World();
        world.Releaser.Check();

        world.Time.Advance(Timings.Engine.EngineStallThreshold - TimeSpan.FromTicks(1));
        world.Releaser.Check().ShouldBeNull();
        world.Time.Advance(TimeSpan.FromTicks(1));
        world.Releaser.Check().ShouldBe(EmergencyOutcome.Released);

        world.System.IsEmpty.ShouldBeTrue();
        world.Restarted.ShouldBe([new EngineGeneration(2)]);
        world.Gate.TryInject(1, [LowLevelInput.KeyDown(Shift)]).Result.ShouldBe(GateResult.Fenced);
        world.Escalations.ShouldBe(0);
    }

    [Fact]
    public void A_second_hang_inside_the_window_restarts_the_process()
    {
        using var world = new World();
        world.Releaser.Check();
        world.Time.Advance(Timings.Engine.EngineStallThreshold);
        world.Releaser.Check().ShouldBe(EmergencyOutcome.Released);

        world.Time.Advance(Timings.Engine.EngineStallThreshold);
        world.Releaser.Check().ShouldBe(EmergencyOutcome.GateBusy);

        world.Escalations.ShouldBe(1);
        world.Ledger.Marks.HasFlag(LedgerMarks.EmergencyRestart).ShouldBeTrue();
        world.Ledger.Marks.HasFlag(LedgerMarks.CleanShutdown).ShouldBeFalse();
    }

    [Fact]
    public void An_engine_frozen_inside_send_input_escalates_to_a_restart()
    {
        using var world = new World();
        world.System.FreezeOnCall = 2;
        var engine = new Thread(() => world.Gate.TryInject(1, [LowLevelInput.KeyUp(Shift)]))
        {
            IsBackground = true,
        };
        engine.Start();
        world
            .System.Frozen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
            .ShouldBeTrue();
        world.Releaser.Check();

        world.Time.Advance(Timings.Engine.EngineStallThreshold);
        var outcome = world.Releaser.Check();

        outcome.ShouldBe(EmergencyOutcome.GateBusy);
        world.Escalations.ShouldBe(1);
        world.Ledger.Marks.HasFlag(LedgerMarks.EmergencyRestart).ShouldBeTrue();
        world.System.Resume.Set();
        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
    }

    [Fact]
    public void No_engine_running_means_no_emergency()
    {
        using var world = new World();
        world.Ledger.ClearMarks(LedgerMarks.EngineAlive);

        world.Time.Advance(TimeSpan.FromMinutes(1));

        world.Releaser.Check().ShouldBeNull();
        world.System.Keys.ShouldBe([Shift]);
    }
}
