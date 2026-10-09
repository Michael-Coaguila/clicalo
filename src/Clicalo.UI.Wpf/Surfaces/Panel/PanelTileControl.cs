using Clicalo.Presentation.Panel;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// A shortcut tile on screen (grid or Always visible row) and its view model: the surface registers it with the
/// gesture recognizer as a <c>Tap</c> or <c>Hold</c> target and forwards taps and holds with the contact's device and
/// summary, as it does for every tile (FIJ-004: same states, filter, execution and long press as the grid).
/// </summary>
/// <param name="ViewModel">The tile's view model.</param>
/// <param name="Control">The control.</param>
public sealed record PanelTileControl(TileViewModel ViewModel, ShortcutTile Control);
