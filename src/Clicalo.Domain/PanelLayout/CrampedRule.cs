namespace Clicalo.Domain.PanelLayout;

/// <summary>
/// Lack of space with an alert (CUA-003): while the panic strip or the administrator notice shows and one whole row
/// of tiles does not fit (<c>measured space + 2 &lt; tile height + 6</c>), the Always visible row and the profile
/// selector hide until the alert goes away. Decided by <b>measuring</b>: the surface passes the height it measured
/// for the grid.
/// </summary>
public static class CrampedRule
{
    /// <summary>Slack added to the measured space (CUA-003).</summary>
    public const int SpaceSlackPx = 2;

    /// <summary>Margin added to the tile height (CUA-003).</summary>
    public const int TileMarginPx = 6;

    /// <summary>
    /// Whether the rows hide: never without an alert; once hidden, they stay hidden while the alert lasts (showing
    /// them again would make the space short again); otherwise, only when a whole row does not fit.
    /// </summary>
    /// <param name="wasCramped">Whether they were hidden.</param>
    /// <param name="alertVisible">Whether the panic strip or the administrator notice shows.</param>
    /// <param name="gridSpacePx">The measured height available to the grid, or <see langword="null"/> before any measure.</param>
    /// <param name="tileHeightPx">Height of a tile.</param>
    public static bool Evaluate(
        bool wasCramped,
        bool alertVisible,
        double? gridSpacePx,
        int tileHeightPx
    )
    {
        if (!alertVisible)
        {
            return false;
        }

        if (wasCramped)
        {
            return true;
        }

        return gridSpacePx is { } space && space + SpaceSlackPx < tileHeightPx + TileMarginPx;
    }
}
