using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// The data the editor reads (D17): the icon library and the icons of combinations (EDI-004, EDI-005), the
/// «Añadir atajo» library (ATJ-010), the starter content with the templates of «Para {perfil}» and the key labels.
/// Missing data gives the empty form: the editor still works with the default icons and «Crear el mío».
/// </summary>
/// <param name="Icons">The icon library.</param>
/// <param name="Combos">The icons of combinations by programs language.</param>
/// <param name="Library">The ready actions.</param>
/// <param name="Starter">The seed and the templates; null when they could not be read.</param>
/// <param name="KeyLabels">The names of the keys.</param>
public sealed record EditorCatalogs(
    IconCatalog Icons,
    ComboIconTable Combos,
    LibraryContent Library,
    StarterContent? Starter,
    KeyLabelCatalog KeyLabels
)
{
    /// <summary>Nothing loaded yet.</summary>
    public static EditorCatalogs Empty { get; } =
        new(
            IconCatalog.Empty,
            ComboIconTable.Empty,
            LibraryContent.Empty,
            null,
            KeyLabelCatalog.Empty
        );
}
