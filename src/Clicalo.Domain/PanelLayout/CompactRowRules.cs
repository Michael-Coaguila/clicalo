namespace Clicalo.Domain.PanelLayout;

/// <summary>The bottom row of the Compact view (VCO-002): ★ · ◀ · page dots · ▶ · profile button. Pure.</summary>
public static class CompactRowRules
{
    /// <summary>Whether the row shows: in the Compact view, hidden while the search has text (VCO-002).</summary>
    /// <param name="settings">The layout settings.</param>
    /// <param name="searchingWithText">The search is open with text.</param>
    public static bool Visible(PanelLayoutSettings settings, bool searchingWithText)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Compact && !searchingWithText;
    }

    /// <summary>Whether ◀, the dots and ▶ show in the row: with more than one page (VCO-002).</summary>
    /// <param name="pageCount">Pages of the grid.</param>
    /// <param name="emptyProfile">The empty profile card is on show instead of the grid.</param>
    public static bool ShowsPager(int pageCount, bool emptyProfile) =>
        !emptyProfile && pageCount > 1;
}
