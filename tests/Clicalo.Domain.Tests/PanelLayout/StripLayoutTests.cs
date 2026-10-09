using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.PanelLayout;

/// <summary><see cref="StripLayout"/>: the Always visible row of FIJ-002 and FIJ-003.</summary>
public sealed class StripLayoutTests
{
    /// <summary>Size, Compact, expected capacity, tile height and whether names show.</summary>
    public static TheoryData<PanelSize, bool, int, int, bool> Rows =>
        new()
        {
            { PanelSize.Small, false, 4, 44, false },
            { PanelSize.Medium, false, 8, 60, true },
            { PanelSize.Large, false, 8, 72, true },
            { PanelSize.Medium, true, 4, 40, false },
            { PanelSize.Large, true, 4, 40, false },
        };

    /// <summary>Shortcuts, capacity, page asked, expected page, pages, start, count, chip.</summary>
    public static TheoryData<int, int, int, int, int, int, int, bool> Windows =>
        new()
        {
            { 0, 4, 0, 0, 1, 0, 0, false },
            { 4, 4, 0, 0, 1, 0, 4, false },
            { 4, 4, 3, 0, 1, 0, 4, false },
            { 5, 4, 0, 0, 2, 0, 3, true },
            { 5, 4, 1, 1, 2, 3, 2, true },
            { 5, 4, 2, 0, 2, 0, 3, true },
            { 8, 8, 0, 0, 1, 0, 8, false },
            { 9, 8, 1, 1, 2, 7, 2, true },
            { 15, 8, 2, 2, 3, 14, 1, true },
            { 15, 8, 3, 0, 3, 0, 7, true },
        };

    [Theory]
    [MemberData(nameof(Rows))]
    [Trait("Req", "FIJ-002")]
    [Trait("Req", "FIJ-003")]
    public void The_row_holds_four_in_S_or_Compact_and_eight_in_M_or_L(
        PanelSize size,
        bool compact,
        int capacity,
        int height,
        bool names
    )
    {
        var settings = PanelLayoutSettings.Default with { Size = size, Compact = compact };

        StripLayout.Capacity(settings).ShouldBe(capacity);
        StripLayout.TileHeight(settings).ShouldBe(height);
        StripLayout.ShowsNames(settings).ShouldBe(names);
    }

    [Theory]
    [MemberData(nameof(Windows))]
    [Trait("Req", "FIJ-003")]
    public void With_more_than_it_holds_it_shows_one_less_and_the_chip(
        int count,
        int capacity,
        int asked,
        int page,
        int pages,
        int start,
        int shown,
        bool chip
    )
    {
        var window = StripLayout.Window(count, capacity, asked);

        window.Page.ShouldBe(page);
        window.PageCount.ShouldBe(pages);
        window.Start.ShouldBe(start);
        window.Count.ShouldBe(shown);
        window.HasMore.ShouldBe(chip);
    }

    [Fact]
    [Trait("Req", "FIJ-003")]
    public void The_chip_says_its_page_and_goes_back_to_the_first_after_the_last()
    {
        var first = StripLayout.Window(5, 4, 0);
        var last = StripLayout.Window(5, 4, 1);

        first.MoreLabel.ShouldBe("1/2");
        first.NextPage.ShouldBe(1);
        last.MoreLabel.ShouldBe("2/2");
        last.NextPage.ShouldBe(0);
    }
}
