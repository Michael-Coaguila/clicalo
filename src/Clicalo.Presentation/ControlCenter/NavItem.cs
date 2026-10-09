namespace Clicalo.Presentation.ControlCenter;

/// <summary>An item of the side menu (CCM-002).</summary>
/// <param name="Section">The section it opens.</param>
/// <param name="Icon">Its Material Symbols icon.</param>
/// <param name="Label">Its name, also its accessible name in the narrow menu.</param>
/// <param name="Count">The warn counter in its corner; 0 for none.</param>
/// <param name="CountName">The counter in words, for UI Automation; empty for none.</param>
/// <param name="Selected">Whether it is the section in view: accentWash, accent text and the selected state.</param>
/// <param name="SeparatorBefore">Whether a separator goes before it (Sistema).</param>
public sealed record NavItem(
    ControlCenterSection Section,
    string Icon,
    string Label,
    int Count,
    string CountName,
    bool Selected,
    bool SeparatorBefore
);
