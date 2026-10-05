using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Tests.Generators;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Tests.Frequents;

/// <summary>Usage recording (FRE-002) and the curation of Frequents (FRE-001, FRE-004, FRE-005).</summary>
public sealed class FrequentsStateTests
{
    private static readonly ShortcutId Copy = new("copy");
    private static readonly ShortcutId Paste = new("paste");
    private static readonly TimeSpan Window = Timings.Frequents.UsageWindow;

    [Fact]
    [Trait("Req", "FRE-002")]
    public void Recording_adds_a_mark_in_order_and_purges_the_expired_ones()
    {
        var now = DomainGen.Now;
        var history = DomainGen.Usage([(Copy, 40), (Copy, 2), (Paste, 31)]);

        var recorded = history
            .Record(Copy, now, Window)
            .Record(Copy, now - TimeSpan.FromDays(1), Window);

        recorded.Entries.Keys.ShouldBe([Copy]);
        recorded.Entries[Copy].ShouldBe([now.AddDays(-2), now.AddDays(-1), now]);
        recorded.CountRecent(Copy, now, Window).ShouldBe(3);
        recorded.LastUse(Copy).ShouldBe(now);
        recorded.LastUse(Paste).ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "FRE-005")]
    public void Recording_keeps_the_usage_of_deleted_shortcuts_and_purging_drops_it()
    {
        var now = DomainGen.Now;
        var history = DomainGen.Usage([(Copy, 1), (Paste, 1)]);

        history.Record(Copy, now, Window).Entries.ContainsKey(Paste).ShouldBeTrue();
        var purged = history.Purge(now, Window, id => id == Copy);
        purged.Entries.Keys.ShouldBe([Copy]);
        history.Purge(now, Window, _ => true).ShouldBeSameAs(history);
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Pinning_appends_once_and_unhides()
    {
        var state = FrequentsState.Empty.WithHidden(Copy).WithPin(Paste).WithPin(Copy);

        state.Pins.ShouldBe([Paste, Copy]);
        state.Hidden.ShouldBeEmpty();
        state.WithPin(Copy).ShouldBeSameAs(state);
        state.WithoutPin(new ShortcutId("nope")).ShouldBeSameAs(state);
        state.IsWellFormed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    public void Hiding_unpins_and_keeps_the_hidden_sorted_without_repetitions()
    {
        var state = FrequentsState
            .Empty.WithPin(Paste)
            .WithHidden(Paste)
            .WithHidden(Copy)
            .WithHidden(Copy);

        state.Pins.ShouldBeEmpty();
        state.Hidden.ShouldBe([Copy, Paste]);
        state.IsWellFormed.ShouldBeTrue();
        (state with { Hidden = [Paste, Copy] }).IsWellFormed.ShouldBeFalse();
        (state with { Pins = [Copy, Copy] }).IsWellFormed.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    public void Reset_empties_usage_pins_and_hidden_and_raises_the_epoch()
    {
        var state = new FrequentsState([Copy], [Paste], 3, DomainGen.Usage([(Copy, 1)]));

        var reset = state.Reset();

        reset.ShouldBe(FrequentsState.Empty with { UsageEpoch = 4, Usage = reset.Usage });
        reset.Usage.Entries.ShouldBeEmpty();
    }
}
