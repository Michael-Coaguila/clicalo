using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Keys;

/// <summary>
/// The key line of a tile (CUA-007, CUA-008): labels of the interface language joined by « + »; in the size S the
/// abbreviations joined by «+» without spaces; the accessible name with the spoken names.
/// </summary>
[Trait("Req", "CUA-007")]
[Trait("Req", "CUA-008")]
public sealed class KeyChordFormatterTests
{
    private static readonly KeyLabelCatalog Labels = new([
        Entry("ctrl", "Ctrl", "Ctrl", shortEs: "Ctl", shortEn: "Ctl"),
        Entry("shift", "Shift", "Shift", shortEs: "⇧", shortEn: "⇧"),
        Entry("rctrl", "Ctrl der.", "Right Ctrl", shortEs: "Ctl der.", shortEn: "R Ctl"),
        Entry("altgr", "AltGr", "AltGr"),
        Entry("win", "Win", "Win"),
        Entry("left", "←", "←", spokenEs: "Flecha izquierda", spokenEn: "Left arrow"),
        Entry("delete", "Supr", "Delete"),
        Entry("t", "T", "T"),
    ]);

    public static TheoryData<string, KeyLabelStyle, string, string> Lines =>
        new()
        {
            // keys, style, language → text
            { "ctrl+shift+t", KeyLabelStyle.Full, "es", "Ctrl + Shift + T" },
            { "ctrl+shift+t", KeyLabelStyle.Abbreviated, "es", "Ctl+⇧+T" },
            { "rctrl+t", KeyLabelStyle.Full, "es", "Ctrl der. + T" },
            { "rctrl+t", KeyLabelStyle.Full, "en", "Right Ctrl + T" },
            { "rctrl+t", KeyLabelStyle.Abbreviated, "en", "R Ctl+T" },
            { "altgr+t", KeyLabelStyle.Full, "es", "AltGr + T" },
            { "win+left", KeyLabelStyle.Full, "es", "Win + ←" },
            { "win+left", KeyLabelStyle.Spoken, "es", "Win + Flecha izquierda" },
            { "win+left", KeyLabelStyle.Spoken, "en", "Win + Left arrow" },
            { "ctrl+delete", KeyLabelStyle.Full, "en", "Ctrl + Delete" },
            { "ctrl+char:ñ", KeyLabelStyle.Full, "es", "Ctrl + Ñ" },
            { "ctrl+t", KeyLabelStyle.Full, "fr", "Ctrl + T" },
        };

    [Theory]
    [MemberData(nameof(Lines))]
    public void A_combination_is_written_with_the_catalog_labels(
        string keys,
        KeyLabelStyle style,
        string language,
        string expected
    )
    {
        KeyChordFormatter
            .Format(Chords.Of(keys.Split('+')), Labels, style, new LangCode(language), LangCode.Es)
            .ShouldBe(expected);
    }

    [Fact]
    public void The_empty_combination_is_empty_and_an_unknown_key_keeps_its_id()
    {
        KeyChordFormatter
            .Format(KeyChord.Empty, Labels, KeyLabelStyle.Full, LangCode.Es, LangCode.Es)
            .ShouldBeEmpty();
        KeyChordFormatter
            .Format(
                Chords.Of("f5"),
                KeyLabelCatalog.Empty,
                KeyLabelStyle.Full,
                LangCode.Es,
                LangCode.Es
            )
            .ShouldBe("f5");
    }

    private static KeyValuePair<KeyId, KeyLabel> Entry(
        string id,
        string es,
        string en,
        string? shortEs = null,
        string? shortEn = null,
        string? spokenEs = null,
        string? spokenEn = null
    ) =>
        KeyValuePair.Create(
            new KeyId(id),
            new KeyLabel(
                Text(es, en),
                shortEs is null ? null : Text(shortEs, shortEn!),
                spokenEs is null ? null : Text(spokenEs, spokenEn!)
            )
        );

    private static LocalizedText Text(string es, string en) =>
        new([new(LangCode.Es, es), new(LangCode.En, en)]);
}
