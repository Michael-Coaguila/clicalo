namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>The consent card of the first generation with a provider (PLA-004, ADR-0033).</summary>
/// <param name="Title">[consentT].</param>
/// <param name="Text">[consentD4], with the chosen provider.</param>
/// <param name="AcceptText">[consentOk].</param>
/// <param name="DeclineText">[consentNo].</param>
/// <param name="Notice">[aiGeminiFree] with Gemini: on the free tier Google may use and read what is sent.</param>
public sealed record ConsentModel(
    string Title,
    string Text,
    string AcceptText,
    string DeclineText,
    string? Notice = null
);
