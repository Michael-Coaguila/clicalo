using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// Pages of the shortcut grid, without scrolling (CUA-004, CUA-005, CUA-006): columns × rows per page, the page clamped
/// when it stops existing, ◀ ▶ and a swipe that never leave the range, and page 1 again when the context changes.
/// </summary>
public static class Paging
{
    /// <summary>Pages of <paramref name="itemCount"/> items at <paramref name="perPage"/> per page; at least 1.</summary>
    /// <param name="itemCount">Items in the list.</param>
    /// <param name="perPage">Items per page.</param>
    public static int PageCount(int itemCount, int perPage) =>
        perPage <= 0 || itemCount <= 0 ? 1 : ((itemCount - 1) / perPage) + 1;

    /// <summary><paramref name="page"/> brought inside 0 … <paramref name="pageCount"/> − 1 (CUA-004).</summary>
    /// <param name="page">A page.</param>
    /// <param name="pageCount">Pages.</param>
    public static int Clamp(int page, int pageCount) =>
        Math.Clamp(page, 0, Math.Max(0, pageCount - 1));

    /// <summary>The page <paramref name="page"/> (clamped) of a list.</summary>
    /// <param name="itemCount">Items in the list.</param>
    /// <param name="perPage">Items per page.</param>
    /// <param name="page">The page asked for.</param>
    public static PageWindow Window(int itemCount, int perPage, int page)
    {
        var count = Math.Max(0, itemCount);
        var pages = PageCount(count, perPage);
        var current = Clamp(page, pages);
        var start = Math.Min(count, current * Math.Max(0, perPage));
        return new PageWindow(current, pages, start, Math.Min(Math.Max(0, perPage), count - start));
    }

    /// <summary>◀ (−1) or ▶ (+1): the neighbor page, never past the first or the last (CUA-004).</summary>
    /// <param name="window">The page in view.</param>
    /// <param name="delta">−1 or +1.</param>
    public static int Step(PageWindow window, int delta) =>
        Clamp(window.Page + delta, window.PageCount);

    /// <summary>
    /// A horizontal swipe (CUA-005): toward the left shows the next page and toward the right the previous one,
    /// without leaving the range.
    /// </summary>
    /// <param name="window">The page in view.</param>
    /// <param name="towardLeft">Whether the finger moved toward the left.</param>
    public static int AfterSwipe(PageWindow window, bool towardLeft) =>
        Step(window, towardLeft ? 1 : -1);

    /// <summary>
    /// The page to show after the context of the grid may have changed: page 1 when it did (CUA-006), the same page
    /// clamped otherwise (CUA-004).
    /// </summary>
    /// <param name="page">The page in view.</param>
    /// <param name="before">The context of that page, or <see langword="null"/> before the first one.</param>
    /// <param name="after">The context now.</param>
    /// <param name="pageCount">Pages now.</param>
    public static int Reconcile(int page, PageContext? before, PageContext after, int pageCount) =>
        before is { } previous && previous == after ? Clamp(page, pageCount) : 0;

    /// <summary>The page dots of <paramref name="window"/>: none when there is one page (CUA-004).</summary>
    /// <param name="window">The page in view.</param>
    public static ImmutableArray<PageDot> Dots(PageWindow window)
    {
        if (!window.IsPaged)
        {
            return [];
        }

        var layout = PanelSizes.Layout;
        var dots = ImmutableArray.CreateBuilder<PageDot>(window.PageCount);
        for (var page = 0; page < window.PageCount; page++)
        {
            var active = page == window.Page;
            dots.Add(
                new PageDot(
                    page,
                    active,
                    active ? layout.PanelPageDotActiveWidthPx : layout.PanelPageDotSizePx
                )
            );
        }

        return dots.MoveToImmutable();
    }

    /// <summary>The accessible name of a dot: «[pageN] n», with n from 1 (CUA-004).</summary>
    /// <param name="pageWord">The localized [pageN] («Página»).</param>
    /// <param name="page">The page, from 0.</param>
    public static string DotName(string pageWord, int page) =>
        string.Create(CultureInfo.InvariantCulture, $"{pageWord} {page + 1}");
}
