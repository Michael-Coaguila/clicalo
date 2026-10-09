using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// A profile template (<c>data/content/templates/&lt;id&gt;.json</c>, CAT-006): installed, it becomes a profile bound
/// to <b>every</b> process of the template (user decision D2, PQ-45), such as the five browsers of «Browser».
/// </summary>
/// <param name="Id">The template id, equal to its file name.</param>
/// <param name="Version">Increases with every change; stored in the catalog reference (DAT-004).</param>
/// <param name="Name">Name in every language.</param>
/// <param name="Icon">Material Symbols icon.</param>
/// <param name="Processes">The executables it binds to, without path; never empty.</param>
/// <param name="Shortcuts">Its shortcuts, in display order.</param>
public sealed record ProfileTemplate(
    string Id,
    int Version,
    LocalizedText Name,
    IconRef Icon,
    ValueList<ProcessName> Processes,
    ValueList<TemplateShortcut> Shortcuts
)
{
    /// <summary>
    /// The programs languages whose shortcuts were reviewed (<c>appsLanguages</c>); empty means every language. The
    /// preview warns [onlyEs] when the programs language is not one of them (PLA-015).
    /// </summary>
    public ValueList<LangCode> AppsLanguages { get; init; } = [];

    /// <summary>Whether the shortcuts were reviewed for <paramref name="appsLanguage"/>.</summary>
    /// <param name="appsLanguage">The programs language of the keyboard settings (PLA-009).</param>
    public bool IsReviewedFor(LangCode appsLanguage) =>
        AppsLanguages.IsEmpty || AppsLanguages.Items.Contains(appsLanguage);
}
