using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>
/// The icon picker (EDI-004, D14): every icon of the bundled collection, by category, with a search in Spanish and in
/// English and the suggestions for the name above. It is a layer over the editor: <see cref="Title"/> and ✕ close it.
/// </summary>
/// <param name="Title">[changeIcon].</param>
/// <param name="CloseName">[close].</param>
/// <param name="Current">The icon in use, marked in the grid.</param>
/// <param name="SuggestedLabel">[iconSug].</param>
/// <param name="Suggested">Up to six suggestions for the name (EDI-005).</param>
/// <param name="Query">The search as typed.</param>
/// <param name="SearchPlaceholder">[iconSearch].</param>
/// <param name="DictateName">The accessible name of the dictation button next to the search (ACC-011).</param>
/// <param name="Categories">«Todos» and the categories of the collection, the chosen one marked.</param>
/// <param name="Icons">
/// The icons of the chosen category that match the search, in the order of the library: without a search, the featured
/// ones first. All of them, however many: the grid only builds the rows in view.
/// </param>
/// <param name="EmptyText">[noResults] when nothing matches; else null.</param>
/// <param name="IconNames">
/// The name of each of <paramref name="Icons"/>, in the same order, in the language of the interface: what UI
/// Automation and voice access call its cell («negrita», not «format bold», REG-06).
/// </param>
public sealed record IconPickerModel(
    string Title,
    string CloseName,
    string Current,
    string SuggestedLabel,
    ValueList<IconOption> Suggested,
    string Query,
    string SearchPlaceholder,
    string DictateName,
    ValueList<IconCategoryOption> Categories,
    ValueList<string> Icons,
    string? EmptyText,
    ValueList<string> IconNames
);
