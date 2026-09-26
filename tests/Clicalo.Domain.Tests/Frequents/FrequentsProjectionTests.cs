using System.Collections.Immutable;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Generators;
using Clicalo.Domain.Timing;
using CsCheck;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Frequents;

/// <summary>The composition of Frequents (FRE-001) and the usage it counts (FRE-002).</summary>
public sealed class FrequentsProjectionTests
{
    private static readonly DateTimeOffset Now = DomainGen.Now;

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Pins_come_first_in_pin_order_then_the_most_used()
    {
        var library = Library12();
        var state = new FrequentsState(
            [Id("s5"), Id("gone"), Id("s2")],
            [],
            0,
            DomainGen.Usage([
                (Id("s1"), 1),
                (Id("s3"), 1),
                (Id("s3"), 2),
                (Id("s4"), 3),
                (Id("s4"), 4),
                (Id("s4"), 5),
            ])
        );

        var entries = FrequentsProjection.Compose(library, state, Now, alwaysVisibleRowShown: true);

        entries.Select(e => e.Shortcut.Id.Value).ShouldBe(["s5", "s2", "s4", "s3", "s1"]);
        entries.Select(e => e.Pinned).ShouldBe([true, true, false, false, false]);
        entries.Select(e => e.Uses).ShouldBe([0, 0, 3, 2, 1]);
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Ties_go_to_the_most_recent_use_then_to_the_profile_order()
    {
        var library = Library12();
        var state = new FrequentsState(
            [],
            [],
            0,
            DomainGen.Usage([(Id("s6"), 5), (Id("s2"), 1), (Id("s1"), 5), (Id("s9"), 5)])
        );

        var entries = FrequentsProjection.Compose(library, state, Now, alwaysVisibleRowShown: true);

        entries.Select(e => e.Shortcut.Id.Value).ShouldBe(["s2", "s1", "s6", "s9"]);
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Only_the_last_30_days_count_and_hidden_shortcuts_are_left_out()
    {
        var library = Library12();
        var window = Timings.Frequents.UsageWindow;
        var state = new FrequentsState(
            [],
            [Id("s3")],
            0,
            new UsageHistory(
                new Dictionary<ShortcutId, ValueList<DateTimeOffset>>
                {
                    [Id("s1")] = [Now - window],
                    [Id("s2")] = [Now - window + TimeSpan.FromTicks(1)],
                    [Id("s3")] = [Now],
                }.ToImmutableDictionary()
            )
        );

        var entries = FrequentsProjection.Compose(library, state, Now, alwaysVisibleRowShown: true);

        entries.Select(e => e.Shortcut.Id.Value).ShouldBe(["s2"]);
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void At_most_nine_are_shown_even_with_more_pins()
    {
        var library = Library12();
        var pins = Enumerable.Range(1, 12).Select(i => Id("s" + i)).ToArray();
        var state = new FrequentsState([.. pins], [], 0, UsageHistory.Empty);

        var entries = FrequentsProjection.Compose(library, state, Now, alwaysVisibleRowShown: true);

        Timings.Frequents.MaxShown.ShouldBe(9);
        entries.Select(e => e.Shortcut.Id.Value).ShouldBe(pins.Take(9).Select(p => p.Value));
        FrequentsProjection.IsPinLimitReached(library, state).ShouldBeTrue();
        FrequentsProjection
            .IsPinLimitReached(library, state with { Pins = [.. pins.Take(8)] })
            .ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Always_visible_shortcuts_are_left_out_while_their_row_is_shown()
    {
        var library = BuildLibrary(
            [Tap("a1", "Copiar", KeyIds.Ctrl, KeyIds.C)],
            [Tap("g1", "Deshacer", KeyIds.Ctrl, KeyIds.Z)]
        );
        var state = new FrequentsState(
            [Id("a1")],
            [],
            0,
            DomainGen.Usage([(Id("a1"), 1), (Id("g1"), 1)])
        );

        FrequentsProjection
            .Compose(library, state, Now, alwaysVisibleRowShown: true)
            .Select(e => e.Shortcut.Id.Value)
            .ShouldBe(["g1"]);
        FrequentsProjection
            .Compose(library, state, Now, alwaysVisibleRowShown: false)
            .Select(e => e.Shortcut.Id.Value)
            .ShouldBe(["a1", "g1"]);
    }

    [Fact]
    [Trait("Req", "FRE-003")]
    public void Each_tile_knows_where_its_shortcut_lives()
    {
        var library = Sample();
        var state = new FrequentsState([], [], 0, DomainGen.Usage([(Id("bold"), 1)]));

        var entry = FrequentsProjection
            .Compose(library, state, Now, alwaysVisibleRowShown: true)
            .Single();

        entry.Location.List.ShouldBe(new ListRef.InProfile(new ProfileId("word")));
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void The_view_never_holds_a_shortcut_twice_nor_more_than_nine() =>
        DomainGen.Document.Sample(
            document =>
            {
                var entries = FrequentsProjection.Compose(
                    document.Library,
                    document.Frequents,
                    Now,
                    alwaysVisibleRowShown: true
                );
                entries.Length.ShouldBeLessThanOrEqualTo(Timings.Frequents.MaxShown);
                entries.Select(e => e.Shortcut.Id).ShouldBeUnique();
                entries.ShouldAllBe(e =>
                    e.Pinned || !document.Frequents.Hidden.Contains(e.Shortcut.Id)
                );
                entries.ShouldAllBe(e => e.Pinned || e.Uses > 0);
                entries.ShouldAllBe(e => e.Location.List is ListRef.InProfile);
            },
            iter: 10_000
        );

    private static ShortcutId Id(string value) => new(value);

    /// <summary>General holds s1…s4; Word holds s5…s8; Chrome holds s9…s12.</summary>
    private static ShortcutLibrary Library12() =>
        BuildLibrary(
            [],
            [.. Range(1, 4)],
            Profile("word", "Word", "winword.exe", [.. Range(5, 8)]),
            Profile("chrome", "Chrome", "chrome.exe", [.. Range(9, 12)])
        );

    private static IEnumerable<Shortcut> Range(int from, int to) =>
        Enumerable.Range(from, to - from + 1).Select(i => Tap("s" + i, "Atajo " + i, KeyIds.F1));
}
