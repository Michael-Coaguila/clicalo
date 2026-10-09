using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>The shortcuts grid and its note (ATJ-009).</summary>
/// <param name="Tiles">The tiles in order.</param>
/// <param name="LibraryText">[fromLib].</param>
/// <param name="IncompleteText">[incomplete].</param>
/// <param name="Hint">The note about editing and reordering.</param>
public sealed record GridModel(
    ValueList<GridTile> Tiles,
    string LibraryText,
    string IncompleteText,
    string Hint
);
