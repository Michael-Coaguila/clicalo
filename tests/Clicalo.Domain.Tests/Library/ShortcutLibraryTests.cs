using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Generators;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Library;

/// <summary>The operations of the aggregate and the invariants I1 to I6 they keep (blueprint §6.2).</summary>
public sealed class ShortcutLibraryTests
{
    private static readonly ListRef Always = new ListRef.AlwaysVisible();

    [Fact]
    [Trait("Req", "DAT-005")]
    public void A_library_without_General_is_refused()
    {
        var result = ShortcutLibrary.CreateValidated([], [Profile("word", "Word", null)]);

        result.Failure.Code.ShouldBe("library.invalid.fixed_lists");
    }

    [Fact]
    [Trait("Req", "DAT-004")]
    public void Ids_are_unique_in_the_whole_document()
    {
        ShortcutLibrary
            .CreateValidated(
                [],
                [DomainGen.General(Tap("a", "A", KeyIds.A), Tap("a", "B", KeyIds.B))]
            )
            .Failure.Code.ShouldBe("library.invalid.unique_ids");
        ShortcutLibrary
            .CreateValidated(
                [],
                [DomainGen.General(Tap("word", "A", KeyIds.A)), Profile("word", "Word", null)]
            )
            .Failure.Code.ShouldBe("library.invalid.unique_ids");
        ShortcutLibrary
            .CreateValidated(
                [],
                [DomainGen.General(), Profile("word", "Word", null), Profile("word", "Otro", null)]
            )
            .Failure.Code.ShouldBe("library.invalid.unique_ids");
    }

    [Fact]
    [Trait("Req", "DAT-005")]
    public void A_shortcut_lives_in_one_list()
    {
        var result = ShortcutLibrary.CreateValidated(
            [Tap("a", "A", KeyIds.A)],
            [DomainGen.General(Tap("a", "A", KeyIds.A))]
        );

        result.Failure.Code.ShouldBe("library.invalid.single_list");
    }

    [Fact]
    [Trait("Req", "DAT-005")]
    public void General_never_has_a_process()
    {
        var general = DomainGen.General() with
        {
            Binding = new AppBinding.Processes([new ProcessName("notepad.exe")]),
        };

        ShortcutLibrary
            .CreateValidated([], [general])
            .Failure.Code.ShouldBe("library.invalid.general_unbound");
    }

    [Theory]
    [Trait("Req", "DAT-005")]
    [InlineData("WINWORD.EXE", "winword.exe")]
    [InlineData("chrome.exe", "Chrome.EXE")]
    public void Two_profiles_never_share_a_process(string first, string second)
    {
        var result = ShortcutLibrary.CreateValidated(
            [],
            [DomainGen.General(), Profile("a", "A", first), Profile("b", "B", second)]
        );

        result.Failure.Code.ShouldBe("library.invalid.process_owned_once");
    }

    [Fact]
    public void The_sample_library_is_valid_and_indexed()
    {
        var library = Sample();

        library.FindViolations().ShouldBeEmpty();
        library.General.Id.ShouldBe(ProfileId.General);
        library.TryLocate(new ShortcutId("save"), out var location).ShouldBeTrue();
        location.ShouldBe(new ShortcutLocation(new ListRef.InProfile(new ProfileId("word")), 1));
        library.TryGetShortcut(new ShortcutId("copy"), out var copy).ShouldBeTrue();
        copy.Id.Value.ShouldBe("copy");
        library.TryLocate(new ShortcutId("nope"), out _).ShouldBeFalse();
        library
            .EnumerateShortcuts()
            .Select(s => s.Shortcut.Id.Value)
            .ShouldBe(["copy", "undo", "bold", "save", "newtab"]);
    }

    [Theory]
    [Trait("Req", "PER-002")]
    [InlineData("winword.exe", "word")]
    [InlineData("WINWORD.EXE", "word")]
    [InlineData("Chrome.exe", "chrome")]
    [InlineData("taskmgr.exe", null)]
    [InlineData("", null)]
    public void The_profile_of_a_process_ignores_case_and_General_never_matches(
        string process,
        string? profile
    ) => (Sample().ProfileFor(new ProcessName(process))?.Id.Value).ShouldBe(profile);

