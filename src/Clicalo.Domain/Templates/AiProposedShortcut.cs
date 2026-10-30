using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>A shortcut of an <see cref="AiTemplateProposal"/>: only a combination to press (PLA-008).</summary>
/// <param name="NameEs">Its name in Spanish.</param>
/// <param name="NameEn">Its name in English.</param>
/// <param name="Icon">A Material Symbols icon.</param>
/// <param name="Keys">Key ids of <c>data/catalogs/keys.json</c>, in press order.</param>
/// <param name="Category">One of the ten colour categories.</param>
/// <param name="Confidence">How sure the AI is, from 0 to 1.</param>
public sealed record AiProposedShortcut(
    string NameEs,
    string NameEn,
    string Icon,
    ValueList<string> Keys,
    string Category,
    double Confidence
);
