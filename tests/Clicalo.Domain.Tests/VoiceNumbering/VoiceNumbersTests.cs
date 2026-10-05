using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.VoiceNumbering;

namespace Clicalo.Domain.Tests.VoiceNumbering;

/// <summary><see cref="VoiceNumbers"/>: continuous across pages, and the Always visible row after the list (ACC-010).</summary>
public sealed class VoiceNumbersTests
{
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(0, 8, 9)]
    [InlineData(1, 0, 10)]
    [InlineData(2, 3, 22)]
    [Trait("Req", "ACC-010")]
    public void The_grid_numbers_continue_across_pages(int page, int indexOnPage, int expected) =>
        VoiceNumbers.ForListPage(Paging.Window(30, 9, page), indexOnPage).ShouldBe(expected);

    [Theory]
    [InlineData(12, 0, 13)]
    [InlineData(12, 4, 17)]
    [InlineData(0, 0, 1)]
    [Trait("Req", "ACC-010")]
    [Trait("Req", "FIJ-004")]
    public void The_Always_visible_row_continues_after_the_list(
        int listCount,
        int index,
        int expected
    ) => VoiceNumbers.ForStrip(listCount, index).ShouldBe(expected);

    [Fact]
    [Trait("Req", "ACC-009")]
    public void The_accessible_name_starts_with_the_number() =>
        VoiceNumbers.Prefix(4, "Negrita").ShouldBe("4 Negrita");
}
