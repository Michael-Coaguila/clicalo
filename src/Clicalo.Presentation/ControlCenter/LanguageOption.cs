namespace Clicalo.Presentation.ControlCenter;

/// <summary>A button of the ES/EN selector of the title bar (CCM-001).</summary>
/// <param name="Code">The language code.</param>
/// <param name="Label">What the button shows (ES, EN).</param>
/// <param name="Name">Its accessible name (Español, English).</param>
/// <param name="Selected">Whether it is the interface language.</param>
public sealed record LanguageOption(string Code, string Label, string Name, bool Selected);
