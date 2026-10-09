namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The consent card of the first generation (PLA-004).</summary>
/// <param name="Title">[consentT].</param>
/// <param name="Text">[consentD4].</param>
/// <param name="AcceptText">[consentOk].</param>
/// <param name="DeclineText">[consentNo].</param>
public sealed record ConsentModel(string Title, string Text, string AcceptText, string DeclineText);
