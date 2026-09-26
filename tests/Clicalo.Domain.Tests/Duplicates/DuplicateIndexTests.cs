using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Generators;
using CsCheck;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Duplicates;

/// <summary>
/// Repeated shortcuts (REP-001 to REP-005), with the cases of docs/03 §9: the same combination is repeated when one is
/// in Always visible, when they share a list or when they share their name; never across profiles with other names.
/// </summary>
public sealed class DuplicateIndexTests
{
    [Fact]
    [Trait("Req", "REP-002")]
    public void One_appearance_in_Always_visible_makes_every_appearance_repeated()
    {
        var library = BuildLibrary(
            [Named("g", "Guardar", "Save", KeyIds.Ctrl, KeyIds.S)],
            [],
            Profile(
                "word",
                "Word",
                "winword.exe",
                Named("w", "Subrayar", "Underline", KeyIds.S, KeyIds.Ctrl)
            )
        );

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index.IsRepeated(Id("g")).ShouldBeTrue();
        index.IsRepeated(Id("w")).ShouldBeTrue();
        index.RepeatedCombinationCount.ShouldBe(1);
        index.AdviceFor(Id("w")).ShouldBe(DuplicateAdvice.AlwaysVisible);
    }

    [Fact]
    [Trait("Req", "REP-002")]
    public void The_same_list_makes_a_repetition_whatever_the_names()
    {
        var library = BuildLibrary(
            [],
            [
                Named("a", "Copiar", "Copy", KeyIds.Ctrl, KeyIds.C),
                Named("b", "Otro", "Other", KeyIds.LeftCtrl, KeyIds.C),
                Named("c", "Más", "More", KeyIds.C, KeyIds.Ctrl),
            ]
        );

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index.IsRepeated(Id("a")).ShouldBeTrue();
        index.IsRepeated(Id("c")).ShouldBeTrue();
        index.IsRepeated(Id("b")).ShouldBeFalse();
        index.AppearancesOf(Id("a")).Select(a => a.Shortcut.Id.Value).ShouldBe(["a", "c"]);
        index.AdviceFor(Id("a")).ShouldBe(DuplicateAdvice.DifferentNames);
    }

