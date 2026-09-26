using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// A button of the panel (docs/02 <c>Button</c>, blueprint §6.2). It lives in exactly one list, Always visible or a
/// profile (invariant I2); moving it never copies it.
/// </summary>
/// <param name="Id">Stable id, unique in the document (I1).</param>
/// <param name="Name">Name in every language; new shortcuts get the same text in all of them.</param>
/// <param name="Icon">Material Symbols icon.</param>
/// <param name="AutoIcon">Whether the icon follows the name (suggested again when the name changes).</param>
/// <param name="Category">Colour category.</param>
/// <param name="Action">What it does.</param>
/// <param name="Options">Confirmation, hold limit and privacy.</param>
/// <param name="Origin">Template or library element it was installed from (DAT-004).</param>
/// <param name="PinnedFrom">The profile it came from when it was pinned to Always visible.</param>
public sealed record Shortcut(
    ShortcutId Id,
    LocalizedText Name,
    IconRef Icon,
    bool AutoIcon,
    CategoryId Category,
    ShortcutAction Action,
    ShortcutOptions Options,
    CatalogRef? Origin,
    ProfileId? PinnedFrom
);
