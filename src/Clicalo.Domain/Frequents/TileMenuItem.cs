namespace Clicalo.Domain.Frequents;

/// <summary>A row of the context menu of a tile (CUA-014), in the order they are shown.</summary>
public enum TileMenuItem
{
    /// <summary>[ctxPin] «Fijar en Frecuentes»; it also stops hiding the shortcut.</summary>
    Pin,

    /// <summary>[ctxUnpin] «Dejar de fijar».</summary>
    Unpin,

    /// <summary>[ctxHide] «Quitar de Frecuentes»: only in Frequents and only for a shortcut that is not pinned.</summary>
    Hide,

    /// <summary>[edit]: the control center opens the shortcut in the editor.</summary>
    Edit,

    /// <summary>[cancel]: closes the menu.</summary>
    Cancel,
}
