using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>«Tus perfiles» and [importProf] (PLA-014).</summary>
/// <param name="Title">[tplInst].</param>
/// <param name="Count">The number of profiles.</param>
/// <param name="Profiles">The profiles, General included.</param>
/// <param name="ImportText">[importProf].</param>
/// <param name="ShareHint">[shareHint].</param>
public sealed record InstalledModel(
    string Title,
    string Count,
    ValueList<InstalledProfile> Profiles,
    string ImportText,
    string ShareHint
);
