using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Frequents;

/// <summary>
/// The context menu of a tile (CUA-014, CUA-015, docs/04 «Anatomía de un botón»): when a long press opens it and which
/// rows it has. Its rows curate Frequents (pins and hidden, FRE-001), so the rules live with them.
/// </summary>
public static class TileMenu
{
    /// <summary>
    /// Whether a long press of <c>Timings.Touch.LongPress</c> (600 ms) on the tile opens the menu: never on a Hold tile,
    /// whose finger holds the keys (EJE-004; its menu opens with the right click or the secondary action, CUA-015), and
    /// never in edit mode, where a tap opens the editor (CUA-012).
    /// </summary>
    /// <param name="holdTile">Whether the tile is a Hold.</param>
    /// <param name="editMode">Whether the panel is in edit mode.</param>
    public static bool OpensOnLongPress(bool holdTile, bool editMode) => !holdTile && !editMode;

    /// <summary>
    /// The rows of the menu, in order: [ctxPin] or [ctxUnpin]; [ctxHide] only in Frequents and when not pinned; [edit];
    /// [cancel].
    /// </summary>
    /// <param name="state">Pins and hidden.</param>
    /// <param name="shortcut">The shortcut of the tile.</param>
    /// <param name="inFrequents">Whether the tile is in the Frequents view.</param>
    public static ImmutableArray<TileMenuItem> Items(
        FrequentsState state,
        ShortcutId shortcut,
        bool inFrequents
    )
    {
        ArgumentNullException.ThrowIfNull(state);
        var pinned = state.Pins.Contains(shortcut);
        var items = ImmutableArray.CreateBuilder<TileMenuItem>(4);
        items.Add(pinned ? TileMenuItem.Unpin : TileMenuItem.Pin);
        if (inFrequents && !pinned)
        {
            items.Add(TileMenuItem.Hide);
        }

        items.Add(TileMenuItem.Edit);
        items.Add(TileMenuItem.Cancel);
        return items.ToImmutable();
    }
}
