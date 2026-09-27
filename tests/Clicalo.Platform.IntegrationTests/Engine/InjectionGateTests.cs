using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The generation fence and the write-ahead protocol (blueprint §3.2 rule 6, §7.4, ADR-0004) over the real gate and an
/// in-memory ledger: every key down is recorded before <c>SendInput</c> (INV-2), what went is committed after, a
/// fenced generation sends nothing (INV-11).
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-007")]
public sealed class InjectionGateTests
{
    private static readonly PhysicalKey Ctrl = new(0xA2, 0x1D, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);
    private static readonly PhysicalKey Alt = new(0xA4, 0x38, LedgerKeyAttributes.None);
    private static readonly PhysicalKey C = new(0x43, 0x2E, LedgerKeyAttributes.None);

    private static (
        KeyLedgerSection Ledger,
        PhysicalStateInjector Sender,
        InjectionGate Gate
    ) Create()
    {
        var ledger = KeyLedgerSection.CreateInMemory();
        var sender = new PhysicalStateInjector();
        return (ledger, sender, new InjectionGate(ledger, sender));
    }

    private static LowLevelInput[] Tap(params PhysicalKey[] keys) =>
        [.. keys.Select(LowLevelInput.KeyDown), .. keys.Reverse().Select(LowLevelInput.KeyUp)];

    [Fact]
    public void Every_key_down_is_recorded_before_it_is_sent()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            sender.BeforeApply = (_, batch) =>
            {
                var recorded = ledger.Snapshot().Slots.Select(static s => s.Key).ToHashSet();
                foreach (var input in batch.Span)
                {
                    if (input.Kind == LowLevelInputKind.KeyDown)
                    {
                        recorded.ShouldContain(input.Key);
                    }
                }
            };

            gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Shift)])
                .Result.ShouldBe(GateResult.Ran);

            ledger.Snapshot().Slots.ShouldAllBe(slot => slot.State == LedgerSlotState.Down);
            sender.Keys.ShouldBe([Ctrl, Shift], ignoreOrder: true);
        }
    }

    [Fact]
    public void A_tap_in_one_batch_leaves_the_ledger_empty()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(1, Tap(Ctrl, C)).Send.Sent.ShouldBe(4);

            ledger.Snapshot().Slots.ShouldBeEmpty();
            sender.IsEmpty.ShouldBeTrue();
        }
    }

    [Fact]
    public void An_old_generation_sends_nothing_and_records_nothing()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            ledger.IncrementGeneration();

            gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]).Result.ShouldBe(GateResult.Fenced);
            gate.TryRun(1, 0, static _ => throw new InvalidOperationException("never"))
                .ShouldBe(GateResult.Fenced);

            sender.Batches.ShouldBeEmpty();
            ledger.Snapshot().Slots.ShouldBeEmpty();
        }
    }

    [Fact]
    public void A_partial_send_keeps_what_went_down_and_rolls_back_what_did_not()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            sender.TakeNext = 1;

            var outcome = gate.TryInject(
                1,
                [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Shift)]
            );

            outcome.Send.Sent.ShouldBe(1);
            ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.Down, 1)]);
            sender.Keys.ShouldBe([Ctrl]);
        }
    }

    [Fact]
    public void A_release_that_did_not_go_stays_recorded()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
            sender.TakeNext = 0;

            gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);

            ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Ctrl, LedgerSlotState.Down, 1)]);
            sender.Keys.ShouldBe([Ctrl]);
        }
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void A_release_the_secure_desktop_refuses_is_pending_and_goes_later()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]);
            sender.TakeNext = 0;
            sender.NextError = InjectionGate.AccessDenied;

            gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);
            ledger.Snapshot().Slots.Single().State.ShouldBe(LedgerSlotState.ReleasePending);

            gate.TryInject(1, [LowLevelInput.KeyUp(Ctrl)]);
            ledger.Snapshot().Slots.ShouldBeEmpty();
            sender.IsEmpty.ShouldBeTrue();
        }
    }

    [Fact]
    public void A_batch_that_needs_a_slot_the_full_ledger_lacks_sends_nothing()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            for (var i = 0; i < KeyLedgerLayout.SlotCount - 1; i++)
            {
                ledger.TryBeginDown(
                    new PhysicalKey((ushort)(0x200 + i), 0, LedgerKeyAttributes.None),
                    out _
                );
            }

            var outcome = gate.TryInject(
                1,
                [LowLevelInput.KeyDown(Ctrl), LowLevelInput.KeyDown(Shift)]
            );

            outcome.Send.ShouldBe(new SendResult(0, InjectionGate.LedgerFull));
            sender.Batches.ShouldBeEmpty();
            ledger.Snapshot().Slots.Count(slot => slot.Key == Ctrl).ShouldBe(0);
        }
    }

    [Fact]
    public void Mouse_buttons_are_recorded_before_the_press_and_cleared_after_the_release()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            sender.BeforeApply = (_, _) => ledger.MouseButtons.ShouldBe(LedgerMouseButtons.Left);

            gate.TryInject(1, [LowLevelInput.ButtonDown(LedgerMouseButtons.Left)]);
            ledger.MouseButtons.ShouldBe(LedgerMouseButtons.Left);

            sender.BeforeApply = null;
            gate.TryInject(1, [LowLevelInput.ButtonUp(LedgerMouseButtons.Left)]);
            ledger.MouseButtons.ShouldBe(LedgerMouseButtons.None);
            sender.IsEmpty.ShouldBeTrue();
        }
    }

    [Fact]
    public void An_internal_chord_skips_the_keys_an_engine_holder_keeps()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(1, [LowLevelInput.KeyDown(Shift)]);

            gate.TryInjectChord(1, [Ctrl, Alt, Shift, C]);

            sender.Batches[^1].ShouldBe(Tap(Ctrl, Alt, C));
            sender.Keys.ShouldBe([Shift]);
            ledger.Snapshot().Slots.ShouldBe([new LedgerSlot(Shift, LedgerSlotState.Down, 1)]);
        }
    }

    [Fact]
    public void The_emergency_raises_the_generation_releases_everything_and_fences_the_old_engine()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(
                1,
                [
                    LowLevelInput.KeyDown(Ctrl),
                    LowLevelInput.KeyDown(Alt),
                    LowLevelInput.ButtonDown(LedgerMouseButtons.Left),
                ]
            );

            gate.TryEmergencyRelease(TimeSpan.FromMilliseconds(250), out var generation)
                .ShouldBe(EmergencyOutcome.Released);

            generation.ShouldBe(2UL);
            sender.IsEmpty.ShouldBeTrue();
            ledger.Snapshot().Slots.ShouldBeEmpty();
            ledger.MouseButtons.ShouldBe(LedgerMouseButtons.None);
            gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]).Result.ShouldBe(GateResult.Fenced);
            sender.IsEmpty.ShouldBeTrue();
            gate.TryInject(generation, [LowLevelInput.KeyDown(Ctrl)])
                .Result.ShouldBe(GateResult.Ran);
        }
    }

    [Fact]
    public void The_emergency_cannot_take_the_gate_while_the_engine_is_frozen_inside_send_input()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            sender.FreezeOnCall = 1;
            var engine = new Thread(() => gate.TryInject(1, [LowLevelInput.KeyDown(Ctrl)]))
            {
                IsBackground = true,
            };
            engine.Start();
            sender
                .Frozen.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken)
                .ShouldBeTrue();

            gate.TryEmergencyRelease(TimeSpan.FromMilliseconds(50), out _)
                .ShouldBe(EmergencyOutcome.GateBusy);

            // Escalation: the process ends and the guardian releases from the ledger, where the frozen press is.
            ledger.Snapshot().Slots.Single().Key.ShouldBe(Ctrl);
            sender.Resume.Set();
            engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
    }

    [Fact]
    public void Releasing_everything_with_the_current_generation_keeps_it()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInject(1, [LowLevelInput.KeyDown(Shift)]);

            gate.TryReleaseEverything(1).ShouldBe(GateResult.Ran);

            sender.IsEmpty.ShouldBeTrue();
            ledger.Generation.ShouldBe(1UL);
            gate.TryReleaseEverything(9).ShouldBe(GateResult.Fenced);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    public void A_chord_send_input_takes_only_in_part_leaves_nothing_down(int taken)
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            var f24 = new PhysicalKey(0x87, 0x76, LedgerKeyAttributes.None);
            sender.TakeNext = taken;

            gate.TryInjectChord(1, [Ctrl, Alt, Shift, f24]).Send.Sent.ShouldBe(taken);

            sender.IsEmpty.ShouldBeTrue();
            ledger.Snapshot().Slots.ShouldBeEmpty();
        }
    }

    [Fact]
    public void The_release_of_a_lone_alt_a_partial_batch_left_down_goes_with_the_menu_mask()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            sender.TakeNext = 1;

            gate.TryInjectBalanced(1, Tap(Alt, C));

            sender
                .Batches[^1]
                .ShouldBe([
                    LowLevelInput.KeyDown(LedgerRelease.MenuMask),
                    LowLevelInput.KeyUp(LedgerRelease.MenuMask),
                    LowLevelInput.KeyUp(Alt),
                ]);
            sender.IsEmpty.ShouldBeTrue();
            ledger.Snapshot().Slots.ShouldBeEmpty();
        }
    }

    [Fact]
    public void A_balanced_batch_is_fenced_like_any_other()
    {
        var (ledger, sender, gate) = Create();
        using (ledger)
        {
            gate.TryInjectBalanced(2, Tap(C)).Result.ShouldBe(GateResult.Fenced);
            gate.TryInjectChord(2, [Ctrl, C]).Result.ShouldBe(GateResult.Fenced);

            sender.Batches.ShouldBeEmpty();
        }
    }
}
