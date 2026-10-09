using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Templates;

/// <summary>A template card (PLA-011, PLA-012).</summary>
/// <param name="Id">The template id.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Name">Its name.</param>
/// <param name="Meta">«N atajos», or [sugLine] for an open app.</param>
/// <param name="Icons">The icons of its first six shortcuts.</param>
/// <param name="Selected">Whether it is in preview.</param>
public sealed record TemplateCard(
    string Id,
    string Icon,
    string Name,
    string Meta,
    ValueList<string> Icons,
    bool Selected
);
