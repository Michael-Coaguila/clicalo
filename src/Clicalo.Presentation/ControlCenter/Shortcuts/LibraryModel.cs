using Clicalo.Domain.Primitives;

namespace Clicalo.Presentation.ControlCenter.Shortcuts;

/// <summary>State A of the editor column: «Añadir atajo» (ATJ-010).</summary>
/// <param name="Title">[addTitle].</param>
/// <param name="CloseName">[close].</param>
/// <param name="CreateTitle">[createOwn].</param>
/// <param name="CreateText">[createOwnD].</param>
/// <param name="OrPick">[orPick].</param>
/// <param name="Chips">The categories.</param>
/// <param name="Rows">The ready actions of the chosen category.</param>
public sealed record LibraryModel(
    string Title,
    string CloseName,
    string CreateTitle,
    string CreateText,
    string OrPick,
    ValueList<LibraryChip> Chips,
    ValueList<LibraryRow> Rows
);
