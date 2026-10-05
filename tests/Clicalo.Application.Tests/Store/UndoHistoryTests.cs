using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Timing;
using static Clicalo.Application.Tests.Store.StoreSamples;

namespace Clicalo.Application.Tests.Store;

/// <summary>The bounded undo stack (DAT-006) and its grouping by key until sealed (EDI-021).</summary>
public sealed class UndoHistoryTests
{
    private static readonly UserDocument Before = Document();

    [Fact]
    [Trait("Req", "EDI-021")]
    public void An_entry_with_the_key_of_the_open_top_joins_it_and_keeps_its_before()
    {
        var history = new UndoHistory();
        history.Record(Entry("bold", DocumentSlices.Library, L.NewCreated.Key));
        var later = Before with { Revision = 7 };

        history.WouldJoin("bold").ShouldBeTrue();
        history.Record(new UndoEntry(later, DocumentSlices.Settings, L.Saved.Key, "bold", false));

        history.Count.ShouldBe(1);
        history.Top!.Before.Revision.ShouldBe(0);
        history.Top.Before.ShouldNotBeSameAs(later);
        history.Top.Slices.ShouldBe(DocumentSlices.Library | DocumentSlices.Settings);
        history.Top.Label.ShouldBe(L.NewCreated.Key);
    }

    [Fact]
    [Trait("Req", "EDI-021")]
    public void A_sealed_top_or_another_key_starts_a_new_entry()
    {
        var history = new UndoHistory();
        history.Record(Entry("bold", DocumentSlices.Library));
        history.Record(Entry("save", DocumentSlices.Library));
        history.Seal();
        history.Record(Entry("save", DocumentSlices.Library));
        history.Record(Entry(null, DocumentSlices.Library));
        history.Record(Entry(null, DocumentSlices.Library));

        history.Count.ShouldBe(5);
        history.WouldJoin(null).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void The_oldest_entries_are_dropped_past_the_depth()
    {
        var history = new UndoHistory();
        for (var i = 0; i < 30; i++)
        {
            history.Record(Entry(null, DocumentSlices.Library, revision: i));
        }

        history.Count.ShouldBe(Timings.Persistence.UndoDepth);
        var revisions = new List<long>();
        while (history.TryPop(out var entry))
        {
            revisions.Add(entry.Before.Revision);
        }

        revisions.ShouldBe(Enumerable.Range(10, 20).Reverse().Select(i => (long)i));
        history.Top.ShouldBeNull();
    }

    [Fact]
    public void Clearing_empties_the_stack()
    {
        var history = new UndoHistory();
        history.Record(Entry("a", DocumentSlices.Library));

        history.Clear();

        history.Count.ShouldBe(0);
        history.TryPop(out _).ShouldBeFalse();
    }

    private static UndoEntry Entry(
        string? key,
        DocumentSlices slices,
        MessageKey? label = null,
        long revision = 0
    ) => new(Before with { Revision = revision }, slices, label ?? L.Saved.Key, key, Sealed: false);
}
