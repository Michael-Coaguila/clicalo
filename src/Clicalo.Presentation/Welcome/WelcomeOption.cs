namespace Clicalo.Presentation.Welcome;

/// <summary>
/// A choice of the welcome: a language of step 0, a chip of steps 1 and 2, or a theme of step 4. Chosen ones have
/// accentWash with an accent outline and the Toggle state (BIE-005).
/// </summary>
/// <param name="Id">What it chooses: a language code, a <c>WelcomeUse</c>, a kit option id or a theme.</param>
/// <param name="Icon">Its Material Symbols icon; empty for none.</param>
/// <param name="Label">Its text and accessible name.</param>
/// <param name="Description">Its description (the help text); empty for none.</param>
/// <param name="Selected">Whether it is chosen.</param>
public sealed record WelcomeOption(
    string Id,
    string Icon,
    string Label,
    string Description,
    bool Selected
);
