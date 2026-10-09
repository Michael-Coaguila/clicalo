using System.Globalization;
using Clicalo.Domain.PanelLayout;

namespace Clicalo.Domain.VoiceNumbering;

/// <summary>
/// Voice numbers (ACC-009, ACC-010; blueprint §8.6 <c>VoiceNumbering.Assign</c>): the grid numbers its list
/// <b>continuously across pages</b> (page 2 starts at per-page + 1), and the Always visible row continues after the
/// total of the list (N + 1 … N + k), so its numbers stay stable while either pages. With the option on, the accessible
/// name becomes «{n} {name}».
/// </summary>
public static class VoiceNumbers
{
    /// <summary>The number of the item at <paramref name="index"/> of the list in view (from 0, across pages).</summary>
    /// <param name="index">Index in the whole list, not in the page.</param>
    public static int ForList(int index) => index + 1;

    /// <summary>The number of the tile at <paramref name="indexOnPage"/> of <paramref name="window"/>.</summary>
    /// <param name="window">The page in view.</param>
    /// <param name="indexOnPage">Index on the page, from 0.</param>
    public static int ForListPage(PageWindow window, int indexOnPage) =>
        ForList(window.Start + indexOnPage);

    /// <summary>
    /// The number of the tile at <paramref name="index"/> of the Always visible row (from 0, across its pages): after
    /// the <paramref name="listCount"/> numbers of the list.
    /// </summary>
    /// <param name="listCount">Shortcuts of the list in view.</param>
    /// <param name="index">Index in the whole row.</param>
    public static int ForStrip(int listCount, int index) => Math.Max(0, listCount) + index + 1;

    /// <summary>The accessible name with its number: «{n} {name}» (ACC-009).</summary>
    /// <param name="number">The voice number.</param>
    /// <param name="name">The name.</param>
    public static string Prefix(int number, string name) =>
        string.Create(CultureInfo.InvariantCulture, $"{number} {name}");
}
