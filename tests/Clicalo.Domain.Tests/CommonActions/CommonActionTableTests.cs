using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.CommonActions;

/// <summary>
/// Decision D4 of the user (2026-10-05): a common action sends the combination of the app in front and the programs
/// language; any other app gets the standard one, and a shortcut with a combination of its own is sent as saved. The
/// table is the full grid of origin × saved combination × app × programs language.
/// </summary>
[Trait("Req", "EJE-018")]
[Trait("Req", "EJE-003")]
public sealed class CommonActionTableTests
{
    private static readonly CommonActionTable Table = new(
        ["seed", "library"],
        [
            new CommonAction(
                "save",
                Chords.Of("ctrl", "s"),
                [
                    new CommonActionOverride(
                        [new ProcessName("winword.exe"), new ProcessName("excel.exe")],
                        LangCode.Es,
                        Chords.Of("ctrl", "g")
                    ),
                ]
            ),
        ]
    );

    public static TheoryData<string?, string, string?, string?, string> Grid =>
        new()
        {
            // origin, saved combination, app, programs language → combination sent
            { "seed", "ctrl+s", "winword.exe", "es", "ctrl+g" },
            { "seed", "ctrl+s", "WINWORD.EXE", "es", "ctrl+g" },
            { "seed", "ctrl+s", "excel.exe", "es", "ctrl+g" },
            { "library", "ctrl+s", "winword.exe", "es", "ctrl+g" },
            { "seed", "ctrl+s", "winword.exe", "en", "ctrl+s" },
            { "seed", "ctrl+s", "chrome.exe", "es", "ctrl+s" },
            { "seed", "ctrl+s", null, "es", "ctrl+s" },
            { "seed", "ctrl+s", "winword.exe", null, "ctrl+s" },
            { "seed", "ctrl+g", "winword.exe", "es", "ctrl+g" },
            { "seed", "ctrl+g", "chrome.exe", "es", "ctrl+s" },
            { "seed", "ctrl+shift+s", "winword.exe", "es", "ctrl+shift+s" },
            { "seed", "ctrl+b", "winword.exe", "es", "ctrl+b" },
            { "word", "ctrl+s", "winword.exe", "es", "ctrl+s" },
            { null, "ctrl+s", "winword.exe", "es", "ctrl+s" },
        };

    [Theory]
    [MemberData(nameof(Grid))]
    public void A_common_action_follows_the_app_in_front_and_any_other_shortcut_is_sent_as_saved(
        string? origin,
        string saved,
        string? app,
        string? appsLanguage,
        string expected
    )
    {
        var shortcut = Save(origin, saved);

        var sent = Table.ChordToSend(
            shortcut,
            (TapAction)shortcut.Action,
            app is null ? null : new ProcessName(app),
            appsLanguage is null ? null : new LangCode(appsLanguage)
        );

        sent.ShouldBe(Chord(expected));
    }

    [Fact]
    public void A_shortcut_that_is_not_a_common_action_still_takes_its_programs_language_variant()
    {
        var bold = Shortcuts.Of(
            "bold",
            new TapAction(
                Chords.Of("ctrl", "n"),
                [new ChordVariant(LangCode.En, Chords.Of("ctrl", "b"))]
            )
        );

        Table
            .ChordToSend(bold, (TapAction)bold.Action, new ProcessName("winword.exe"), LangCode.En)
            .ShouldBe(Chords.Of("ctrl", "b"));
        Table
            .ChordToSend(bold, (TapAction)bold.Action, new ProcessName("winword.exe"), LangCode.Es)
            .ShouldBe(Chords.Of("ctrl", "n"));
    }

    [Fact]
    public void The_empty_table_sends_every_shortcut_as_saved()
    {
        var save = Save("seed", "ctrl+s");

        CommonActionTable
            .Empty.ChordToSend(
                save,
                (TapAction)save.Action,
                new ProcessName("winword.exe"),
                LangCode.Es
            )
            .ShouldBe(Chords.Of("ctrl", "s"));
    }

    [Fact]
    public void Only_taps_are_common_actions()
    {
        var hold = Shortcuts.Hold("save", "ctrl", "s") with
        {
            Origin = new CatalogRef("seed", "1", "save"),
        };

        Table.TryFind(hold, out _).ShouldBeFalse();
    }

    private static Shortcut Save(string? origin, string saved) =>
        Shortcuts.Of("s1", new TapAction(Chord(saved), [])) with
        {
            Origin = origin is null ? null : new CatalogRef(origin, "1", "save"),
        };

    private static KeyChord Chord(string keys) => Chords.Of(keys.Split('+'));
}
