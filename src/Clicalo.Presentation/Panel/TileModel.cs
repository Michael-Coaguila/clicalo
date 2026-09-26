using Clicalo.Application.Coordinators;
using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.Panel;

/// <summary>One tile as the projection sees it: immutable, compared by value.</summary>
/// <param name="Id">The shortcut id, the key of the tile in the view (KeyedCollectionSync of blueprint §8.2).</param>
/// <param name="Name">The shortcut name in the interface language (user data, shown as is).</param>
/// <param name="Behavior">How it reacts to the finger.</param>
/// <param name="Binding">What it runs.</param>
public sealed record TileModel(
    ShortcutId Id,
    string Name,
    TileBehavior Behavior,
    TileBinding Binding
);
