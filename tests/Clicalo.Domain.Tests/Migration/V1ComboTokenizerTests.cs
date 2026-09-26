using Clicalo.Domain.Keys;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using CsCheck;
using static Clicalo.Domain.Tests.Migration.V1Docs;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>The v1 combination grammar of catalog §7.3 (MIG-003, MIG-005).</summary>
[Trait("Req", "MIG-003")]
public sealed class V1ComboTokenizerTests
{
    [Fact]
    public void A_plus_after_a_plus_is_the_plus_key()
    {
        var scan = V1ComboTokenizer.Scan("ctrl++");

        scan.Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.Plus))]);
        scan.IsResolved.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ctrl+plus")]
    [InlineData("ctrl++")]
    [InlineData("CTRL + +")]
    public void The_plus_key_has_the_same_meaning_however_it_is_written(string text) =>
        V1ComboTokenizer.Scan(text).Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.Plus))]);

    [Fact]
    public void A_plus_after_num_is_the_keypad_plus()
    {
        V1ComboTokenizer
            .Scan("ctrl+num+")
            .Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.NumAdd))]);
        V1ComboTokenizer
            .Scan("ctrl+num-")
            .Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.NumSubtract))]);
        V1ComboTokenizer.Scan("num+").Chords.ShouldBe([Chord(Key(KeyIds.NumAdd))]);
    }

    [Fact]
    public void Spaces_separate_chords_that_become_macro_steps()
    {
        var scan = V1ComboTokenizer.Scan("ctrl+k ctrl+c");

        scan.Chords.ShouldBe([
            Chord(Key(KeyIds.Ctrl), Key(KeyIds.K)),
            Chord(Key(KeyIds.Ctrl), Key(KeyIds.C)),
        ]);
    }

    [Fact]
    public void Runs_of_spaces_and_spaces_next_to_a_plus_are_trimmed_like_v1_did()
    {
        V1ComboTokenizer
            .Scan("  ctrl + k    ctrl +c ")
            .Chords.ShouldBe([
                Chord(Key(KeyIds.Ctrl), Key(KeyIds.K)),
                Chord(Key(KeyIds.Ctrl), Key(KeyIds.C)),
            ]);
    }

    [Theory]
    [InlineData("ctrl+-")]
    [InlineData("ctrl+minus")]
    public void Minus_and_its_alias_are_the_minus_key(string text) =>
        V1ComboTokenizer.Scan(text).Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.Minus))]);

    [Fact]
    public void The_minus_sign_U_2212_is_minus_also_on_the_keypad()
    {
        var minusSign = ((char)0x2212).ToString();

        V1ComboTokenizer
            .Scan("ctrl+" + minusSign)
            .Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.Minus))]);
        V1ComboTokenizer
            .Scan("ctrl+num" + minusSign)
            .Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.NumSubtract))]);
    }

    [Fact]
    public void Sides_become_a_base_modifier_with_its_side()
    {
        V1ComboTokenizer
            .Scan("altright+shiftright")
            .Chords.ShouldBe([
                Chord(Key(KeyIds.Alt, KeySide.Right), Key(KeyIds.Shift, KeySide.Right)),
            ]);
        V1ComboTokenizer
            .Scan("ctrlleft+winright+a")
            .Chords.ShouldBe([
                Chord(
                    Key(KeyIds.Ctrl, KeySide.Left),
                    Key(KeyIds.Win, KeySide.Right),
                    Key(KeyIds.A)
                ),
            ]);

        // «winleft» is the generic Win key (catalog §7.3).
        V1ComboTokenizer
            .Scan("winleft+d")
            .Chords.ShouldBe([Chord(Key(KeyIds.Win), Key(KeyIds.D))]);
    }

    [Theory]
    [InlineData("altright")]
    [InlineData("ctrlright")]
    [InlineData("ctrl+win")]
    [InlineData("altright+shiftright")]
    public void Modifier_only_combinations_are_valid(string text)
    {
        var scan = V1ComboTokenizer.Scan(text);

        scan.IsResolved.ShouldBeTrue();
        scan.Chords.Count.ShouldBe(1);
    }

    [Fact]
    public void The_saved_order_is_kept()
    {
        V1ComboTokenizer
            .Scan("shift+alt+f")
            .Chords.ShouldBe([Chord(Key(KeyIds.Shift), Key(KeyIds.Alt), Key(KeyIds.F))]);
        V1ComboTokenizer
            .Scan("win+ctrl+space")
            .Chords.ShouldBe([Chord(Key(KeyIds.Win), Key(KeyIds.Ctrl), Key(KeyIds.Space))]);
    }

    [Theory]
    [Trait("Req", "MIG-005")]
    [InlineData("ctrl+grave", "char:`")]
    [InlineData("ctrl+`", "char:`")]
    [InlineData("ctrl+backslash", "char:\\")]
    [InlineData("ctrl+\\", "char:\\")]
    [InlineData("ctrl+[", "char:[")]
    [InlineData("ctrl+]", "char:]")]
    [InlineData("ctrl+'", "char:'")]
    [InlineData("ctrl+#", "char:#")]
    public void The_keys_added_for_the_migration_resolve(string text, string key) =>
        V1ComboTokenizer.Scan(text).Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(new KeyId(key)))]);

    [Theory]
    [InlineData("return", "enter")]
    [InlineData("escape", "esc")]
    [InlineData("del", "delete")]
    [InlineData("pgup", "pageup")]
    [InlineData("pgdn", "pagedown")]
    [InlineData("prtsc", "printscreen")]
    [InlineData("apps", "menu")]
    [InlineData("add", "num.add")]
    [InlineData("subtract", "num.subtract")]
    [InlineData("num*", "num.multiply")]
    [InlineData("multiply", "num.multiply")]
    [InlineData("num/", "num.divide")]
    [InlineData("divide", "num.divide")]
    [InlineData("decimal", "num.decimal")]
    [InlineData("num7", "num.7")]
    [InlineData("f24", "f24")]
    [InlineData("stop", "media.stop")]
    [InlineData("ñ", "char:ñ")]
    public void Aliases_of_the_table_resolve_to_one_catalog_key(string token, string key) =>
        V1ComboTokenizer.Scan(token).Chords.ShouldBe([Chord(Key(new KeyId(key)))]);

    [Fact]
    [Trait("Req", "MIG-005")]
    public void A_token_without_equivalent_is_kept_as_written_for_review()
    {
        var scan = V1ComboTokenizer.Scan("Ctrl+Hyper+c");

        scan.Unresolved.ShouldBe(["hyper"]);
        scan.Chords.ShouldBe([Chord(Key(KeyIds.Ctrl), Key(KeyIds.C))]);
        scan.IsResolved.ShouldBeFalse();
    }

    [Theory]
    [InlineData("ctrl+")]
    [InlineData("++")]
    [InlineData("ctrl+num++")]
    public void A_dangling_separator_is_never_an_empty_token(string text)
    {
        var scan = V1ComboTokenizer.Scan(text);

        scan.Unresolved.ShouldNotBeEmpty();
        scan.Unresolved.ShouldAllBe(token => token.Length > 0);
        scan.Chords.ShouldAllBe(chord => !chord.IsEmpty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_combination_has_no_chords(string text)
    {
        var scan = V1ComboTokenizer.Scan(text);

        scan.Chords.ShouldBeEmpty();
        scan.Unresolved.ShouldBeEmpty();
    }

    [Fact]
    public void Any_text_scans_without_throwing_and_without_empty_tokens() =>
        Gen.String.Sample(
            text =>
            {
                var scan = V1ComboTokenizer.Scan(text);
                scan.Chords.ShouldAllBe(chord => !chord.IsEmpty);
                scan.Unresolved.ShouldAllBe(token => token.Length > 0);
            },
            iter: 5_000
        );

    [Fact]
    public void Chords_written_with_table_tokens_scan_back_to_their_strokes() =>
        Gen.Int[0, TokenList.Count - 1]
            .Array[1, 4]
            .Array[1, 3]
            .Sample(
                chords =>
                {
                    var text = string.Join(
                        ' ',
                        chords.Select(c => string.Join('+', c.Select(i => TokenList[i])))
                    );
                    var scan = V1ComboTokenizer.Scan(text);

                    scan.IsResolved.ShouldBeTrue(text);
                    ValueList<ValueList<KeyStroke>> expected =
                    [
                        .. chords.Select(c => new ValueList<KeyStroke>([
                            .. c.Select(i => V1KeyTokens.Table[TokenList[i]]),
                        ])),
                    ];
                    scan.Chords.ShouldBe(expected, text);
                },
                iter: 5_000
            );

    [Fact]
    [Trait("Req", "MIG-003")]
    public void Tokenize_builds_one_normalized_chord_per_group()
    {
        var parse = V1ComboTokenizer.Tokenize("ctrl+k ctrl+c");

        parse.Chords.Count.ShouldBe(2);
        parse.Chords[0].ShouldBe(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.K)]));
        parse.Chords[1].ShouldBe(KeyChord.Create([Key(KeyIds.Ctrl), Key(KeyIds.C)]));
        parse.Unresolved.ShouldBeEmpty();
    }

    /// <summary>
    /// Every token of the table except the ones that the grammar reads differently when joined with <c>+</c>
    /// (<c>+</c> itself and <c>num+</c> are covered above).
    /// </summary>
    private static readonly List<string> TokenList =
    [
        .. V1KeyTokens
            .Table.Keys.Where(static t =>
                !t.Contains('+', StringComparison.Ordinal)
                && !string.Equals(t, "num", StringComparison.Ordinal)
            )
            .Order(StringComparer.Ordinal),
    ];
}
