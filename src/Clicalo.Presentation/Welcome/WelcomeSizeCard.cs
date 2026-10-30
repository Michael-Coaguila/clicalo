using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.Welcome;

/// <summary>A size of step 4 (BIE-008): a «Copiar» button at real scale and its name.</summary>
/// <param name="Size">The size.</param>
/// <param name="Label">[sizeS], [sizeM] or [sizeL].</param>
/// <param name="TileWidth">The width of a tile of that size, in logical pixels.</param>
/// <param name="TileHeight">The height of a tile of that size.</param>
/// <param name="IconSize">The icon size of a tile of that size.</param>
/// <param name="TextSize">The label size of a tile of that size.</param>
/// <param name="Selected">Whether it is the size in use.</param>
public sealed record WelcomeSizeCard(
    PanelSize Size,
    string Label,
    double TileWidth,
    double TileHeight,
    double IconSize,
    double TextSize,
    bool Selected
);
