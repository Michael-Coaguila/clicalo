using Clicalo.Domain.Settings;

namespace Clicalo.Presentation.Welcome;

/// <summary>
/// A card of step 3 (BIE-007): the icon of the view (its miniature, in the same icon box for the three, as in
/// «General y panel»), its name and its description.
/// </summary>
/// <param name="Density">The view.</param>
/// <param name="Icon">The icon of the view.</param>
/// <param name="Label">[dFull], [dCompact] or [dDock].</param>
/// <param name="Description">[dFullD], [dCompactD] or [dDockD].</param>
/// <param name="Selected">Whether it is the view in use.</param>
public sealed record WelcomeViewCard(
    PanelDensity Density,
    string Icon,
    string Label,
    string Description,
    bool Selected
);
