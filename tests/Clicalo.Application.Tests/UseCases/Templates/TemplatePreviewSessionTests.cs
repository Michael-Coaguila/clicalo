using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Sharing;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.UseCases.Templates;

/// <summary>
/// The preview of Plantillas and its install (PLA-013 to PLA-017, LOG-008, DAT-004): checked rows, renames, the
/// programs language, «Añadir los que faltan» and one undo step per install.
/// </summary>
public sealed class TemplatePreviewSessionTests
{
    private static readonly ProcessName WordProcess = new("winword.exe");

    private static readonly ProfileTemplate Word = new(
        "word",
        2,
        LocalizedText.Same("Word", LangCode.Es, LangCode.En),
        new IconRef("description"),
        [WordProcess],
        [
            Item(
                "bold",
                "Negrita",
                new TapAction(
                    KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N),
                    [new ChordVariant(LangCode.En, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B))]
                )
            ),
            Item("save", "Guardar", new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G), [])),
            Item("close", "Cerrar", new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.W), [])),
        ]
    )
    {
        AppsLanguages = [LangCode.Es],
    };

    private static TemplateShortcut Item(string id, string name, ShortcutAction action) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            new CategoryId("edit"),
            action,
            false
        );

    private static StoreHarness Harness(LangCode? appsLanguage = null)
    {
        var defaults = SettingsSchema.Defaults;
        var settings = defaults with
        {
            Keyboard = defaults.Keyboard with { AppsLanguage = appsLanguage ?? LangCode.Es },
        };
        var library = ShortcutLibrary.CreateValidated([], [DomainGen.General()]).Value;
        return new StoreHarness(UserDocument.Create(library, settings));
    }

    [Fact]
    [Trait("Req", "PLA-013")]
    [Trait("Req", "PLA-015")]
    [Trait("Req", "DAT-004")]
    public void Installing_creates_the_profile_with_the_checked_and_renamed_shortcuts_in_one_undo_step()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);
        session.Toggle(2);
        session.Rename(0, "Mi negrita");

        session.Count.ShouldBe(2);
        session.Action.ShouldBe(PreviewAction.Install);
        var outcome = session.Install(LangCode.Es).Value;

        var profile = harness.Store.Current.Library.ProfileFor(WordProcess).ShouldNotBeNull();
        outcome.Profile.ShouldBe(profile.Id);
        profile.Origin.ShouldNotBeNull().Source.ShouldBe("word");
        profile
            .Shortcuts.Select(s => s.Name.Get(LangCode.Es, LangCode.Es))
            .ShouldBe(["Mi negrita", "Guardar"]);
        profile
            .Shortcuts.Select(s => s.Name.Get(LangCode.En, LangCode.Es))
            .ShouldBe(["Mi negrita", "Guardar"]);
        profile.Shortcuts.ShouldAllBe(s =>
            !s.Id.Value.StartsWith("preview", StringComparison.Ordinal)
        );
        session.Source.ShouldBeNull("the preview closes after installing");
        harness.UndoAll().ShouldBe(1);
        harness.Store.Current.Library.ProfileFor(WordProcess).ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PLA-009")]
    [Trait("Req", "PLA-015")]
    public void The_programs_language_picks_the_variant_and_the_review_note()
    {
        var harness = Harness(LangCode.En);
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);

        session
            .Rows()[0]
            .Shortcut.Action.ShouldBe(new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.B), []));
        session.OnlyOtherLanguage.ShouldBeTrue("the template was reviewed only in Spanish");
        session.Rows()[2].Dangerous.ShouldBeTrue("Ctrl+W closes at once");
    }

    [Fact]
    [Trait("Req", "PLA-013")]
    [Trait("Req", "PLA-017")]
    public void An_installed_template_is_never_overwritten_and_offers_what_is_missing()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word with { Shortcuts = [Word.Shortcuts[0]] });
        _ = session.Install(LangCode.Es).Value;
        var installed = harness.Store.Current.Library.ProfileFor(WordProcess)!.Id;

        session.ShowTemplate(Word);
        var rows = session.Rows();
        rows[0].AlreadyIn.ShouldBeTrue();
        rows[0].Checked.ShouldBeFalse();
        session.Action.ShouldBe(PreviewAction.AddMissing);
        session.Count.ShouldBe(2);
        _ = session.Install(LangCode.Es).Value;

        var library = harness.Store.Current.Library;
        library.Profiles.Count.ShouldBe(2, "no second profile");
        library.TryGetProfile(installed, out var profile).ShouldBeTrue();
        profile!.Shortcuts.Count.ShouldBe(3);
        session.ShowTemplate(Word);
        session.Action.ShouldBe(PreviewAction.EditShortcuts);
        session.Count.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "PLA-007")]
    public void An_ai_proposal_is_created_without_catalog_references()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowAi(
            new AiTemplate(Word with { Id = TemplateSchema.AiTemplateId }, Known: false)
        );

        session.IsUnknown.ShouldBeTrue();
        session.Action.ShouldBe(PreviewAction.CreateWith);
        var outcome = session.Install(LangCode.Es).Value;

        harness
            .Store.Current.Library.TryGetProfile(outcome.Profile, out var profile)
            .ShouldBeTrue();
        profile!.Origin.ShouldBeNull();
        profile.Shortcuts.ShouldAllBe(s => s.Origin == null);
    }

    [Fact]
    [Trait("Req", "PLA-015")]
    [Trait("Req", "LOG-008")]
    [Trait("Req", "DAT-007")]
    public void A_shared_profile_starts_with_its_risky_shortcuts_unchecked()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        var tap = TemplateInstaller.Install(
            Word.Shortcuts[1],
            "word",
            1,
            LangCode.Es,
            new SequentialIds()
        );
        var text = tap with
        {
            Id = new ShortcutId("t"),
            Action = new TextAction(SecretText.From("hola"), TextMethod.Unicode),
        };
        var shared = new Profile(
            new ProfileId("shared"),
            LocalizedText.Same("Compartido", LangCode.Es, LangCode.En),
            new IconRef("apps"),
            false,
            new AppBinding.Processes([new ProcessName("notepad.exe")]),
            InjectionMode.VirtualKey,
            [tap, text],
            null
        );
        session.ShowShared(new SharedProfile(shared, 0));

        var rows = session.Rows();
        rows[0].Checked.ShouldBeTrue();
        rows[1].Risky.ShouldBeTrue();
        rows[1].Checked.ShouldBeFalse();
        session.Toggle(1);
        session.Count.ShouldBe(2);
        _ = session.Install(LangCode.Es).Value;

        harness
            .Store.Current.Library.ProfileFor(new ProcessName("notepad.exe"))
            .ShouldNotBeNull()
            .Shortcuts.Count.ShouldBe(2);
    }

    [Fact]
    public void Nothing_checked_installs_nothing()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word with { Shortcuts = [Word.Shortcuts[1]] });
        session.Toggle(0);

        session.Install(LangCode.Es).IsSuccess.ShouldBeFalse();
        harness.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PLA-012")]
    public void Install_all_installs_without_touching_another_preview()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        var other = Word with { Id = "notes", Processes = [new ProcessName("notepad.exe")] };
        session.ShowTemplate(other);
        session.Toggle(0);

        _ = session.InstallAll(Word, LangCode.Es).Value;

        harness
            .Store.Current.Library.ProfileFor(WordProcess)
            .ShouldNotBeNull()
            .Shortcuts.Count.ShouldBe(3);
        session.TemplateId.ShouldBe("notes");
        session.Rows()[0].Checked.ShouldBeFalse("the other preview keeps its checks");
    }

    [Fact]
    [Trait("Req", "PLA-010")]
    public void A_blank_profile_binds_a_free_app_and_needs_a_name()
    {
        var harness = Harness();

        BlankProfiles
            .Create(harness.Store, "  ", new IconRef("apps"), true, null)
            .IsSuccess.ShouldBeFalse();
        var id = BlankProfiles
            .Create(harness.Store, " Diseño ", new IconRef("brush"), false, WordProcess)
            .Value;

        harness.Store.Current.Library.TryGetProfile(id, out var profile).ShouldBeTrue();
        profile!.Name.ShouldBe(LocalizedText.Same("Diseño", LangCode.Es, LangCode.En));
        profile.Binding.ShouldBe(new AppBinding.Processes([WordProcess]));
        profile.Shortcuts.ShouldBeEmpty();
    }
}
