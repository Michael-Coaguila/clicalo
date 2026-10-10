using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.Welcome;

/// <summary>A card of step 3 (BIE-007): miniature, name and description of a view.</summary>
/// <param name="Density">The view.</param>
/// <param name="Icon">The icon of its miniature.</param>
/// <param name="Label">[dFull], [dCompact] or [dDock].</param>
/// <param name="Description">[dFullD], [dCompactD] or [dDockD].</param>
/// <param name="MiniatureWidth">The width of the miniature, in logical pixels.</param>
/// <param name="MiniatureHeight">The height of the miniature, in logical pixels.</param>
/// <param name="Selected">Whether it is the view in use.</param>
public sealed record WelcomeViewCard(
    PanelDensity Density,
    string Icon,
    string Label,
    string Description,
    double MiniatureWidth,
    double MiniatureHeight,
    bool Selected
);
