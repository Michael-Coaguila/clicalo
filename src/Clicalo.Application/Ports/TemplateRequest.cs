using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Ports;

/// <summary>
/// What a template generation sends (PLA-008, ADR-0014): <b>exactly</b> these four values and nothing else leaves the
/// machine — never documents, window titles or the person's shortcuts.
/// </summary>
/// <param name="AppName">The program name typed or dictated, cut to <c>Timings.Ai.AiAppNameMaxLength</c>.</param>
/// <param name="Layout">The keyboard layout of the keyboard line (<c>es-LA</c>, PLA-009).</param>
/// <param name="ProgramsLang">The programs language (PLA-009).</param>
/// <param name="UiLang">The interface language.</param>
public sealed record TemplateRequest(
    string AppName,
    string Layout,
    LangCode ProgramsLang,
    LangCode UiLang
);