    [Fact]
    [Trait("Req", "DAT-004")]
    public void Adding_a_shortcut_with_a_used_id_fails()
    {
        var library = Sample();

        library
            .AddShortcut(Always, Tap("bold", "Otro", KeyIds.B), ListPosition.End)
            .Failure.Code.ShouldBe("library.id.duplicate");
        library
            .AddShortcut(Always, Tap("word", "Otro", KeyIds.B), ListPosition.End)
            .Failure.Code.ShouldBe("library.id.duplicate");
        library
            .AddShortcut(
                new ListRef.InProfile(new ProfileId("ghost")),
                Tap("x", "X", KeyIds.X),
                ListPosition.End
            )
            .Failure.Code.ShouldBe("library.profile.not_found");
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(99, 2)]
    [InlineData(null, 2)]
    public void Positions_outside_the_list_are_clamped(int? index, int expected)
    {
        var word = new ListRef.InProfile(new ProfileId("word"));

        var library = Sample()
            .AddShortcut(word, Tap("x", "X", KeyIds.X), new ListPosition(index))
            .Value;

        library.TryLocate(new ShortcutId("x"), out var location).ShouldBeTrue();
        location.Index.ShouldBe(expected);
    }

    [Fact]
    public void An_operation_that_changes_nothing_returns_the_same_library()
    {
        var library = Sample();
        library.TryGetShortcut(new ShortcutId("bold"), out var bold).ShouldBeTrue();

        library.ReplaceShortcut(bold).Value.ShouldBeSameAs(library);
        library
            .MoveShortcut(bold.Id, new ListRef.InProfile(new ProfileId("word")), ListPosition.At(0))
            .Value.ShouldBeSameAs(library);
        library
            .Bind(new ProfileId("word"), new ProcessName("winword.exe"), takeOver: false)
            .Value.ShouldBeSameAs(library);
        library
            .Unbind(new ProfileId("word"), new ProcessName("excel.exe"))
            .Value.ShouldBeSameAs(library);
        library
            .MoveProfile(new ProfileId("word"), ListPosition.At(1))
            .Value.ShouldBeSameAs(library);
    }

    [Fact]
    [Trait("Req", "DAT-005")]
    public void Moving_is_atomic_and_never_copies()
    {
        var library = Sample();

        var moved = library.MoveShortcut(new ShortcutId("bold"), Always, ListPosition.End).Value;

        moved.EnumerateShortcuts().Count().ShouldBe(library.EnumerateShortcuts().Count());
        moved.TryLocate(new ShortcutId("bold"), out var location).ShouldBeTrue();
        location.ShouldBe(new ShortcutLocation(Always, 1));
        moved.TryGetProfile(new ProfileId("word"), out var word).ShouldBeTrue();
        word.Shortcuts.Select(s => s.Id.Value).ShouldBe(["save"]);
    }

    [Fact]
    public void Moving_within_a_list_places_it_among_the_others()
    {
        var word = new ListRef.InProfile(new ProfileId("word"));

        var moved = Sample().MoveShortcut(new ShortcutId("bold"), word, ListPosition.End).Value;

        moved.TryGetProfile(new ProfileId("word"), out var profile).ShouldBeTrue();
        profile.Shortcuts.Select(s => s.Id.Value).ShouldBe(["save", "bold"]);
    }

