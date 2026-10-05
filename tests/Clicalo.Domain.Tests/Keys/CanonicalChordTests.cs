using Clicalo.Domain.Keys;
using Clicalo.Domain.Tests.Generators;
using CsCheck;

namespace Clicalo.Domain.Tests.Keys;

/// <summary>
/// The canonical key of REP-001 and its persisted text (ADR-0018 point 4): modifiers as a set with their side, main
/// keys in order, one spelling per key and a lossless round trip.
/// </summary>
[Trait("Req", "REP-001")]
public sealed class CanonicalChordTests
{
    [Fact]
    public void Modifiers_compare_as_a_set_whatever_the_press_order()
    {
        Key(KeyIds.Ctrl, KeyIds.Shift, KeyIds.S).ShouldBe(Key(KeyIds.Shift, KeyIds.Ctrl, KeyIds.S));
        Key(KeyIds.Ctrl, KeyIds.Shift, KeyIds.S).ShouldBe(Key(KeyIds.S, KeyIds.Shift, KeyIds.Ctrl));
    }

    [Fact]
    public void Main_keys_compare_in_order()
    {
        Key(KeyIds.Ctrl, KeyIds.K, KeyIds.C).ShouldNotBe(Key(KeyIds.Ctrl, KeyIds.C, KeyIds.K));
    }

    [Fact]
    public void The_side_is_part_of_the_key()
    {
        Key(KeyIds.LeftCtrl, KeyIds.C).ShouldNotBe(Key(KeyIds.Ctrl, KeyIds.C));
        Key(KeyIds.LeftCtrl, KeyIds.C).ShouldNotBe(Key(KeyIds.RightCtrl, KeyIds.C));
        Key(KeyIds.AltGr, KeyIds.Q).Modifiers.ShouldBe(ChordModifiers.RightAlt);
    }

    [Fact]
    public void Character_keys_compare_without_distinguishing_case()
    {
        Key(KeyIds.Ctrl, new KeyId("char:Ñ")).ShouldBe(Key(KeyIds.Ctrl, new KeyId("char:ñ")));
    }

    [Fact]
    public void Only_a_chord_with_a_key_has_a_canonical_key()
    {
        CanonicalChord.TryFrom(KeyChord.Empty, out _).ShouldBeFalse();
        Key(KeyIds.AltGr).Main.IsEmpty.ShouldBeTrue();
    }

    [Theory]
    [Trait("Req", "EJE-014")]
    [InlineData("ctrl alt delete")]
    [InlineData("delete alt ctrl")]
    [InlineData("lctrl lalt delete")]
    [InlineData("alt lctrl delete")]
    [InlineData("rctrl alt delete")]
    [InlineData("ctrl altgr delete")]
    [InlineData("rctrl altgr delete")]
    public void A_blocked_combination_is_found_in_any_order_and_on_either_side(string keys)
    {
        var blocked = Key(KeyIds.Ctrl, KeyIds.Alt, KeyIds.Delete).ForBlockedComparison();

        Key([.. keys.Split(' ').Select(k => new KeyId(k))])
            .ForBlockedComparison()
            .ShouldBe(blocked);
    }

    [Fact]
    [Trait("Req", "EJE-014")]
    public void The_blocked_form_keeps_the_families_and_the_main_keys()
    {
        var blocked = Key(KeyIds.Ctrl, KeyIds.Alt, KeyIds.Delete).ForBlockedComparison();

        Key(KeyIds.RightCtrl, KeyIds.Delete).ForBlockedComparison().ShouldNotBe(blocked);
        Key(KeyIds.Ctrl, KeyIds.Shift, KeyIds.Delete).ForBlockedComparison().ShouldNotBe(blocked);
        Key(KeyIds.RightCtrl, KeyIds.Alt, KeyIds.Delete)
            .ForBlockedComparison()
            .Modifiers.ShouldBe(ChordModifiers.Ctrl | ChordModifiers.Alt);
    }

    [Fact]
    public void Repeated_shortcuts_keep_the_side_that_blocked_combinations_ignore()
    {
        Key(KeyIds.RightCtrl, KeyIds.Alt, KeyIds.Delete)
            .ShouldNotBe(Key(KeyIds.Ctrl, KeyIds.Alt, KeyIds.Delete));
    }

    [Theory]
    [InlineData(new[] { "s", "shift", "ctrl" }, "ctrl+shift+s")]
    [InlineData(new[] { "win", "rctrl", "lalt", "lshift", "f4" }, "rctrl+lalt+lshift+win+f4")]
    [InlineData(new[] { "altgr", "q" }, "altgr+q")]
    [InlineData(new[] { "ctrl", "char:+" }, "ctrl+char:%2B")]
    [InlineData(new[] { "shift", "char:%" }, "shift+char:%25")]
    [InlineData(new[] { "ctrl", "lwin" }, "ctrl+%6Cwin")]
    [InlineData(new[] { "ctrl", "k", "c" }, "ctrl+k+c")]
    [InlineData(new[] { "rwin" }, "rwin")]
    public void The_stable_text_has_one_spelling(string[] keys, string text)
    {
        var key = Key([.. keys.Select(k => new KeyId(k))]);

        key.ToStableString().ShouldBe(text);
        CanonicalChord.TryParse(text, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(key);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ctrl++s")]
    [InlineData("+s")]
    [InlineData("s+ctrl")]
    [InlineData("shift+ctrl+s")]
    [InlineData("ctrl+ctrl+s")]
    [InlineData("ctrl+s+s")]
    [InlineData("ctrl+char:%2")]
    [InlineData("ctrl+char:%2b")]
    [InlineData("ctrl+%C3%B1")]
    [InlineData("ctrl+S")]
    [InlineData("ctrl+%73")]
    [InlineData("ctrl+char:%C3%B1")]
    [InlineData("CTRL+s")]
    public void A_text_outside_the_grammar_does_not_parse(string text) =>
        CanonicalChord.TryParse(text, out _).ShouldBeFalse();

    [Fact]
    [Trait("Req", "REP-002")]
    public void Every_key_round_trips_through_its_stable_text() =>
        DomainGen.Chord.Sample(
            chord =>
            {
                if (!CanonicalChord.TryFrom(chord, out var key))
                {
                    chord.IsEmpty.ShouldBeTrue();
                    return;
                }

                var text = key.ToStableString();
                CanonicalChord.TryParse(text, out var parsed).ShouldBeTrue(text);
                parsed.ShouldBe(key, text);
                parsed.ToStableString().ShouldBe(text);
            },
            iter: 10_000
        );

    [Fact]
    public void Two_chords_have_the_same_key_exactly_when_their_texts_are_equal() =>
        Gen.Select(DomainGen.Chord, DomainGen.Chord)
            .Sample(
                (left, right) =>
                {
                    if (
                        CanonicalChord.TryFrom(left, out var a)
                        && CanonicalChord.TryFrom(right, out var b)
                    )
                    {
                        (a == b).ShouldBe(
                            string.Equals(
                                a.ToStableString(),
                                b.ToStableString(),
                                StringComparison.Ordinal
                            )
                        );
                    }
                },
                iter: 10_000
            );

    private static CanonicalChord Key(params ReadOnlySpan<KeyId> keys)
    {
        CanonicalChord.TryFrom(KeyChord.FromKeys(keys), out var key).ShouldBeTrue();
        return key;
    }
}
