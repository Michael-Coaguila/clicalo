using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Keys;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>The combination box and the key picker (EDI-007, EDI-008, EDI-009), in press order (EJE-003).</summary>
public sealed class ChordEditsTests
{
    [Fact]
    [Trait("Req", "EDI-008")]
    public void A_tap_adds_the_key_at_the_end_and_a_second_tap_removes_it()
    {
        var chord = ChordEdits.Tap(KeyChord.Empty, KeyIds.C);
        chord = ChordEdits.Tap(chord, KeyIds.Ctrl);

        chord.ShouldBe(KeyChord.FromKeys(KeyIds.C, KeyIds.Ctrl), "press order, not modifier order");
        ChordEdits.Tap(chord, KeyIds.C).ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl));
        ChordEdits.IsChosen(chord, KeyIds.Ctrl).ShouldBeTrue();
        ChordEdits.IsChosen(chord, KeyIds.V).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void A_sided_key_sets_the_side_of_the_modifier_in_its_place()
    {
        var chord = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C);

        var left = ChordEdits.Tap(chord, KeyIds.LeftCtrl);

        left.Strokes.Count.ShouldBe(2);
        left.Strokes[0].ShouldBe(new KeyStroke(KeyIds.Ctrl, KeySide.Left));
        ChordEdits.IsChosen(left, KeyIds.LeftCtrl).ShouldBeTrue();
        ChordEdits.IsChosen(left, KeyIds.RightCtrl).ShouldBeFalse();
        ChordEdits.IsChosen(left, KeyIds.Ctrl).ShouldBeTrue("the Ctrl button shows any Ctrl");
        ChordEdits.Tap(left, KeyIds.LeftCtrl).ShouldBe(KeyChord.FromKeys(KeyIds.C));
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void Changing_other_keys_keeps_the_side_of_the_modifiers()
    {
        var chord = KeyChord.Create([
            new KeyStroke(KeyIds.Alt, KeySide.Right),
            new KeyStroke(KeyIds.E),
        ]);

        var changed = ChordEdits.Tap(ChordEdits.Tap(chord, KeyIds.E), KeyIds.Q);

        changed.Strokes.ShouldBe([
            new KeyStroke(KeyIds.Alt, KeySide.Right),
            new KeyStroke(KeyIds.Q),
        ]);
    }

    [Fact]
    [Trait("Req", "EDI-007")]
    public void The_chip_x_and_backspace_remove_one_key()
    {
        var chord = KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift, KeyIds.S);

        ChordEdits.RemoveAt(chord, 1).ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S));
        ChordEdits.RemoveLast(chord).ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Shift));
        ChordEdits.RemoveAt(chord, 7).ShouldBe(chord);
        ChordEdits.RemoveLast(KeyChord.Empty).ShouldBe(KeyChord.Empty);
    }
}
