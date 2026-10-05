using Clicalo.Application.UseCases;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.UseCases;

/// <summary>
/// The document of a first installation (user decision D2 of 2026-10-03): the welcome's selection, or the options
/// marked by default when it is skipped or does not exist yet, with the settings given.
/// </summary>
[Trait("Req", "BIE-006")]
[Trait("Req", "CAT-003")]
public sealed class FirstDocumentTests
{
    private static readonly StarterContent Content = new(
        new StarterKit(
            1,
            [
                new StarterOption("basics", StarterOptionKind.Basics, true),
                new StarterOption("word", StarterOptionKind.Template, false),
            ]
        ),
        new SeedContent(
            1,
            [Item("dict", KeyIds.Win, KeyIds.H)],
            LocalizedText.Same("General", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            [Item("copy", KeyIds.Ctrl, KeyIds.C)]
        ),
        [
            new ProfileTemplate(
                "word",
                1,
                LocalizedText.Same("Word", LangCode.Es, LangCode.En),
                new IconRef("description"),
                [new ProcessName("winword.exe")],
                [
                    new TemplateShortcut(
                        "bold",
                        LocalizedText.Same("Bold", LangCode.Es, LangCode.En),
                        new IconRef("format_bold"),
                        new CategoryId("fmt"),
                        new TapAction(
                            KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N),
                            [
                                new ChordVariant(
                                    LangCode.En,
                                    KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B)
                                ),
                            ]
                        ),
                        false
                    ),
                ]
            ),
        ]
    );

    [Fact]
    [Trait("Req", "BIE-003")]
    public void Skipping_or_a_first_start_installs_basics_only()
    {
        var english = SettingsSchema.Defaults with { Language = LangCode.En };

        var document = FirstDocument.CreateDefault(Content, english, new SequentialIds()).Value;

        document.Settings.ShouldBe(english);
        document.Validate().ShouldBeEmpty();
        document.Onboarding.Completed.ShouldBeFalse();
        document.Library.AlwaysVisible.Count.ShouldBe(1);
        document.Library.Profiles.ShouldHaveSingleItem().Shortcuts.Count.ShouldBe(1);
    }

    [Fact]
    public void Nothing_marked_starts_with_general_and_always_visible_empty()
    {
        var document = FirstDocument
            .Create(Content, StarterSelection.Empty, SettingsSchema.Defaults, new SequentialIds())
            .Value;

        document.Library.AlwaysVisible.ShouldBeEmpty();
        document.Library.Profiles.ShouldHaveSingleItem().Shortcuts.ShouldBeEmpty();
        document.Validate().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "PLA-009")]
    [Trait("Req", "CAT-005")]
    public void A_marked_template_uses_the_programs_language_of_the_settings()
    {
        var settings = SettingsSchema.Defaults with
        {
            Keyboard = SettingsSchema.Defaults.Keyboard with { AppsLanguage = LangCode.En },
        };

        var document = FirstDocument
            .Create(Content, StarterSelection.Of(["word"]), settings, new SequentialIds())
            .Value;

        var word = document.Library.Profiles[1];
        word.Binding.ShouldBe(new AppBinding.Processes([new ProcessName("winword.exe")]));
        ((TapAction)word.Shortcuts.ShouldHaveSingleItem().Action).Chord.ShouldBe(
            KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B)
        );
    }

    private static TemplateShortcut Item(string id, params KeyId[] keys) =>
        new(
            id,
            LocalizedText.Same(id, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            false
        );
}
