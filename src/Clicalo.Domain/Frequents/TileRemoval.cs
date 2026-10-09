namespace Clicalo.Domain.Frequents;

/// <summary>What the red × of a tile does in edit mode (CUA-012, CUA-013).</summary>
public enum TileRemoval
{
    /// <summary>No ×: the search results.</summary>
    None,

    /// <summary>Deletes the shortcut from its list, with two taps and undo (REG-04).</summary>
    Delete,

    /// <summary>In Frequents: removes the tile from Frequents and leaves the shortcut in its profile (CUA-013).</summary>
    HideFromFrequents,
}
