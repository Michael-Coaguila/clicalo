using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>One tile as the projection sees it: immutable, compared by value.</summary>
/// <param name="Id">The shortcut id, the key of the tile in the view (KeyedCollectionSync of blueprint §8.2).</param>
/// <param name="Name">The shortcut name in the interface language (user data, shown as is).</param>
/// <param name="Behavior">How it reacts to the finger.</param>
/// <param name="Binding">What it runs.</param>
/// <param name="Icon">The Material Symbols icon of the shortcut (CUA-007).</param>
/// <param name="Category">The color category of the shortcut (TEM-003).</param>
/// <param name="Keys">
/// The line under the name (CUA-007): the combination the tile sends, abbreviated in size S (CUA-008), or the origin
/// of a Frequents tile or of a search result; empty hides it.
/// </param>
/// <param name="SpokenKeys">
/// The combination with full key names for screen readers (CUA-008), or the origin; empty when there is none.
/// </param>
public sealed record TileModel(
    ShortcutId Id,
    string Name,
    TileBehavior Behavior,
    TileBinding Binding,
    IconRef Icon,
    CategoryId Category,
    string Keys = "",
    string SpokenKeys = ""
);
