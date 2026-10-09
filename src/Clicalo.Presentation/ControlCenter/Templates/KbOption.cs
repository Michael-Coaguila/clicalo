namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>An option of the keyboard line (PLA-009): a layout or a programs language.</summary>
/// <param name="Id">Its id: a layout id or a language code.</param>
/// <param name="Label">Its name.</param>
/// <param name="Selected">Whether it is the one in use.</param>
/// <param name="Detected">Whether Windows suggests it ([detected]).</param>
public sealed record KbOption(
    string Id,
    string Label,
    bool Selected,
    bool Detected
);
