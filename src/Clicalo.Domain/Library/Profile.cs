using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// A profile: a named list of shortcuts, optionally bound to apps (docs/02, blueprint §6.2). Renaming keeps its id
/// (PER-008); editing its name, icon or compatible mode is undoable (REG-07).
/// </summary>
/// <param name="Id">Stable id; <see cref="ProfileId.General"/> for General.</param>
/// <param name="Name">Name in every language.</param>
/// <param name="Icon">Material Symbols icon.</param>
/// <param name="AutoIcon">Whether the icon follows the name.</param>
/// <param name="Binding">The apps it follows.</param>
/// <param name="Injection">How its keys are sent; persisted as <c>compat</c> (D24).</param>
/// <param name="Shortcuts">Its shortcuts, in display order.</param>
/// <param name="Origin">Template it was installed from (DAT-004).</param>
public sealed record Profile(
    ProfileId Id,
    LocalizedText Name,
    IconRef Icon,
    bool AutoIcon,
    AppBinding Binding,
    InjectionMode Injection,
    ValueList<Shortcut> Shortcuts,
    CatalogRef? Origin
);
