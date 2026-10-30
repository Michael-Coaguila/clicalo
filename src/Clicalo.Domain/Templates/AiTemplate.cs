namespace Clicalo.Domain.Templates;

/// <summary>An AI proposal that <see cref="TemplateSchema"/> accepted, ready for the preview (PLA-007, PLA-015).</summary>
/// <param name="Template">
/// The proposal as a template: only taps, keys of the catalog and safe names. Its processes are empty when the
/// program is unknown: the process then comes from the capture or the user's choice, never from a guess (PLA-007).
/// </param>
/// <param name="Known">Whether the AI knows the program; otherwise the preview shows [unknownMsg].</param>
public sealed record AiTemplate(ProfileTemplate Template, bool Known);
