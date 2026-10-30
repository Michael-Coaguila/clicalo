using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>Everything the section Plantillas shows (docs/05 §2).</summary>
/// <param name="Title">[tplTitle].</param>
/// <param name="Subtitle">[tplSub2].</param>
/// <param name="Ai">«Crear con IA».</param>
/// <param name="Blank">«Perfil vacío».</param>
/// <param name="Suggested">«Apps abiertas sin perfil».</param>
/// <param name="AvailableTitle">[tplAvail].</param>
/// <param name="Available">The templates not installed.</param>
/// <param name="AllInstalledText">[allInstalled] when none is left.</param>
/// <param name="InstallText">[installBtn].</param>
/// <param name="Installed">«Tus perfiles».</param>
/// <param name="Preview">The preview column.</param>
public sealed record TemplatesScreen(
    string Title,
    string Subtitle,
    AiCardModel Ai,
    BlankModel Blank,
    SuggestedModel Suggested,
    string AvailableTitle,
    ValueList<TemplateCard> Available,
    string? AllInstalledText,
    string InstallText,
    InstalledModel Installed,
    PreviewModel Preview
);
