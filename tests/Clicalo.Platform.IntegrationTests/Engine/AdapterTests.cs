using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The engine's adapters over the gate (blueprint §7.7): the Domain's keys become the same physical keys in the same
/// mode (INV-12), the menu mask becomes <c>VK 0xE8</c>, text becomes Unicode with Enter for line breaks, a mouse action
/// moves first, and each gate outcome becomes the port's result. Nothing is injected: the gate sends to a model.
/// </summary>
[Trait("Req", "NFR-004")]
public sealed class AdapterTests
{
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey RightAltScan = new(0, 0x38, true, InjectionMode.ScanCode);

    private static (
        KeyLedgerSection Ledger,
        PhysicalStateInjector Sender,
        GateInputInjector Injector
    ) Create()
    {
        var ledger = KeyLedgerSection.CreateInMemory();
        var sender = new PhysicalStateInjector();
        return (ledger, sender, new GateInputInjector(new InjectionGate(ledger, sender)));
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Keys_keep_their_mode_and_the_mask_becomes_an_unassigned_virtual_key()
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            injector.Send(
                new EngineGeneration(1),
                [InjectedEvent.KeyDown(Ctrl), InjectedEvent.KeyDown(RightAltScan)]
            );
            injector.Send(
                new EngineGeneration(1),
                [
                    InjectedEvent.MenuMask(InjectionMode.ScanCode),
                    InjectedEvent.KeyUp(RightAltScan),
                    InjectedEvent.KeyUp(Ctrl),
                ]
            );

            sender
                .Batches[0]
                .ShouldBe([
                    LowLevelInput.KeyDown(new PhysicalKey(0xA2, 0x1D, LedgerKeyAttributes.None)),
                    LowLevelInput.KeyDown(
                        new PhysicalKey(
                            0,
                            0x38,
                            LedgerKeyAttributes.Extended | LedgerKeyAttributes.ScanCodeMode
                        )
                    ),
                ]);
            sender.Batches[1][0].ShouldBe(LowLevelInput.KeyDown(LedgerRelease.MenuMask));
            sender.Batches[1][1].ShouldBe(LowLevelInput.KeyUp(LedgerRelease.MenuMask));
            sender.IsEmpty.ShouldBeTrue();
            ledger.Snapshot().Slots.ShouldBeEmpty();
        }
    }

    [Fact]
    public void The_mapping_round_trips_every_mode_and_flag()
    {
        foreach (
            var key in new[]
            {
                Ctrl,
                RightAltScan,
                new InjectedKey(0x25, 0x4B, true, InjectionMode.VirtualKey),
            }
        )
        {
            InputMapping.ToInjected(InputMapping.ToPhysical(key)).ShouldBe(key);
        }
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    public void Text_goes_as_unicode_and_every_line_break_as_one_enter()
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            var result = injector.TypeText(new EngineGeneration(1), "ñ😀\r\nb\nc\r");

            var inputs = sender.Batches.Single();
            inputs
                .Where(static i => i.Kind == LowLevelInputKind.Unicode)
                .Select(static i => i.Character)
                .ShouldBe(['ñ', '\uD83D', '\uDE00', 'b', 'c']);
            inputs
                .Count(static i =>
                    i.Kind == LowLevelInputKind.KeyDown && i.Key == InputMapping.Enter
                )
                .ShouldBe(3);
            result.Status.ShouldBe(InjectionStatus.Sent);
            sender.IsEmpty.ShouldBeTrue();
        }
    }

    [Theory]
    [InlineData(MouseOp.RightClick, LowLevelInputKind.MouseButtonDown)]
    [InlineData(MouseOp.DoubleClick, LowLevelInputKind.MouseButtonDown)]
    [InlineData(MouseOp.MiddleClick, LowLevelInputKind.MouseButtonDown)]
    [InlineData(MouseOp.ScrollUp, LowLevelInputKind.Wheel)]
    [InlineData(MouseOp.ScrollLeft, LowLevelInputKind.HorizontalWheel)]
    [Trait("Req", "EJE-009")]
    public void A_mouse_action_moves_to_its_point_first_and_leaves_no_button_down(
        MouseOp op,
        LowLevelInputKind kind
    )
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            injector
                .Mouse(new EngineGeneration(1), op, new PhysicalPoint(640, 480))
                .Status.ShouldBe(InjectionStatus.Sent);

            var inputs = sender.Batches.Single();
            inputs[0].ShouldBe(LowLevelInput.MoveTo(640, 480));
            inputs[1].Kind.ShouldBe(kind);
            sender.Buttons.ShouldBe(LedgerMouseButtons.None);
        }
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    [Trait("Req", "SEG-001")]
    public void A_click_send_input_takes_only_in_part_leaves_no_button_down()
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            // The move and the button down go; the button up does not.
            sender.TakeNext = 2;

            injector
                .Mouse(new EngineGeneration(1), MouseOp.RightClick, new PhysicalPoint(640, 480))
                .Status.ShouldBe(InjectionStatus.Failed);

            sender.Batches.Count.ShouldBe(2);
            sender.Batches[1].ShouldBe([LowLevelInput.ButtonUp(LedgerMouseButtons.Right)]);
            sender.Buttons.ShouldBe(LedgerMouseButtons.None);
            ledger.MouseButtons.ShouldBe(LedgerMouseButtons.None);
        }
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    [Trait("Req", "SEG-001")]
    public void A_text_send_input_takes_only_in_part_leaves_no_enter_down()
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            // «a», then Enter down; Enter up and «b» do not go.
            sender.TakeNext = 2;

            injector
                .TypeText(new EngineGeneration(1), "a\nb")
                .Status.ShouldBe(InjectionStatus.Failed);

            sender.Batches.Count.ShouldBe(2);
            sender.Batches[1].ShouldBe([LowLevelInput.KeyUp(InputMapping.Enter)]);
            sender.IsEmpty.ShouldBeTrue();
            ledger.Snapshot().Slots.ShouldBeEmpty();
        }
    }

    [Fact]
    [Trait("Req", "EJE-007")]
    public void A_drag_only_moves_its_button_is_held_through_the_ledger()
    {
        var (ledger, sender, injector) = Create();
        using (ledger)
        {
            injector.Mouse(new EngineGeneration(1), MouseOp.Drag, new PhysicalPoint(1, 2));
            injector.Send(new EngineGeneration(1), [InjectedEvent.MouseDown(MouseButtons.Left)]);

            sender.Batches[0].ShouldBe([LowLevelInput.MoveTo(1, 2)]);
            ledger.MouseButtons.ShouldBe(LedgerMouseButtons.Left);
            sender.Buttons.ShouldBe(LedgerMouseButtons.Left);
        }
    }

    [Fact]
    public void Gate_outcomes_become_port_results()
    {
        GateInputInjector
            .Result(new GateOutcome(GateResult.Fenced, default), 2)
            .Status.ShouldBe(InjectionStatus.Fenced);
        GateInputInjector
            .Result(new GateOutcome(GateResult.Ran, new SendResult(2, 0)), 2)
            .Status.ShouldBe(InjectionStatus.Sent);
        GateInputInjector
            .Result(
                new GateOutcome(GateResult.Ran, new SendResult(0, InjectionGate.AccessDenied)),
                2
            )
            .Status.ShouldBe(InjectionStatus.Blocked);
        GateInputInjector
            .Result(new GateOutcome(GateResult.Ran, new SendResult(1, 87)), 2)
            .ShouldBe(new InjectionResult(InjectionStatus.Failed, 1, 87));
    }

    [Fact]
    public void The_ledger_port_exposes_generation_marks_and_pending_releases()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var port = new KeyLedgerPort(new InjectionGate(ledger, new PhysicalStateInjector()));

        port.SetMarks(KeyLedgerMarks.EngineAlive | KeyLedgerMarks.CleanShutdown);
        port.ClearMarks(KeyLedgerMarks.CleanShutdown);
        port.TryWriteHeartbeat(new EngineGeneration(1), 99).ShouldBeTrue();
        ledger.TryBeginDown(new PhysicalKey(0x41, 0x1E, LedgerKeyAttributes.None), out var slot);
        ledger.MarkReleasePending(slot);

        port.Marks.ShouldBe(KeyLedgerMarks.EngineAlive);
        ledger.Marks.ShouldBe(LedgerMarks.EngineAlive);
        port.CurrentGeneration.ShouldBe(new EngineGeneration(1));
        port.PendingReleases.ShouldBe(1);
        ledger.LastHeartbeatTicks.ShouldBe(99);
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void The_start_up_release_lets_go_of_every_modifier_the_system_reports_down()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var sender = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, sender);

        var released = PreventiveRelease.Run(gate, 1, static vk => vk is 0xA0 or 0x5B);

        released.ShouldBe(2);
        sender
            .Batches.Single()
            .ShouldBe([
                LowLevelInput.KeyUp(PreventiveRelease.Modifiers[0]),
                LowLevelInput.KeyDown(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(LedgerRelease.MenuMask),
                LowLevelInput.KeyUp(PreventiveRelease.Modifiers[6]),
            ]);
        PreventiveRelease.Run(gate, 1, static _ => false).ShouldBe(0);
        sender.Batches.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_rights_chord_goes_only_when_it_is_registered_and_is_balanced()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var sender = new PhysicalStateInjector();
        var hotkey = new FakeRightsHotkey();
        var effects = new InternalKeyEffects(new InjectionGate(ledger, sender), hotkey);

        (await effects.SendRightsHotkeyAsync(CancellationToken.None)).ShouldBeFalse();
        sender.Batches.ShouldBeEmpty();

        hotkey.IsRegistered = true;
        (await effects.SendRightsHotkeyAsync(CancellationToken.None)).ShouldBeTrue();
        (await effects.SendDictationChordAsync(CancellationToken.None)).ShouldBeTrue();

        sender.Batches.Count.ShouldBe(2);
        sender.Batches[0].Length.ShouldBe(8);
        sender.IsEmpty.ShouldBeTrue();
        ledger.Snapshot().Slots.ShouldBeEmpty();
    }
}
