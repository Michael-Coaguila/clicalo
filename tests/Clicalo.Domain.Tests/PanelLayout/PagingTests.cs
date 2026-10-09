using Clicalo.Domain.PanelLayout;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary><see cref="Paging"/>: pages without scrolling, ◀ ▶, swipe, dots and the return to page 1.</summary>
public sealed class PagingTests
{
    private static readonly PageContext Word = new("p-word", 3, 3, false);

    /// <summary>Items, per page, expected pages.</summary>
    public static TheoryData<int, int, int> Counts =>
        new()
        {
            { 0, 9, 1 },
            { 1, 9, 1 },
            { 9, 9, 1 },
            { 10, 9, 2 },
            { 18, 9, 2 },
            { 19, 9, 3 },
            { 5, 0, 1 },
        };

    /// <summary>Items, per page, page asked, expected page, start and count.</summary>
    public static TheoryData<int, int, int, int, int, int> Windows =>
        new()
        {
            { 10, 9, 0, 0, 0, 9 },
            { 10, 9, 1, 1, 9, 1 },
            { 10, 9, 5, 1, 9, 1 },
            { 10, 9, -3, 0, 0, 9 },
            { 0, 9, 2, 0, 0, 0 },
            { 20, 6, 3, 3, 18, 2 },
        };

    /// <summary>The context after a change, and whether the page survives it.</summary>
    public static TheoryData<PageContext, bool> Changes =>
        new()
        {
            { Word, true },
            { Word with { View = "p-excel" }, false },
            { Word with { View = "freq" }, false },
            { Word with { Columns = 4 }, false },
            { Word with { Rows = 2 }, false },
            { Word with { Compact = true }, false },
        };

    [Theory]
    [MemberData(nameof(Counts))]
    [Trait("Req", "CUA-004")]
    public void Pages_hold_columns_times_rows(int items, int perPage, int expected) =>
        Paging.PageCount(items, perPage).ShouldBe(expected);

    [Theory]
    [MemberData(nameof(Windows))]
    [Trait("Req", "CUA-004")]
    public void The_page_is_clamped_when_it_stops_existing(
        int items,
        int perPage,
        int asked,
        int page,
        int start,
        int count
    )
    {
        var window = Paging.Window(items, perPage, asked);

        window.Page.ShouldBe(page);
        window.Start.ShouldBe(start);
        window.Count.ShouldBe(count);
    }

    [Fact]
    [Trait("Req", "CUA-004")]
    public void The_arrows_never_leave_the_range()
    {
        var first = Paging.Window(20, 9, 0);
        var last = Paging.Window(20, 9, 2);

        first.HasPrevious.ShouldBeFalse();
        first.HasNext.ShouldBeTrue();
        Paging.Step(first, -1).ShouldBe(0);
        Paging.Step(first, 1).ShouldBe(1);
        last.HasNext.ShouldBeFalse();
        Paging.Step(last, 1).ShouldBe(2);
        Paging.Step(last, -1).ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "CUA-005")]
    public void A_swipe_to_the_left_shows_the_next_page_and_to_the_right_the_previous_one()
    {
        var middle = Paging.Window(30, 9, 1);

        Paging.AfterSwipe(middle, towardLeft: true).ShouldBe(2);
        Paging.AfterSwipe(middle, towardLeft: false).ShouldBe(0);
        Paging.AfterSwipe(Paging.Window(30, 9, 3), towardLeft: true).ShouldBe(3);
        Paging.AfterSwipe(Paging.Window(30, 9, 0), towardLeft: false).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Changes))]
    [Trait("Req", "CUA-006")]
    public void The_grid_goes_back_to_page_one_when_its_context_changes(
        PageContext after,
        bool keeps
    ) => Paging.Reconcile(2, Word, after, 4).ShouldBe(keeps ? 2 : 0);

    [Fact]
    [Trait("Req", "CUA-004")]
    [Trait("Req", "CUA-006")]
    public void The_first_projection_starts_on_page_one_and_a_kept_page_is_clamped()
    {
        Paging.Reconcile(2, null, Word, 4).ShouldBe(0);
        Paging.Reconcile(5, Word, Word, 3).ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "CUA-004")]
    public void The_active_dot_is_24_wide_and_the_others_10()
    {
        Paging.Dots(Paging.Window(9, 9, 0)).ShouldBeEmpty();

        var dots = Paging.Dots(Paging.Window(25, 9, 1));

        dots.Select(static dot => (dot.Page, dot.IsActive, dot.WidthPx))
            .ShouldBe([(0, false, 10), (1, true, 24), (2, false, 10)]);
    }

    [Fact]
    [Trait("Req", "CUA-004")]
    public void Each_dot_is_named_after_its_page_from_one() =>
        Paging.DotName("Página", 0).ShouldBe("Página 1");
}
