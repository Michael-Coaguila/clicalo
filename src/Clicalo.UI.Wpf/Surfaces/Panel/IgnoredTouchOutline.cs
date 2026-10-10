using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The discreet answer to an ignored touch (TAC-003): a slight outline of 2 px in muted around the tile while
/// <see cref="TileViewModel.IsIgnored"/>, which the composition keeps for <c>Timings.Touch.IgnoredTouchFeedback</c>.
/// No sound and no animation; it lets touches pass through and adds nothing to UI Automation.
/// </summary>
/// <remarks>The tile cell lays it over the tile (same cell, on top).</remarks>
public sealed class IgnoredTouchOutline : Border
{
    private const double OutlinePx = 2;

    private readonly TileViewModel _tile;

    /// <summary>Creates the outline of a tile.</summary>
    /// <param name="tile">The tile.</param>
    public IgnoredTouchOutline(TileViewModel tile)
    {
        ArgumentNullException.ThrowIfNull(tile);
        _tile = tile;
        CornerRadius = new CornerRadius(Radii.Tile);
        BorderThickness = new Thickness(OutlinePx);
        IsHitTestVisible = false;
        SetResourceReference(BorderBrushProperty, ThemeBrushKey.For(ColorToken.Muted));
        _tile.PropertyChanged += OnTileChanged;
        Refresh();
    }

    /// <summary>Stops following the tile when it goes away.</summary>
    public void Detach() => _tile.PropertyChanged -= OnTileChanged;

    private void OnTileChanged(object? sender, PropertyChangedEventArgs change)
    {
        if (
            string.Equals(
                change.PropertyName,
                nameof(TileViewModel.IsIgnored),
                StringComparison.Ordinal
            )
        )
        {
            Refresh();
        }
    }

    private void Refresh() =>
        Visibility = _tile.IsIgnored ? Visibility.Visible : Visibility.Collapsed;
}
