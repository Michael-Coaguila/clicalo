using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The discreet answer to an ignored touch (TAC-003): a slight outline of 2 px in muted around the tile while
/// <see cref="IIgnoredTouchState.IsIgnored"/>, which the composition keeps for
/// <c>Timings.Touch.IgnoredTouchFeedback</c>. No sound and no animation; it lets touches pass through and adds nothing
/// to UI Automation. The tiles of the panel and the shortcuts of the Tab view share it.
/// </summary>
/// <remarks>
/// The tile cell lays it over the tile (same cell, on top). It follows the tile through a weak event, so a cell that
/// is thrown away without <see cref="Detach"/> does not keep it alive.
/// </remarks>
public sealed class IgnoredTouchOutline : Border
{
    private const double OutlinePx = 2;

    private readonly IIgnoredTouchState _tile;

    /// <summary>Creates the outline of a tile.</summary>
    /// <param name="tile">The tile.</param>
    public IgnoredTouchOutline(IIgnoredTouchState tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _tile = tile;
        CornerRadius = new CornerRadius(Radii.Tile);
        BorderThickness = new Thickness(OutlinePx);
        IsHitTestVisible = false;
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Muted));
        PropertyChangedEventManager.AddHandler(
            _tile,
            OnTileChanged,
            nameof(IIgnoredTouchState.IsIgnored)
        );
        Refresh();
    }

    /// <summary>Stops following the tile when it goes away.</summary>
    public void Detach() =>
        PropertyChangedEventManager.RemoveHandler(
            _tile,
            OnTileChanged,
            nameof(IIgnoredTouchState.IsIgnored)
        );

    private void OnTileChanged(object? sender, PropertyChangedEventArgs change) => Refresh();

    private void Refresh() =>
        Visibility = _tile.IsIgnored ? Visibility.Visible : Visibility.Collapsed;
}
