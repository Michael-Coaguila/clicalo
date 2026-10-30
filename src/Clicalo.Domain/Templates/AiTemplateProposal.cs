using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// An AI answer that passed the structural check against <c>data/schemas/ai-template.v1.schema.json</c> (blueprint
/// §9.2, step 1). It is still untrusted content (LOG-006): <see cref="TemplateSchema"/> decides what of it can reach
/// the preview.
/// </summary>
/// <param name="Known">Whether the AI knows the program (PLA-007).</param>
/// <param name="App">The program name as the AI writes it.</param>
/// <param name="Process">Its executable, or empty when the AI does not give one.</param>
/// <param name="Icon">A Material Symbols icon for the profile.</param>
/// <param name="Shortcuts">The proposed shortcuts, in order.</param>
public sealed record AiTemplateProposal(
    bool Known,
    string App,
    string Process,
    string Icon,
    ValueList<AiProposedShortcut> Shortcuts
);
