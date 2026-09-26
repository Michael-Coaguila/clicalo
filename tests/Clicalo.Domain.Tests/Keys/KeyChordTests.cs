using Clicalo.Domain.Keys;
using Clicalo.Domain.Tests.Generators;
using CsCheck;

namespace Clicalo.Domain.Tests.Keys;

/// <summary>
/// <see cref="KeyChord.Create"/>: press order kept (EJE-003), one side mechanism (EDI-009), no repeated stroke and no
/// empty token (MIG-004).
/// </summary>
public sealed class KeyChordTests
{
    [Fact]
    [Trait("Req", "EJE-003")]
    public void The_press_order_is_kept()
    {
        var chord = KeyChord.FromKeys(KeyIds.S, KeyIds.Shift, KeyIds.Ctrl);

        chord.Strokes.Select(s => s.Key).ShouldBe([KeyIds.S, KeyIds.Shift, KeyIds.Ctrl]);
    }

    [Theory]
    [Trait("Req", "EDI-009")]
    [InlineData("lctrl", "ctrl", KeySide.Left)]
    [InlineData("rctrl", "ctrl", KeySide.Right)]
    [InlineData("lshift", "shift", KeySide.Left)]
    [InlineData("rshift", "shift", KeySide.Right)]
    [InlineData("lalt", "alt", KeySide.Left)]
    [InlineData("altgr", "alt", KeySide.Right)]
    [InlineData("rwin", "win", KeySide.Right)]
    public void A_sided_key_of_the_catalog_is_its_base_key_with_a_side(
        string sided,
        string baseKey,
        KeySide side
    )
    {
        var chord = KeyChord.FromKeys(new KeyId(sided), KeyIds.A);

        chord.Strokes[0].ShouldBe(new KeyStroke(new KeyId(baseKey), side));
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void The_side_of_a_modifier_is_kept_and_a_key_that_is_not_a_modifier_has_none()
    {
        var chord = KeyChord.Create([
            new KeyStroke(KeyIds.Ctrl, KeySide.Right),
            new KeyStroke(KeyIds.A, KeySide.Left),
        ]);

        chord.Strokes.ShouldBe([
            new KeyStroke(KeyIds.Ctrl, KeySide.Right),
            new KeyStroke(KeyIds.A),
        ]);
    }

    [Fact]
    [Trait("Req", "EDI-008")]
    public void A_repeated_stroke_is_dropped_and_the_first_one_wins()
    {
        var chord = KeyChord.FromKeys(
            KeyIds.Ctrl,
            KeyIds.C,
            KeyIds.Ctrl,
            KeyIds.LeftCtrl,
            KeyIds.C
        );

        chord.Strokes.ShouldBe([
            new KeyStroke(KeyIds.Ctrl),
            new KeyStroke(KeyIds.C),
            new KeyStroke(KeyIds.Ctrl, KeySide.Left),
        ]);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public void An_empty_key_never_becomes_a_token()
    {
        var chord = KeyChord.Create([
            new KeyStroke(default),
            new KeyStroke(new KeyId(string.Empty)),
        ]);

        chord.ShouldBeSameAs(KeyChord.Empty);
        chord.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void Keys_outside_the_catalog_are_kept_as_given()
    {
        var chord = KeyChord.Create([
            new KeyStroke(new KeyId("char:ñ")),
            new KeyStroke(new KeyId("x.custom"), KeySide.Left),
        ]);

        chord.Strokes.ShouldBe([
            new KeyStroke(new KeyId("char:ñ")),
            new KeyStroke(new KeyId("x.custom"), KeySide.Left),
        ]);
    }

    [Theory]
    [InlineData(new[] { "altgr" }, true)]
    [InlineData(new[] { "ctrl", "win" }, true)]
    [InlineData(new[] { "ctrl", "c" }, false)]
    [InlineData(new string[0], false)]
    public void Only_modifiers_is_a_complete_combination(string[] keys, bool modifiersOnly) =>
        KeyChord
            .FromKeys([.. keys.Select(k => new KeyId(k))])
            .IsModifiersOnly.ShouldBe(modifiersOnly);

    [Fact]
    [Trait("Req", "EDI-009")]
    public void Creating_again_from_its_own_strokes_changes_nothing() =>
        DomainGen.Chord.Sample(
            chord =>
            {
                var again = KeyChord.Create(chord.Strokes);
                again.ShouldBe(chord);
                again.Strokes.Distinct().Count().ShouldBe(chord.Strokes.Count);
                chord.Strokes.ShouldAllBe(s => !string.IsNullOrEmpty(s.Key.Value));
                foreach (var stroke in chord.Strokes)
                {
                    if (KeyDefinitions.TryGet(stroke.Key, out var definition))
                    {
                        definition.BaseKey.ShouldBeNull();
                    }
                }
            },
            iter: 10_000
        );
}
