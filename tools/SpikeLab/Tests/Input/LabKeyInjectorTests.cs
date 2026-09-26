using Clicalo.TestKit.Windows.Input;
using Clicalo.Tools.SpikeLab.Input;

namespace Clicalo.Tools.SpikeLab.Tests.Input;

/// <summary>
/// The lab injector's safety rules, checked with a fake <c>SendInput</c>: nothing here injects real input.
/// </summary>
public sealed class LabKeyInjectorTests
{
    private const nint Word = 0x1234;
    private readonly List<(IReadOnlyList<KeyStroke> Batch, nint Target)> _sent = [];
    private readonly HashSet<VirtualKeyCode> _down = [];
    private readonly Queue<nint> _foregrounds = new();

    [Fact]
    public void A_chord_goes_to_the_window_in_front_as_one_batch()
    {
        var outcome = Injector().Send(LabChord.Ctrl(VirtualKeyCode.B));

        outcome.ShouldBe(InjectionOutcome.Done);
        var (batch, target) = _sent.ShouldHaveSingleItem();
        target.ShouldBe(Word);
        batch.Count.ShouldBe(4);
    }

    [Fact]
    public void Nothing_is_sent_without_a_foreground_window()
    {
        _foregrounds.Enqueue(0);

        var outcome = Injector().Send(LabChord.Ctrl(VirtualKeyCode.B));

        outcome.Sent.ShouldBeFalse();
        outcome.Reason.ShouldNotBeNull().ShouldContain("ninguna ventana");
        _sent.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(VirtualKeyCode.RightMenu, "AltGr")]
    [InlineData(VirtualKeyCode.RightControl, "Ctrl derecha")]
    [InlineData(VirtualKeyCode.LeftWindows, "Windows izquierda")]
    [InlineData(VirtualKeyCode.LeftShift, "Mayús izquierda")]
    public void Nothing_is_sent_while_a_modifier_is_held(VirtualKeyCode held, string name)
    {
        _down.Add(held);

        var outcome = Injector().Send(LabChord.Ctrl(VirtualKeyCode.B));

        outcome.Sent.ShouldBeFalse();
        outcome.Reason.ShouldNotBeNull().ShouldContain(name);
        _sent.ShouldBeEmpty();
    }

    [Fact]
    public void Nothing_is_sent_when_the_foreground_changes_just_before_the_call()
    {
        _foregrounds.Enqueue(Word);
        _foregrounds.Enqueue(0x9999);

        var outcome = Injector().Send(LabChord.Ctrl(VirtualKeyCode.B));

        outcome.Sent.ShouldBeFalse();
        _sent.ShouldBeEmpty();
    }

    [Fact]
    public void A_refusal_of_SendInput_is_reported_not_thrown()
    {
        var injector = new LabKeyInjector(
            () => Word,
            _ => false,
            (_, _) => throw new InjectionRefusedException("UIPI")
        );

        injector.Send(LabChord.Ctrl(VirtualKeyCode.B)).Reason.ShouldBe("UIPI");
    }

    [Fact]
    public void Right_hand_and_generic_modifiers_are_refused_before_sending()
    {
        foreach (
            var key in (VirtualKeyCode[])
                [
                    VirtualKeyCode.RightMenu,
                    VirtualKeyCode.RightControl,
                    VirtualKeyCode.Control,
                    VirtualKeyCode.Menu,
                ]
        )
        {
            Should.Throw<ArgumentException>(() =>
                LabKeyInjector.EnsureLeftHandOnly(KeyStrokes.Chord(key, VirtualKeyCode.B))
            );
        }
    }

    [Fact]
    public void Release_all_sends_only_key_ups_of_the_left_modifiers_that_are_down()
    {
        _down.Add(VirtualKeyCode.LeftShift);
        _down.Add(VirtualKeyCode.LeftMenu);
        _down.Add(VirtualKeyCode.RightControl);
        _down.Add(VirtualKeyCode.LeftWindows);

        var released = Injector().ReleaseHeldModifiers();

        released.ShouldBe([VirtualKeyCode.LeftShift, VirtualKeyCode.LeftMenu]);
        _sent
            .ShouldHaveSingleItem()
            .Batch.Select(stroke => stroke.ToString())
            .ShouldBe(["VK LeftShift up", "VK LeftMenu up"]);
    }

    [Fact]
    public void Release_all_sends_nothing_when_nothing_is_down()
    {
        Injector().ReleaseHeldModifiers().ShouldBeEmpty();

        _sent.ShouldBeEmpty();
    }

    private LabKeyInjector Injector() =>
        new(
            () => _foregrounds.Count > 0 ? _foregrounds.Dequeue() : Word,
            _down.Contains,
            (batch, target) => _sent.Add((batch, target))
        );
}
