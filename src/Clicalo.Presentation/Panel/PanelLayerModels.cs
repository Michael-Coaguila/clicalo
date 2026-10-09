using Clicalo.Presentation.Panel.ContextMenu;
using Clicalo.Presentation.Panel.EditMode;
using Clicalo.Presentation.Panel.QuickSettings;
using Clicalo.Presentation.Panel.TestMode;

namespace Clicalo.Presentation.Panel;

/// <summary>
/// The layers of the panel above its tiles (docs/04): Quick settings, edit mode, the tile menu and test mode, with
/// <see cref="TileInteractionModes"/>, which every gesture on a tile asks before the tile runs. The composition creates
/// them once; the surface hosts their components and routes the gestures through <see cref="Modes"/>.
/// </summary>
/// <param name="QuickSettings">Quick settings (AJR-001).</param>
/// <param name="EditMode">Edit mode (CUA-012).</param>
/// <param name="Menu">The tile menu (CUA-014).</param>
/// <param name="TestMode">Test mode (TAC-008).</param>
/// <param name="Modes">What a gesture on a tile does before the engine (EJE-001).</param>
/// <param name="InFrequents">Whether Frequents is in view, for the rows of the tile menu (CUA-013).</param>
public sealed record PanelLayerModels(
    QuickSettingsViewModel QuickSettings,
    EditModeViewModel EditMode,
    TileContextMenuViewModel Menu,
    TestModeViewModel TestMode,
    TileInteractionModes Modes,
    Func<bool> InFrequents
);
