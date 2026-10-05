using System.Globalization;
using System.Runtime.InteropServices;

namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// The page of the Always visible row in view (FIJ-003): up to its capacity, or capacity − 1 tiles and the «··· i/N»
/// chip when there are more.
/// </summary>
/// <param name="Page">The page, from 0.</param>
/// <param name="PageCount">Pages, at least 1.</param>
/// <param name="Start">Index in the row of the first tile shown.</param>
/// <param name="Count">Tiles shown.</param>
/// <param name="HasMore">Whether the «··· i/N» chip shows.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StripWindow(
    int Page,
    int PageCount,
    int Start,
    int Count,
    bool HasMore
)
{
    /// <summary>The «i/N» of the chip, with i from 1.</summary>
    public string MoreLabel =>
        string.Create(CultureInfo.InvariantCulture, $"{Page + 1}/{PageCount}");

    /// <summary>The page the chip goes to: the next one, and the first after the last (FIJ-003).</summary>
    public int NextPage => PageCount <= 1 ? 0 : (Page + 1) % PageCount;
}
