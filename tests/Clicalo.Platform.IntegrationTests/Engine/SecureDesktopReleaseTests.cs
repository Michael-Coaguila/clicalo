using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Releases the secure desktop refuses without locking the session (UAC, Ctrl+Alt+Del; blueprint §7.6, INV-3, D-22):
/// the physical ledger keeps them pending and the gate sends them again under the fence, whoever refused them, as soon
/// as <see cref="InputDesktopWatch"/> sees the input desktop come back. Nothing is injected: the gate sends to a
/// <see cref="PhysicalStateInjector"/> and the desktop is a fake.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "REG-03")]
public sealed class SecureDesktopReleaseTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Alt = new(0xA4, 0x38, LedgerKeyAttributes.None);

    [Fact]
    public void The_releases_the_ledger_keeps_pending_go_again_with_the_menu_mask_and_free_their_slots()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Alt)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;
        gate.TryInject(1, [LowLevelInput.KeyUp(Alt), LowLevelInput.KeyUp(Ctrl)]);
        ledger.Snapshot().Slots.ShouldAllBe(static s => s.State == LedgerSlotState.ReleasePending);

        var outcome = gate.TryReleasePending(1, out var count);

        outcome.Result.ShouldBe(GateResult.Ran);
        count.ShouldBe(4);
        system
            .Batches[^1]
            .ShouldBe([
                LowLevelInput.KeyDown(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(Alt),
                LowLevelInput.KeyUp(Ctrl),
            ]);
        system.IsEmpty.ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [Fact]
    public void Only_pending_releases_go_again_and_a_key_still_held_is_left_alone()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Alt)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;
        gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);

        gate.TryReleasePending(1, out _).Result.ShouldBe(GateResult.Ran);

        system.Keys.ShouldBe([Alt]);
        ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Alt, LedgerSlotState.Down, 1)]);
        gate.TryReleasePending(1, out var count).Send.Sent.ShouldBe(0);
        count.ShouldBe(0);
    }

    [Fact]
    public void The_releases_an_emergency_could_not_send_go_again_with_the_new_generation_only()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;
        gate.TryEmergencyRelease(TimeSpan.FromMilliseconds(250), out var generation)
            .ShouldBe(EmergencyOutcome.Released);
        system.Keys.ShouldBe([Ctrl]);

        gate.TryReleasePending(1, out _).Result.ShouldBe(GateResult.Fenced);
        system.Keys.ShouldBe([Ctrl]);
        new GateInputInjector(gate)
            .ReleasePending(new EngineGeneration(generation))
            .Status.ShouldBe(InjectionStatus.Sent);

        system.IsEmpty.ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [Fact]
    public void A_key_pressed_again_while_its_release_is_pending_is_freed_by_its_holder_release()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;
        gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);

        // The desktop is back before SessionResumed: a holder presses Ctrl again, then the pending releases go.
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
        gate.TryReleasePending(1, out _).Result.ShouldBe(GateResult.Ran);
        gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);

        system.IsEmpty.ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBeEmpty();
        gate.TryInjectChord(1, [Ctrl, Alt]).Result.ShouldBe(GateResult.Ran);
        system
            .Batches[^1]
            .ShouldBe([
                LowLevelInput.KeyDown(Ctrl),
                LowLevelInput.KeyDown(Alt),
                LowLevelInput.KeyUp(Alt),
                LowLevelInput.KeyUp(Ctrl),
            ]);
    }

    [Fact]
    public void A_press_the_secure_desktop_refuses_keeps_the_release_of_that_key_pending()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;
        gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);
        system.TakeNext = 0;
        system.NextError = InjectionGate.AccessDenied;

        gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Ctrl)]);

        ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.ReleasePending, 1)]);
        gate.TryReleasePending(1, out var count).Result.ShouldBe(GateResult.Ran);
        count.ShouldBe(1);
        system.IsEmpty.ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }

    [Fact]
    public void Leaving_the_secure_desktop_tells_the_engine_at_once()
    {
        var world = new DesktopWorld { Reachable = true };

        world.Watch.OnDesktopSwitched();

        world.Inbox.Posted.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.SessionResumed>();
        world.Watch.IsWaiting.ShouldBeFalse();
    }

    [Fact]
    public void While_the_secure_desktop_is_in_front_it_is_checked_again_until_it_leaves()
    {
        var world = new DesktopWorld { Reachable = false };
        world.Watch.OnDesktopSwitched();
        world.Inbox.Posted.ShouldBeEmpty();

        world.Time.Advance(Timings.KeySafety.InputDesktopRecheck[0]);
        world.Inbox.Posted.ShouldBeEmpty();
        world.Reachable = true;
        world.Time.Advance(Timings.KeySafety.InputDesktopRecheck[1]);

        world.Inbox.Posted.ShouldHaveSingleItem().ShouldBeOfType<EngineEvent.SessionResumed>();
        world.Watch.IsWaiting.ShouldBeFalse();
        for (var i = 0; i < 100; i++)
        {
            world.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        world.Inbox.Posted.Count.ShouldBe(1);
    }

    [Fact]
    public void The_checks_after_one_switch_are_bounded()
    {
        var world = new DesktopWorld { Reachable = false };
        world.Watch.OnDesktopSwitched();

        for (var i = 0; i < 600; i++)
        {
            world.Time.Advance(TimeSpan.FromMilliseconds(100));
        }

        world.Checks.ShouldBe(1 + Timings.KeySafety.InputDesktopRecheck.Length);
        world.Watch.IsWaiting.ShouldBeFalse();
        world.Inbox.Posted.ShouldBeEmpty();

        // The switch back to Clícalo's desktop starts over.
        world.Reachable = true;
        world.Watch.OnDesktopSwitched();
        world.Inbox.Posted.ShouldHaveSingleItem();
    }

    private sealed class DesktopWorld
    {
        public DesktopWorld() =>
            Watch = new InputDesktopWatch(
                Inbox,
                () =>
                {
                    Checks++;
                    return Reachable;
                },
                Time
            );

        public FakeTimeProvider Time { get; } =
            new(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));

        public RecordingInbox Inbox { get; } = new();

        public InputDesktopWatch Watch { get; }

        public bool Reachable { get; set; }

        public int Checks { get; private set; }
    }

    private sealed class RecordingInbox : IEngineInbox
    {
        public List<EngineEvent> Posted { get; } = [];

        public bool Post(EngineEvent engineEvent)
        {
            Posted.Add(engineEvent);
            return true;
        }
    }
}
