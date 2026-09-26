using Clicalo.Domain.Document;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Store;

/// <summary>One step of the undo history (blueprint §6.4).</summary>
/// <param name="Before">The document before the step (structural sharing keeps it cheap).</param>
/// <param name="Slices">The slices the step touched; undo restores only these.</param>
/// <param name="Label">What «Undo» names.</param>
/// <param name="CoalesceKey">Grouping key while the entry is open, or <see langword="null"/>.</param>
/// <param name="Sealed">Whether later changes with the same key start a new entry (EDI-021).</param>
public sealed record UndoEntry(
    UserDocument Before,
    DocumentSlices Slices,
    MessageKey Label,
    string? CoalesceKey,
    bool Sealed
);
