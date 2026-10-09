namespace Clicalo.Domain.Frequents;

/// <summary>
/// The parts of edit mode that depend on the view in front (CUA-012, CUA-013, FRE-003): the × of Frequents curates
/// Frequents instead of deleting, so the rules live with Frequents.
/// </summary>
public static class EditModeRules
{
    /// <summary>
    /// What the × of a grid tile does: <see cref="TileRemoval.HideFromFrequents"/> in Frequents (the shortcut stays in
    /// its profile, CUA-013), <see cref="TileRemoval.None"/> in the search, <see cref="TileRemoval.Delete"/> elsewhere.
    /// </summary>
    /// <param name="frequents">Whether Frequents is in view.</param>
    /// <param name="searching">Whether the search shows results in place of the list.</param>
    public static TileRemoval RemovalIn(bool frequents, bool searching) =>
        searching ? TileRemoval.None
        : frequents ? TileRemoval.HideFromFrequents
        : TileRemoval.Delete;

    /// <summary>
    /// Whether the dashed «+ [add]» tile closes the grid: not in Frequents (FRE-003) and not in the search (CUA-012).
    /// </summary>
    /// <param name="frequents">Whether Frequents is in view.</param>
    /// <param name="searching">Whether the search shows results in place of the list.</param>
    public static bool OffersAdd(bool frequents, bool searching) => !frequents && !searching;
}
