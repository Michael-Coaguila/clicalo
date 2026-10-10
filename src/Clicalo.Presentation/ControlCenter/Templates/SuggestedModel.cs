using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>«Apps abiertas sin perfil» (PLA-011).</summary>
/// <param name="Title">[tplSuggested].</param>
/// <param name="DetectText">[asShort].</param>
/// <param name="DetectOn">The «Detectar» switch (autoSuggestProfiles).</param>
/// <param name="Cards">The open apps with a template and no profile.</param>
/// <param name="EmptyText">[noSugOn] or [noSugOff] when there is none.</param>
/// <param name="PreviewText">[preview].</param>
/// <param name="InstallText">[installBtn].</param>
public sealed record SuggestedModel(
    string Title,
    string DetectText,
    bool DetectOn,
    ValueList<TemplateCard> Cards,
    string? EmptyText,
    string PreviewText,
    string InstallText
);
