namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>A row of 56 of «Añadir atajo» (ATJ-010): add, or a check when it is already in the list.</summary>
/// <param name="Index">Its index in the category.</param>
/// <param name="Icon">Its icon.</param>
/// <param name="Category">Its color category.</param>
/// <param name="Name">Its name.</param>
/// <param name="Foot">Its keys.</param>
/// <param name="Added">Whether it is already in the list.</param>
/// <param name="AccessibleState">[alreadyAdded] when it is.</param>
public sealed record LibraryRow(
    int Index,
    string Icon,
    string Category,
    string Name,
    string Foot,
    bool Added,
    string AccessibleState
);
