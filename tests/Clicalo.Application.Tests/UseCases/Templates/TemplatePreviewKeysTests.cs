using Clicalo.Application.Tests.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Templates;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Templates;
using Clicalo.Domain.Tests.Generators;

namespace Clicalo.Application.Tests.UseCases.Templates;

/// <summary>
/// The keys of the preview (PLA-016), «Actualizar a la variante» (EC-PLA-04) and the plural of the notice of the
/// missing shortcuts (IDI-004).
/// </summary>
public sealed class TemplatePreviewKeysTests
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
            Item(
                "italic",
                "Cursiva",
                new TapAction(
                    KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.K),
                    [new ChordVariant(LangCode.En, KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.I))]
                )
            ),
            Item("save", "Guardar", new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.G), [])),
        ]
    )
    {
        AppsLanguages = [LangCode.Es, LangCode.En],
    };

    [Fact]
    [Trait("Req", "PLA-016")]
    public void The_keys_chosen_in_the_preview_are_the_ones_installed()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);

        session.EditChord(0, chord => ChordEdits.Tap(chord, KeyIds.Shift));
        session.EditChord(2, static _ => KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S));

        Keys(session.Rows()[0].Shortcut).ShouldBe([KeyIds.Ctrl, KeyIds.N, KeyIds.Shift]);
        _ = session.Install(LangCode.Es).Value;
        var profile = harness.Store.Current.Library.ProfileFor(WordProcess).ShouldNotBeNull();
        Keys(profile.Shortcuts[0]).ShouldBe([KeyIds.Ctrl, KeyIds.N, KeyIds.Shift]);
        Keys(profile.Shortcuts[1]).ShouldBe([KeyIds.Ctrl, KeyIds.K], "untouched rows keep theirs");
        Keys(profile.Shortcuts[2]).ShouldBe([KeyIds.Ctrl, KeyIds.S]);
        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        harness.Store.Current.Library.ProfileFor(WordProcess).ShouldBeNull("one undo step");
    }

    [Fact]
    [Trait("Req", "PLA-016")]
    public void A_row_that_is_already_in_keeps_its_keys_and_another_preview_starts_clean()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);
        session.Toggle(1);
        session.Toggle(2);
        _ = session.Install(LangCode.Es).Value;

        session.ShowTemplate(Word);
        session.EditChord(0, static _ => KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S));
        session.EditChord(1, static _ => KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.S));

        var rows = session.Rows();
        rows[0].AlreadyIn.ShouldBeTrue();
        Keys(rows[0].Shortcut).ShouldBe([KeyIds.Ctrl, KeyIds.N]);
        Keys(rows[1].Shortcut).ShouldBe([KeyIds.Ctrl, KeyIds.S]);
        session.ShowTemplate(Word);
        Keys(session.Rows()[1].Shortcut).ShouldBe([KeyIds.Ctrl, KeyIds.K]);
    }

    [Fact]
    [Trait("Req", "EC-PLA-04")]
    [Trait("Req", "IDI-004")]
    public void Updating_to_the_variant_changes_only_the_keys_of_the_other_language_in_one_undo_step()
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);
        _ = session.Install(LangCode.Es).Value;
        var installed = harness.Store.Current.Library.ProfileFor(WordProcess).ShouldNotBeNull();
        // The person changed Cursiva by hand: it is theirs and no variant touches it.
        var custom = installed.Shortcuts[1] with
        {
            Action = new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.Alt, KeyIds.K), []),
        };
        harness.Store.Dispatch(new EditShortcut(custom)).IsSuccess.ShouldBeTrue();
        session.ShowTemplate(Word);
        session.StaleVariants.ShouldBe(0, "the programs are still in Spanish");

        harness
            .Store.Dispatch(new SetSetting(SettingPaths.AppsLanguage, LangCode.En))
            .IsSuccess.ShouldBeTrue();
        session.ShowTemplate(Word);

        // EC-PLA-04: the installed shortcuts did not change by themselves.
        var before = harness.Store.Current.Library.ProfileFor(WordProcess).ShouldNotBeNull();
        Keys(before.Shortcuts[0]).ShouldBe([KeyIds.Ctrl, KeyIds.N]);
        session.StaleVariants.ShouldBe(1);
        var outcome = session.UpdateVariant(LangCode.Es).Value;

        var after = harness.Store.Current.Library.ProfileFor(WordProcess).ShouldNotBeNull();
        Keys(after.Shortcuts[0]).ShouldBe([KeyIds.Ctrl, KeyIds.B]);
        Keys(after.Shortcuts[1]).ShouldBe([KeyIds.Ctrl, KeyIds.Alt, KeyIds.K]);
        Keys(after.Shortcuts[2]).ShouldBe([KeyIds.Ctrl, KeyIds.G], "no variant: it stays");
        after.Shortcuts[0].Id.ShouldBe(before.Shortcuts[0].Id);
        I18nRepository
            .Localizer("es")
            .Format(outcome.Notice.ShouldNotBeNull())
            .ShouldBe("Actualizado 1 atajo de Word");
        session.StaleVariants.ShouldBe(0);
        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        Keys(harness.Store.Current.Library.ProfileFor(WordProcess)!.Shortcuts[0])
            .ShouldBe([KeyIds.Ctrl, KeyIds.N]);
    }

    [Theory]
    [InlineData(1, "Añadido 1 atajo a Word")]
    [InlineData(2, "Añadidos 2 atajos a Word")]
    [Trait("Req", "IDI-004")]
    [Trait("Req", "PLA-017")]
    public void Adding_the_missing_ones_says_how_many_with_the_right_plural(
        int missing,
        string expected
    )
    {
        var harness = Harness();
        var session = new TemplatePreviewSession(harness.Store);
        session.ShowTemplate(Word);
        for (var row = Word.Shortcuts.Count - missing; row < Word.Shortcuts.Count; row++)
        {
            session.Toggle(row);
        }

        _ = session.Install(LangCode.Es).Value;
        session.ShowTemplate(Word);
        session.Action.ShouldBe(PreviewAction.AddMissing);

        var outcome = session.Install(LangCode.Es).Value;

        I18nRepository.Localizer("es").Format(outcome.Notice.ShouldNotBeNull()).ShouldBe(expected);
    }

    private static IEnumerable<KeyId> Keys(Shortcut shortcut) =>
        ActionKinds.ChordOf(shortcut.Action).ShouldNotBeNull().Strokes.Select(s => s.Key);

    private static TemplateShortcut Item(string id, string name, ShortcutAction action) =>
        new(
            id,
            LocalizedText.Same(name, LangCode.Es, LangCode.En),
            new IconRef("bolt"),
            new CategoryId("edit"),
            action,
            false
        );

    private static StoreHarness Harness()
    {
        var library = ShortcutLibrary.CreateValidated([], [DomainGen.General()]).Value;
        return new StoreHarness(UserDocument.Create(library, SettingsSchema.Defaults));
    }
}
