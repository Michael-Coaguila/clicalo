namespace Clicalo.Presentation.ControlCenter.General;

/// <summary>«Primeros pasos» (GEN-014).</summary>
/// <param name="Caption">[secStart].</param>
/// <param name="WelcomeTitle">[seeWelcome].</param>
/// <param name="WelcomeDescription">[seeWelcomeD].</param>
/// <param name="CoachTitle">[seeGuides]: every guide shows again (R-39).</param>
/// <param name="CoachDescription">[seeGuidesD].</param>
/// <param name="NoKeyboard">[uNokb] (BIE-005).</param>
public sealed record StartModel(
    string Caption,
    string WelcomeTitle,
    string WelcomeDescription,
    string CoachTitle,
    string CoachDescription,
    SwitchItem NoKeyboard
);
