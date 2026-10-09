namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Primeros pasos» (GEN-014).</summary>
/// <param name="Caption">[secStart].</param>
/// <param name="WelcomeTitle">[seeWelcome].</param>
/// <param name="WelcomeDescription">[seeWelcomeD].</param>
/// <param name="CoachTitle">«Ver la guía de la pestaña» (PES-015).</param>
/// <param name="CoachDescription">When the guide shows again.</param>
/// <param name="NoKeyboard">[uNokb] (BIE-005).</param>
public sealed record StartModel(
    string Caption,
    string WelcomeTitle,
    string WelcomeDescription,
    string CoachTitle,
    string CoachDescription,
    SwitchItem NoKeyboard
);
