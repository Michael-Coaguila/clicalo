using Clicalo.Presentation.Dock;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>A shortcut of the bar or of a window beside it ran from a tap.</summary>
/// <param name="tile">The shortcut.</param>
public sealed class DockTileEventArgs(DockTileViewModel tile) : EventArgs
{
    /// <summary>The shortcut.</summary>
    public DockTileViewModel Tile { get; } = tile ?? throw new ArgumentNullException(nameof(tile));
}