    [Fact]
    [Trait("Req", "DAT-005")]
    public void General_cannot_be_removed_and_a_profile_goes_with_its_shortcuts()
    {
        var library = Sample();

        library.RemoveProfile(ProfileId.General).Failure.Code.ShouldBe("library.general.protected");
        var removed = library.RemoveProfile(new ProfileId("word")).Value;
        removed.TryLocate(new ShortcutId("bold"), out _).ShouldBeFalse();
        removed.Profiles.Select(p => p.Id.Value).ShouldBe(["general", "chrome"]);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Editing_a_profile_keeps_its_shortcuts_and_refuses_an_empty_name()
    {
        var library = Sample();
        library.TryGetProfile(new ProfileId("word"), out var word).ShouldBeTrue();

        var renamed = library
            .ReplaceProfile(
                word with
                {
                    Name = LocalizedText.Same("Texto", LangCode.Es),
                    Shortcuts = [],
                }
            )
            .Value;

        renamed.TryGetProfile(word.Id, out var edited).ShouldBeTrue();
        edited.Name.Get(LangCode.Es, LangCode.En).ShouldBe("Texto");
        edited.Shortcuts.Count.ShouldBe(2);
        library
            .ReplaceProfile(word with { Name = LocalizedText.Same("  ", LangCode.Es, LangCode.En) })
            .Failure.Code.ShouldBe("library.profile.name_empty");
    }

    [Fact]
    [Trait("Req", "DAT-005")]
    public void General_cannot_be_bound_to_a_process()
    {
        var library = Sample();

        library
            .Bind(ProfileId.General, new ProcessName("notepad.exe"), takeOver: true)
            .Failure.Code.ShouldBe("library.general.unbound");
        library
            .ReplaceProfile(
                library.General with
                {
                    Binding = new AppBinding.Processes([new ProcessName("x.exe")]),
                }
            )
            .Failure.Code.ShouldBe("library.general.unbound");
    }

    [Fact]
    [Trait("Req", "ATJ-007")]
    public void A_process_of_another_profile_is_only_taken_over_when_asked()
    {
        var library = Sample();
        var chrome = new ProfileId("chrome");

        library
            .Bind(chrome, new ProcessName("winword.exe"), takeOver: false)
            .Failure.Code.ShouldBe("library.process.bound");

        var taken = library.Bind(chrome, new ProcessName("winword.exe"), takeOver: true).Value;

        taken.ProfileFor(new ProcessName("WINWORD.EXE"))!.Id.ShouldBe(chrome);
        taken.TryGetProfile(new ProfileId("word"), out var word).ShouldBeTrue();
        word.Binding.ShouldBe(new AppBinding.Manual());
        taken.FindViolations().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ATJ-006")]
    public void Unbinding_the_last_process_leaves_the_profile_manual()
    {
        var unbound = Sample().Unbind(new ProfileId("chrome"), new ProcessName("CHROME.EXE")).Value;

        unbound.TryGetProfile(new ProfileId("chrome"), out var chrome).ShouldBeTrue();
        chrome.Binding.ShouldBe(new AppBinding.Manual());
    }

    [Fact]
    [Trait("Req", "DAT-004")]
    public void A_new_profile_needs_unused_ids_a_name_and_free_processes()
    {
        var library = Sample();

        library
            .AddProfile(Profile("word", "Otro", null), ListPosition.End)
            .Failure.Code.ShouldBe("library.id.duplicate");
        library
            .AddProfile(Profile("x", "X", null, Tap("bold", "B", KeyIds.B)), ListPosition.End)
            .Failure.Code.ShouldBe("library.id.duplicate");
        library
            .AddProfile(Profile("x", " ", null), ListPosition.End)
            .Failure.Code.ShouldBe("library.profile.name_empty");
        library
            .AddProfile(Profile("x", "X", "Winword.exe"), ListPosition.End)
            .Failure.Code.ShouldBe("library.process.bound");
        library
            .AddProfile(Profile("x", "X", "excel.exe"), ListPosition.At(1))
            .Value.Profiles[1]
            .Id.Value.ShouldBe("x");
    }

    [Fact]
    public void Profiles_can_be_reordered()
    {
        var moved = Sample().MoveProfile(new ProfileId("chrome"), ListPosition.At(0)).Value;

        moved.Profiles.Select(p => p.Id.Value).ShouldBe(["chrome", "general", "word"]);
    }

    [Fact]
    public void Libraries_compare_by_value()
    {
        Sample().ShouldBe(Sample());
        Sample().GetHashCode().ShouldBe(Sample().GetHashCode());
    }
}
