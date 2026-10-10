using System.Windows;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// One touch target of a <see cref="TouchSurface"/>: a button, whose tap runs <see cref="Tap"/>, or a shortcut, whose
/// tap and hold go to <see cref="Tile"/>.
/// </summary>
/// <param name="Element">What is on screen; hidden elements are skipped.</param>
/// <param name="Kind">Tap, or Hold for a Mantener (EJE-004).</param>
/// <param name="Tap">What a tap does, for a button.</param>
/// <param name="Tile">The shortcut, for a tile.</param>
public sealed record SurfaceTarget(
    FrameworkElement Element,
    TouchTargetKind Kind,
    Action? Tap,
    DockTileViewModel? Tile = null
)
{
    /// <summary>
    /// Whether the target lies in a zone of the surface that scrolls now (TAC-004): a Mantener there waits until the
    /// finger shows that it is not scrolling before it holds anything (<see cref="TouchTarget.InScrollZone"/>).
    /// </summary>
    public bool InScrollZone { get; init; }

    /// <summary>A button.</summary>
    /// <param name="element">The button.</param>
    /// <param name="tap">What a tap does.</param>
    public static SurfaceTarget Button(FrameworkElement element, Action tap) =>
        new(element, TouchTargetKind.Tap, tap);

    /// <summary>A shortcut of the bar or of a window beside it.</summary>
    /// <param name="element">The tile.</param>
    /// <param name="tile">The shortcut.</param>
    /// <param name="longPress">
    /// Whether a long press opens its menu (CUA-014, PES-010): every shortcut but a Mantener, which holds instead.
    /// </param>
    public static SurfaceTarget For(
        FrameworkElement element,
        DockTileViewModel tile,
        bool longPress = false
    )
    {
        ArgumentNullException.ThrowIfNull(tile);
        return new SurfaceTarget(
            element,
            longPress ? DockTileModes.KindOf(tile)
                : tile.Behavior == Clicalo.Presentation.Panel.TileBehavior.Hold
                    ? TouchTargetKind.Hold
                : TouchTargetKind.Tap,
            null,
            tile
        );
    }
}
