using System.Text;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Content;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Content;

/// <summary>
/// The document of a new installation from <c>data/content/seed.json</c> (CAT-003, PQ-44), read at run time and
/// validated again because content is untrusted (LOG-006).
/// </summary>
[Trait("Req", "CAT-003")]
public sealed class SeedDocumentTests
{
    private static readonly byte[] Seed = File.ReadAllBytes(
        Path.Combine(RepoPaths.Data, "content", SeedDocument.FileName)
    );

    [Fact]
    public void The_shipped_seed_is_the_always_visible_row_and_general()
    {
        var document = SeedDocument.Read(Seed, SettingsSchema.Defaults).ShouldNotBeNull();

        document.Validate().ShouldBeEmpty();
        document.Revision.ShouldBe(0);
        document.Onboarding.Completed.ShouldBeFalse();
        var library = document.Library;
        library
            .AlwaysVisible.Select(static s => s.Id.Value)
            .ShouldBe(["dict", "ptt", "mute", "desk"]);
        library.Profiles.ShouldHaveSingleItem().Id.ShouldBe(ProfileId.General);
        library.General.Name.Get(LangCode.Es, LangCode.En).ShouldBe("General");
        library.General.Shortcuts.Count.ShouldBe(12);
    }

    [Fact]
    public void Every_kind_of_the_seed_keeps_its_meaning()
    {
        var library = SeedDocument.Read(Seed, SettingsSchema.Defaults).ShouldNotBeNull().Library;

        Find(library, "copy").Action.ShouldBe(new TapAction(Chord(KeyIds.Ctrl, KeyIds.C), []));
        Find(library, "ptt").Action.ShouldBe(new HoldAction(Chord(KeyIds.Ctrl, KeyIds.Space)));
        Find(library, "shift").Action.ShouldBe(new ToggleAction(Chord(KeyIds.Shift)));
        Find(library, "drag").Action.ShouldBe(new MouseAction(MouseOp.Drag, ScrollSpeed.Normal));
        Find(library, "close")
            .Options.Confirm.ShouldBeTrue("Alt+F4 asks for a second tap (EJE-002)");
        Find(library, "copy").Options.Confirm.ShouldBeFalse();
        Find(library, "copy").Origin.ShouldBe(new CatalogRef(SeedDocument.Source, "1", "copy"));
        Find(library, "copy").Name.Get(LangCode.En, LangCode.Es).ShouldBe("Copy");
    }

    [Fact]
    public void The_settings_given_are_the_settings_of_the_document()
    {
        var english = SettingsSchema.Defaults with { Language = LangCode.En };

        SeedDocument.Read(Seed, english).ShouldNotBeNull().Settings.ShouldBe(english);
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("""{"catalogVersion":1}""")]
    [InlineData(
        """{"catalogVersion":1,"alwaysVisible":[],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[{"id":"a"}]}}"""
    )]
    [Trait("Req", "LOG-006")]
    public void A_broken_seed_gives_no_document(string json) =>
        SeedDocument.Read(Encoding.UTF8.GetBytes(json), SettingsSchema.Defaults).ShouldBeNull();

    [Fact]
    [Trait("Req", "LOG-006")]
    public void A_seed_that_breaks_the_library_invariants_gives_no_document()
    {
        const string twice =
            """{"catalogVersion":1,"alwaysVisible":[{"id":"a","name":{"es":"A"},"icon":"bolt","category":"edit","action":{"type":"tap","keys":["ctrl","c"]}}],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[{"id":"a","name":{"es":"A"},"icon":"bolt","category":"edit","action":{"type":"tap","keys":["ctrl","v"]}}]}}""";

        SeedDocument.Read(Encoding.UTF8.GetBytes(twice), SettingsSchema.Defaults).ShouldBeNull();
    }

    [Fact]
    public void A_kind_the_seed_does_not_use_is_left_out_rather_than_guessed()
    {
        const string web =
            """{"catalogVersion":1,"alwaysVisible":[],"general":{"name":{"es":"G"},"icon":"apps","shortcuts":[{"id":"w","name":{"es":"W"},"icon":"public","category":"web","action":{"type":"web","url":"https://example.org"}},{"id":"c","name":{"es":"C"},"icon":"bolt","category":"edit","action":{"type":"tap","keys":["ctrl","c"]}}]}}""";

        SeedDocument
            .Read(Encoding.UTF8.GetBytes(web), SettingsSchema.Defaults)
            .ShouldNotBeNull()
            .Library.General.Shortcuts.ShouldHaveSingleItem()
            .Id.ShouldBe(new ShortcutId("c"));
    }

    private static Shortcut Find(ShortcutLibrary library, string id) =>
        library.TryGetShortcut(new ShortcutId(id), out var shortcut)
            ? shortcut
            : throw new ShouldAssertException("The seed has no shortcut " + id + ".");

    private static KeyChord Chord(params KeyId[] keys) =>
        KeyChord.Create([.. keys.Select(static key => new KeyStroke(key))]);
}
