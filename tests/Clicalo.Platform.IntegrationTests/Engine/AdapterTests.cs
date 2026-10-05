using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The engine's injector (blueprint §7.7, ADR-0023): the Domain's keys become the same physical keys in the same mode
/// (INV-12), the menu mask becomes <c>VK 0xE8</c>, text becomes Unicode with Enter for line breaks, a mouse action
/// moves first, a batch taken only in part is balanced, and each <c>SendInput</c> answer becomes the port's result.
/// Nothing is injected: the injector sends to a model.
/// </summary>
[Trait("Req", "NFR-004")]
public sealed class AdapterTests
{
    private static readonly InjectedKey Ctrl = new(0xA2, 0x1D, false, InjectionMode.VirtualKey);
    private static readonly InjectedKey RightAltScan = new(0, 0x38, true, InjectionMode.ScanCode);

    private readonly PhysicalStateInjector _sender = new();
    private readonly InputInjector _injector;

    public AdapterTests() => _injector = new InputInjector(_sender, _sender);

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Keys_keep_their_mode_and_the_mask_becomes_an_unassigned_virtual_key()
    {
        _injector.Send([InjectedEvent.KeyDown(Ctrl), InjectedEvent.KeyDown(RightAltScan)]);
        _injector.Send([
            InjectedEvent.MenuMask(InjectionMode.ScanCode),
            InjectedEvent.KeyUp(RightAltScan),
            InjectedEvent.KeyUp(Ctrl),
        ]);

        _sender
            .Batches[0]
            .ShouldBe([
                LowLevelInput.KeyDown(new PhysicalKey(0xA2, 0x1D, PhysicalKeyAttributes.None)),
                LowLevelInput.KeyDown(
                    new PhysicalKey(
                        0,
                        0x38,
                        PhysicalKeyAttributes.Extended | PhysicalKeyAttributes.ScanCodeMode
                    )
                ),
            ]);
        _sender.Batches[1][0].ShouldBe(LowLevelInput.KeyDown(PressedInputRelease.MenuMask));
        _sender.Batches[1][1].ShouldBe(LowLevelInput.KeyUp(PressedInputRelease.MenuMask));
        _sender.IsEmpty.ShouldBeTrue();
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
        var result = _injector.TypeText("ñ😀\r\nb\nc\r");

        var inputs = _sender.Batches.Single();
        inputs
            .Where(static i => i.Kind == LowLevelInputKind.Unicode)
            .Select(static i => i.Character)
            .ShouldBe(['ñ', '\uD83D', '\uDE00', 'b', 'c']);
        inputs
            .Count(static i => i.Kind == LowLevelInputKind.KeyDown && i.Key == InputMapping.Enter)
            .ShouldBe(3);
        result.Status.ShouldBe(InjectionStatus.Sent);
        _sender.IsEmpty.ShouldBeTrue();
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
        _injector.Mouse(op, new PhysicalPoint(640, 480)).Status.ShouldBe(InjectionStatus.Sent);

        var inputs = _sender.Batches.Single();
        inputs[0].ShouldBe(LowLevelInput.MoveTo(640, 480));
        inputs[1].Kind.ShouldBe(kind);
        _sender.Buttons.ShouldBe(LowLevelMouseButtons.None);
    }

    [Fact]
    [Trait("Req", "EJE-009")]
    [Trait("Req", "SEG-001")]
    public void A_click_send_input_takes_only_in_part_leaves_no_button_down()
    {
        // The move and the button down go; the button up does not.
        _sender.TakeNext = 2;

        _injector
            .Mouse(MouseOp.RightClick, new PhysicalPoint(640, 480))
            .Status.ShouldBe(InjectionStatus.Failed);

        _sender.Batches.Count.ShouldBe(2);
        _sender.Batches[1].ShouldBe([LowLevelInput.ButtonUp(LowLevelMouseButtons.Right)]);
        _sender.Buttons.ShouldBe(LowLevelMouseButtons.None);
    }

    [Fact]
    [Trait("Req", "EJE-008")]
    [Trait("Req", "SEG-001")]
    public void A_text_send_input_takes_only_in_part_leaves_no_enter_down()
    {
        // «a», then Enter down; Enter up and «b» do not go.
        _sender.TakeNext = 2;

        _injector.TypeText("a\nb").Status.ShouldBe(InjectionStatus.Failed);

        _sender.Batches.Count.ShouldBe(2);
        _sender.Batches[1].ShouldBe([LowLevelInput.KeyUp(InputMapping.Enter)]);
        _sender.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EJE-007")]
    public void A_drag_only_moves_its_button_is_held_by_the_engine()
    {
        _injector.Mouse(MouseOp.Drag, new PhysicalPoint(1, 2));
        _injector.Send([InjectedEvent.MouseDown(MouseButtons.Left)]);

        _sender.Batches[0].ShouldBe([LowLevelInput.MoveTo(1, 2)]);
        _sender.Buttons.ShouldBe(LowLevelMouseButtons.Left);
    }

    [Fact]
    public void Send_input_answers_become_port_results()
    {
        InputInjector.Result(new SendResult(2, 0), 2).Status.ShouldBe(InjectionStatus.Sent);
        InputInjector
            .Result(new SendResult(0, SendResult.AccessDenied), 2)
            .Status.ShouldBe(InjectionStatus.Blocked);
        InputInjector
            .Result(new SendResult(1, 87), 2)
            .ShouldBe(new InjectionResult(InjectionStatus.Failed, 1, 87));
    }

    [Fact]
    public void The_internal_chords_go_balanced()
    {
        _injector
            .SendChord(InternalChord.Rights)
            .ShouldBe(new InjectionResult(InjectionStatus.Sent, 8, 0));
        _injector.SendChord(InternalChord.Dictation).Status.ShouldBe(InjectionStatus.Sent);

        _sender.Batches.Count.ShouldBe(2);
        _sender.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void An_internal_chord_neither_presses_again_nor_releases_a_key_already_down()
    {
        var shift = InternalChords.Rights[2];
        _sender.Press(shift);

        _injector.SendChord(InternalChord.Rights).Status.ShouldBe(InjectionStatus.Sent);

        _sender.Batches.Single().ShouldNotContain(input => input.Key == shift);
        _sender.Keys.ShouldBe([shift]);
    }

    [Fact]
    [Trait("Req", "NFR-005")]
    [Trait("Req", "SEG-003")]
    public void Releasing_what_is_pressed_lets_go_of_every_key_and_button_the_system_reports_down()
    {
        _injector.Send([InjectedEvent.KeyDown(Ctrl), InjectedEvent.MouseDown(MouseButtons.Left)]);
        _sender.Press(new PhysicalKey(0xA4, 0x38, PhysicalKeyAttributes.None));

        _injector.ReleasePressed().Status.ShouldBe(InjectionStatus.Sent);

        _sender.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "SEG-006")]
    public void Releasing_what_is_pressed_while_the_secure_desktop_has_the_input_is_blocked()
    {
        _sender.Press(new PhysicalKey(0xA2, 0x1D, PhysicalKeyAttributes.None));
        _sender.SecureDesktop = true;

        _injector.ReleasePressed().Status.ShouldBe(InjectionStatus.Blocked);

        _sender.Batches.ShouldBeEmpty();
    }
}
