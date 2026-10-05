using System.Runtime.InteropServices;

namespace Clicalo.Domain.PanelLayout;

/// <summary>The page of a paged list in view (CUA-004).</summary>
/// <param name="Page">The page, from 0.</param>
/// <param name="PageCount">Pages, at least 1.</param>
/// <param name="Start">Index in the list of the first item of the page.</param>
/// <param name="Count">Items on the page.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PageWindow(int Page, int PageCount, int Start, int Count)
{
    /// <summary>Whether there is more than one page: ◀, the dots and ▶ show (CUA-004).</summary>
    public bool IsPaged => PageCount > 1;

    /// <summary>Whether a previous page exists; ◀ dims on the first one.</summary>
    public bool HasPrevious => Page > 0;

    /// <summary>Whether a next page exists; ▶ dims on the last one.</summary>
    public bool HasNext => Page < PageCount - 1;
}
