using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.UseCases.Library;

/// <summary>A category chip of «Añadir atajo» and its rows (ATJ-010).</summary>
/// <param name="Id">Its id: <see cref="LibraryCategories.ProfileCategory"/> or the id of a library section.</param>
/// <param name="Label">[lcFor] {perfil}, [lcEdit], [lcWin], [lcMouse], [lcVoice], [lcText] or [lcSys].</param>
/// <param name="Icon">The icon of the chip.</param>
/// <param name="Items">Its ready actions, in order.</param>
/// <param name="Source">The source of the catalog reference of what it installs (DAT-004).</param>
/// <param name="Version">The version of that source.</param>
public sealed record LibraryCategory(
    string Id,
    Message Label,
    string Icon,
    ValueList<TemplateShortcut> Items,
    string Source,
    int Version
);
