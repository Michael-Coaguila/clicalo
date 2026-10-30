using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Domain.Tests.Templates;

/// <summary>The rules of the preview of Plantillas (PLA-013, PLA-015, PLA-017, LOG-008).</summary>
public sealed class TemplatePreviewRulesTests
{
    private static Shortcut Tap(string item, params KeyId[] keys) =>
        new(
            new ShortcutId(item),
            LocalizedText.Same(item, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            false,
            new CategoryId("edit"),
            new TapAction(KeyChord.FromKeys(keys), []),
            new ShortcutOptions(false, new HoldLimit.InheritGlobal(), false),
            new CatalogRef("word", "1", item),
            null
        );

    private static Profile Installed(params Shortcut[] shortcuts) =>
        new(
            new ProfileId("p"),
            LocalizedText.Same("Word", LangCode.Es, LangCode.En),
            new IconRef("description"),
            false,
            new AppBinding.Manual(),
            InjectionMode.VirtualKey,
            [.. shortcuts],
            new CatalogRef("word", "1", "word")
        );

    [Theory]
    [Trait("Req", "PLA-017")]
    [InlineData(false, 0, false, PreviewAction.Install)]
    [InlineData(false, 0, true, PreviewAction.CreateWith)]
    [InlineData(true, 2, false, PreviewAction.AddMissing)]
    [InlineData(true, 0, false, PreviewAction.EditShortcuts)]
    public void The_final_button_follows_the_state(
        bool installed,
        int missing,
        bool fromAi,
        PreviewAction expected
    ) => TemplatePreviewRules.ActionFor(installed, missing, fromAi).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PLA-015")]
    public void A_shortcut_is_already_in_by_its_catalog_item_or_by_its_combination()
    {
        var profile = Installed(Tap("bold", KeyIds.Ctrl, KeyIds.N));

        TemplatePreviewRules
            .IsAlreadyIn(profile, Tap("bold", KeyIds.Ctrl, KeyIds.B))
            .ShouldBeTrue();
        TemplatePreviewRules
            .IsAlreadyIn(profile, Tap("other", KeyIds.Ctrl, KeyIds.N) with { Origin = null })
            .ShouldBeTrue();
        TemplatePreviewRules
            .IsAlreadyIn(profile, Tap("italic", KeyIds.Ctrl, KeyIds.K))
            .ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PLA-013")]
    public void The_installed_template_is_found_by_its_catalog_reference()
    {
        var library = ShortcutLibrary
            .CreateValidated([], [Generators.DomainGen.General(), Installed()])
            .Value;

        TemplatePreviewRules
            .InstalledFrom(library, "word")
            .ShouldNotBeNull()
            .Id.Value.ShouldBe("p");
        TemplatePreviewRules.InstalledFrom(library, "excel").ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "LOG-008")]
    public void Web_app_macro_and_text_are_risky_and_taps_are_not()
    {
        var tap = Tap("t", KeyIds.Ctrl, KeyIds.C);

        TemplatePreviewRules.IsRisky(tap).ShouldBeFalse();
        TemplatePreviewRules
            .IsRisky(
                tap with
                {
                    Action = new TextAction(Privacy.SecretText.From("hola"), TextMethod.Unicode),
                }
            )
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "PLA-015")]
    public void An_empty_rename_keeps_the_original_name()
    {
        var original = new LocalizedText([new(LangCode.Es, "Negrita"), new(LangCode.En, "Bold")]);

        TemplatePreviewRules.NameFor(original, "  ").ShouldBe(original);
        TemplatePreviewRules
            .NameFor(original, " Mi negrita ")
            .ShouldBe(LocalizedText.Same("Mi negrita", LangCode.Es, LangCode.En));
    }

    [Theory]
    [Trait("Req", "PLA-009")]
    [InlineData("es-ES", "es-ES")]
    [InlineData("es-MX", "es-LA")]
    [InlineData("es-419", "es-LA")]
    [InlineData("en-GB", "en-US")]
    [InlineData(null, "en-US")]
    public void The_layout_is_detected_from_the_input_language(string? culture, string expected) =>
        KeyboardLayouts.Detect(culture).ShouldBe(expected);

    [Fact]
    [Trait("Req", "PLA-009")]
    public void The_programs_language_and_the_effective_layout_are_detected()
    {
        KeyboardLayouts.DetectAppsLanguage("es-PE").ShouldBe(LangCode.Es);
        KeyboardLayouts.DetectAppsLanguage("de-DE").ShouldBe(LangCode.En);
        KeyboardLayouts.Effective(string.Empty, "es-LA").ShouldBe("es-LA");
        KeyboardLayouts.Effective("en-INT", "es-LA").ShouldBe("en-INT");
    }
}
