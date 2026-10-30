using Clicalo.Domain.Frequents;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.Presentation.Panel.TestMode;

namespace Clicalo.Presentation.Panel.EditMode;

/// <summary>
/// What a gesture on a tile does before it reaches the engine (EJE-001 steps 2 and 3, CUA-012, CUA-014, TAC-008): the
/// surface asks it first and lets the tile run only when nothing here took the gesture. In edit mode a tap opens the
/// editor; in test mode a tap or the start of a hold is marked ✓ and an ignored touch ⊘; a long press, a right click or
/// the secondary action opens the context menu. One place for the grid, the Always visible row, the search results and
/// the Tab, so every surface behaves the same.
/// </summary>
/// <remarks>
/// The touch filter already ran in the gesture recognizer; the order edit mode → test mode is the activation policy's,
/// and the engine is in test mode too, so nothing would be sent even if a gesture went through (INV-7).
/// </remarks>
public sealed class TileInteractionModes
{
    private readonly EditModeViewModel _edit;
    private readonly TestModeViewModel _test;
    private readonly TileContextMenuViewModel _menu;

    /// <summary>Joins the three modes of the tiles.</summary>
    /// <param name="edit">Edit mode.</param>
    /// <param name="test">Test mode.</param>
    /// <param name="menu">The context menu.</param>
    public TileInteractionModes(
        EditModeViewModel edit,
        TestModeViewModel test,
        TileContextMenuViewModel menu
    )
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(menu);
        _edit = edit;
        _test = test;
        _menu = menu;
    }

    /// <summary>
    /// The touch target kind of a tile now: in edit mode every tile is a plain tap (no Hold and no long press,
    /// CUA-012); otherwise a Hold holds (EJE-004) and any other tile also opens its menu with a long press (CUA-014).
    /// </summary>
    /// <param name="tile">The tile.</param>
    public TouchTargetKind KindOf(TileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var hold = tile.Behavior == TileBehavior.Hold;
        return TileMenu.OpensOnLongPress(hold, _edit.IsOn) ? TouchTargetKind.TapOrLongPress
            : hold && !_edit.IsOn ? TouchTargetKind.Hold
            : TouchTargetKind.Tap;
    }

    /// <summary>An accepted tap lifted on the tile, or its UI Automation Invoke.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>Whether a mode took it: the tile must not run.</returns>
    public bool Tapped(TileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _menu.Close();
        return _edit.OnTap(tile.Id) || _test.OnCounted(tile.Id);
    }

    /// <summary>A Hold tile starts holding: in test mode it is marked ✓ and holds nothing.</summary>
    /// <param name="tile">The tile.</param>
    /// <returns>Whether a mode took it: the tile must not hold.</returns>
    public bool HoldStarted(TileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _menu.Close();
        return _edit.OnTap(tile.Id) || _test.OnCounted(tile.Id);
    }

    /// <summary>
    /// Shows the discreet answer to an ignored touch on a tile in normal use (TAC-003); the composition times it and
    /// leaves it out when the person turned the flash off. Unset, an ignored touch shows nothing.
    /// </summary>
    public Action<TileViewModel>? IgnoredFeedback { get; set; }

    /// <summary>
    /// The recognizer ignored a touch on the tile (TAC-002): test mode marks it ⊘ (not in edit mode); in normal use the
    /// tile answers with a slight outline (TAC-003), so a person with tremor knows nothing was sent. A resting palm
    /// gets no answer: it is not an attempt to press.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="reason">Why it was ignored.</param>
    public void Ignored(TileViewModel tile, IgnoreReason reason)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (_edit.IsOn)
        {
            return;
        }

        if (_test.IsOn)
        {
            _test.OnIgnored(tile.Id, reason);
        }
        else if (reason != IgnoreReason.Palm)
        {
            IgnoredFeedback?.Invoke(tile);
        }
    }

    /// <summary>
    /// A long press of 600 ms on the tile (CUA-014). In test mode it is marked ✓ like a tap and opens nothing
    /// (decision delegated by the user, 2026-10-09); otherwise it opens the menu (<see cref="OpenMenu"/>).
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="inFrequents">Whether the tile is in the Frequents view.</param>
    /// <returns>Whether the menu opened.</returns>
    public bool LongPressed(TileViewModel tile, bool inFrequents)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (!_edit.IsOn && _test.OnCounted(tile.Id))
        {
            return false;
        }

        return OpenMenu(tile, inFrequents);
    }

    /// <summary>
    /// A long press of 600 ms (the recognizer only reports it on a <see cref="TouchTargetKind.TapOrLongPress"/> tile), a
    /// right click, the Menu key, Shift+F10 or the accessible secondary action (CUA-014, CUA-015: a Hold tile too). Not
    /// in edit mode, where a tap edits.
    /// </summary>
    /// <param name="tile">The tile.</param>
    /// <param name="inFrequents">Whether the tile is in the Frequents view.</param>
    /// <returns>Whether the menu opened.</returns>
    public bool OpenMenu(TileViewModel tile, bool inFrequents)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (_edit.IsOn)
        {
            return false;
        }

        _menu.Open(tile.Id, tile.AccessibleName, tile.Icon, inFrequents);
        return true;
    }
}
