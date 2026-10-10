using Clicalo.Domain.Frequents;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.Presentation.Panel.TestMode;

namespace Clicalo.Presentation.Dock;

/// <summary>
/// What a gesture on a shortcut of the bar or of «Pinned» does before it reaches the engine (PES-010, PES-014, CUA-014,
/// TAC-008): the same rules as the tiles of the panel, for the Tab view. In test mode a tap or the start of a hold is
/// marked ✓ and an ignored touch ⊘, and nothing runs; in normal use an ignored touch gets a slight outline (TAC-003); a
/// long press, a right click or the accessible secondary action
/// opens the menu of the shortcut, which shows beside the bar; while that menu is open, a tap on a shortcut only
/// closes it. The Tab view has no edit mode.
/// </summary>
public sealed class DockTileModes
{
    private readonly TileContextMenuViewModel _menu;
    private readonly Func<bool> _inFrequents;

    /// <summary>Joins test mode and the menu for the Tab view.</summary>
    /// <param name="test">Test mode.</param>
    /// <param name="menu">The menu of a shortcut.</param>
    /// <param name="inFrequents">Whether the Frequents view is in front.</param>
    public DockTileModes(
        TestModeViewModel test,
        TileContextMenuViewModel menu,
        Func<bool> inFrequents
    )
    {
        ArgumentNullException.ThrowIfNull(test);
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(inFrequents);
        TestMode = test;
        _menu = menu;
        _inFrequents = inFrequents;
    }

    /// <summary>Test mode: the shortcuts of the bar show its ✓ and ⊘ too (PES-014).</summary>
    public TestModeViewModel TestMode { get; }

    /// <summary>
    /// The touch target kind of a shortcut: a Mantener holds (EJE-004); any other also opens its menu with a long press
    /// (CUA-014).
    /// </summary>
    /// <param name="tile">The shortcut.</param>
    public static TouchTargetKind KindOf(DockTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        var hold = tile.Behavior == TileBehavior.Hold;
        return TileMenu.OpensOnLongPress(hold, editMode: false) ? TouchTargetKind.TapOrLongPress
            : hold ? TouchTargetKind.Hold
            : TouchTargetKind.Tap;
    }

    /// <summary>An accepted tap lifted on the shortcut, or its UI Automation Invoke.</summary>
    /// <param name="tile">The shortcut.</param>
    /// <returns>Whether a mode took it: the shortcut must not run.</returns>
    public bool Tapped(DockTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (_menu.IsOpen)
        {
            // CUA-014: with the menu open, a tap outside it only closes it.
            _menu.Close();
            return true;
        }

        return TestMode.OnCounted(tile.Id);
    }

    /// <summary>A Mantener starts holding: in test mode it is marked ✓ and holds nothing.</summary>
    /// <param name="tile">The shortcut.</param>
    /// <returns>Whether a mode took it: the shortcut must not hold.</returns>
    public bool HoldStarted(DockTileViewModel tile) => Tapped(tile);

    /// <summary>
    /// Shows the discreet answer to an ignored touch on a shortcut in normal use (TAC-003); the composition times it
    /// and leaves it out when the person turned the flash off. Unset, an ignored touch shows nothing.
    /// </summary>
    public Action<DockTileViewModel>? IgnoredFeedback { get; set; }

    /// <summary>
    /// The recognizer ignored a touch on the shortcut (TAC-002): test mode marks it ⊘; in normal use the shortcut
    /// answers with a slight outline (TAC-003), as a tile of the panel does. A resting palm gets no answer.
    /// </summary>
    /// <param name="tile">The shortcut.</param>
    /// <param name="reason">Why it was ignored.</param>
    public void Ignored(DockTileViewModel tile, IgnoreReason reason)
    {
        ArgumentNullException.ThrowIfNull(tile);
        if (TestMode.IsOn)
        {
            TestMode.OnIgnored(tile.Id, reason);
        }
        else if (reason != IgnoreReason.Palm)
        {
            IgnoredFeedback?.Invoke(tile);
        }
    }

    /// <summary>
    /// A long press of 600 ms on the shortcut (CUA-014, PES-010). In test mode it is marked ✓ like a tap and opens
    /// nothing; otherwise it opens the menu.
    /// </summary>
    /// <param name="tile">The shortcut.</param>
    /// <returns>Whether the menu opened.</returns>
    public bool LongPressed(DockTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        return !TestMode.OnCounted(tile.Id) && OpenMenu(tile);
    }

    /// <summary>
    /// A right click, the Menu key, Shift+F10 or the accessible secondary action (CUA-014, CUA-015: a Mantener too): the
    /// menu of the shortcut opens beside the bar.
    /// </summary>
    /// <param name="tile">The shortcut.</param>
    /// <returns>Whether the menu opened.</returns>
    public bool OpenMenu(DockTileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _menu.Open(tile.Id, tile.AccessibleName, tile.Icon, _inFrequents());
        return true;
    }
}
