using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;

namespace Clicalo.Presentation.ControlCenter.Editor;

/// <summary>The icon picker (EDI-004).</summary>
/// <param name="Suggested">Up to six suggestions.</param>
/// <param name="SearchPlaceholder">[iconSearch].</param>
/// <param name="Icons">The grid: the search results, or the featured icons and then the rest.</param>
/// <param name="DictateName">The accessible name of the dictation button next to the search (ACC-011).</param>
public sealed record IconPickerModel(
    ValueList<IconOption> Suggested,
    string SearchPlaceholder,
    ValueList<IconOption> Icons,
    string DictateName
);
