namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>The update card (ACT-001): icon, title, subtitle, the bar while installing and the button of 48.</summary>
/// <param name="Icon">Its icon.</param>
/// <param name="Title">Its title.</param>
/// <param name="Subtitle">Its subtitle.</param>
/// <param name="ShowBar">Whether the progress bar shows.</param>
/// <param name="Percent">The progress, 0 to 100.</param>
/// <param name="Button">The text of the button.</param>
/// <param name="ButtonEnabled">Whether the button acts (not while checking or installing).</param>
/// <param name="Hot">Whether it is the new version: warn icon and accent button.</param>
/// <param name="Warning">Whether it is an error.</param>
public sealed record UpdateCardModel(
    string Icon,
    string Title,
    string Subtitle,
    bool ShowBar,
    int Percent,
    string Button,
    bool ButtonEnabled,
    bool Hot,
    bool Warning
);
