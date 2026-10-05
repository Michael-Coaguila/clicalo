using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// A shortcut as content ships it (seed, templates): what <see cref="TemplateInstaller"/> copies into the document
/// with a new id and its <see cref="CatalogRef"/> (DAT-004). A tap keeps the combination of Spanish programs in its
/// chord and the other programs languages in its variants (CAT-005, data/content/README.md).
/// </summary>
/// <param name="ItemId">Its id inside the content file; the item of the catalog reference.</param>
/// <param name="Name">Name in every language.</param>
/// <param name="Icon">Material Symbols icon.</param>
/// <param name="Category">Colour category.</param>
/// <param name="Action">What it does.</param>
/// <param name="Confirm">Whether the first tap arms and the second runs it (EJE-002).</param>
public sealed record TemplateShortcut(
    string ItemId,
    LocalizedText Name,
    IconRef Icon,
    CategoryId Category,
    ShortcutAction Action,
    bool Confirm
);