    [Fact]
    [Trait("Req", "REP-002")]
    public void Different_profiles_repeat_only_with_the_same_name_in_both_languages()
    {
        var library = BuildLibrary(
            [],
            [],
            Profile(
                "word",
                "Word",
                "winword.exe",
                Named("w", "Copiar", "Copy", KeyIds.Ctrl, KeyIds.C),
                Named("w2", "Negrita", "Bold", KeyIds.Ctrl, KeyIds.B)
            ),
            Profile(
                "excel",
                "Excel",
                "excel.exe",
                Named("x", " copiar", "COPY ", KeyIds.Ctrl, KeyIds.C),
                Named("x2", "Negrita", "Strong", KeyIds.Ctrl, KeyIds.B)
            )
        );

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index.IsRepeated(Id("w")).ShouldBeTrue();
        index.IsRepeated(Id("x")).ShouldBeTrue();
        index.AdviceFor(Id("x")).ShouldBe(DuplicateAdvice.SameName);
        index.IsRepeated(Id("w2")).ShouldBeFalse();
        index.IsRepeated(Id("x2")).ShouldBeFalse();
        index.RepeatedCombinationCount.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "REP-001")]
    public void Only_tap_hold_and_toggle_with_keys_have_a_key()
    {
        var library = BuildLibrary(
            [],
            [
                Shortcut("tap", "A", new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C), [])),
                Shortcut("hold", "A", new HoldAction(KeyChord.FromKeys(KeyIds.C, KeyIds.Ctrl))),
                Shortcut("toggle", "A", new ToggleAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C))),
                Shortcut("empty", "A", new TapAction(KeyChord.Empty, [])),
                Shortcut("empty2", "A", new TapAction(KeyChord.Empty, [])),
                Shortcut(
                    "macro",
                    "A",
                    new MacroAction([new KeysStep(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C))])
                ),
            ]
        );

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index
            .AppearancesOf(Id("tap"))
            .Select(a => a.Shortcut.Id.Value)
            .ShouldBe(["tap", "hold", "toggle"]);
        index.IsRepeated(Id("empty")).ShouldBeFalse();
        index.IsRepeated(Id("macro")).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REP-002")]
    [Trait("Req", "REP-005")]
    public void A_combination_marked_as_fine_is_never_flagged()
    {
        var library = BuildLibrary(
            [],
            [Tap("a", "A", KeyIds.Ctrl, KeyIds.C), Tap("b", "B", KeyIds.C, KeyIds.Ctrl)]
        );
        CanonicalChord
            .TryFrom(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C), out var key)
            .ShouldBeTrue();

        var index = DuplicateIndex.Build(library, new DuplicatePolicy([key]));

        index.IsRepeated(Id("a")).ShouldBeFalse();
        index.RepeatedCombinationCount.ShouldBe(0);
        index.FirstToReview.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "REP-003")]
    [Trait("Req", "REP-005")]
    public void Review_opens_an_appearance_outside_Always_visible_first()
    {
        var library = BuildLibrary(
            [Tap("g", "Guardar", KeyIds.Ctrl, KeyIds.S)],
            [Tap("x", "Guardar", KeyIds.Ctrl, KeyIds.S)],
            Profile("word", "Word", null, Tap("w", "Guardar", KeyIds.Ctrl, KeyIds.S))
        );

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index.FirstToReview.ShouldBe(Id("x"));
        index.NextAfterDeleting(Id("x")).ShouldBe(Id("w"));
        index.NextAfterDeleting(Id("w")).ShouldBe(Id("x"));
        index.AppearancesOf(Id("g")).Length.ShouldBe(3);
    }

    [Fact]
    [Trait("Req", "REP-005")]
    public void Names_match_in_some_language_for_leaving_one_in_Always_visible()
    {
        var spanish = LocalizedText.Same("Guardar", LangCode.Es);
        var english = new LocalizedText([new(LangCode.Es, "Subrayar"), new(LangCode.En, "Save")]);
        var both = new LocalizedText([new(LangCode.Es, "Guardar"), new(LangCode.En, "Save")]);

        ShortcutNames.ShareAnyLanguage(both, english).ShouldBeTrue();
        ShortcutNames.AreSame(both, english).ShouldBeFalse();
        ShortcutNames.AreSame(spanish, LocalizedText.Same(" guardar ", LangCode.Es)).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REP-002")]
    [Trait("Req", "REP-007")]
    public void The_index_agrees_with_the_pairwise_rule() =>
        DomainGen.Document.Sample(
            document =>
            {
                var index = DuplicateIndex.Build(document.Library, document.Duplicates);
                var all = document.Library.EnumerateShortcuts().ToArray();
                foreach (var located in all)
                {
                    var expected = all.Any(other =>
                        other != located && RepeatedPair(document.Duplicates, located, other)
                    );
                    index
                        .IsRepeated(located.Shortcut.Id)
                        .ShouldBe(expected, located.Shortcut.Id.Value);
                }
            },
            iter: 10_000
        );

    [Fact]
    [Trait("Req", "REP-007")]
    public void Thousands_of_shortcuts_are_indexed_without_comparing_all_against_all()
    {
        var shortcuts = Enumerable
            .Range(0, 3000)
            .Select(i =>
                Tap("s" + i, "Atajo " + (i % 50), KeyIds.Ctrl, KeyDefinitions.All[11 + (i % 26)].Id)
            )
            .ToArray();
        var library = BuildLibrary([], shortcuts);

        var index = DuplicateIndex.Build(library, DuplicatePolicy.Empty);

        index.RepeatedCombinationCount.ShouldBe(26);
        index.IsRepeated(new ShortcutId("s0")).ShouldBeTrue();
    }

    private static ShortcutId Id(string value) => new(value);

    private static bool RepeatedPair(
        DuplicatePolicy policy,
        LocatedShortcut left,
        LocatedShortcut right
    )
    {
        if (
            !DuplicateIndex.TryGetKey(left.Shortcut, out var a)
            || !DuplicateIndex.TryGetKey(right.Shortcut, out var b)
            || a != b
            || policy.Ignored.Contains(a)
        )
        {
            return false;
        }

        return left.Location.List is ListRef.AlwaysVisible
            || right.Location.List is ListRef.AlwaysVisible
            || left.Location.List.Equals(right.Location.List)
            || ShortcutNames.AreSame(left.Shortcut.Name, right.Shortcut.Name);
    }
}
