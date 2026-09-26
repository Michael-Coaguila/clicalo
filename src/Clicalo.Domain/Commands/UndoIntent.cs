using Clicalo.Domain.Messages;

namespace Clicalo.Domain.Commands;

/// <summary>How a change enters the undo history of the document store (blueprint §6.4).</summary>
public abstract record UndoIntent
{
    private UndoIntent() { }

    /// <summary>
    /// An undoable step. Consecutive changes with the same <paramref name="CoalesceKey"/> join the top entry until the
    /// store seals it (EDI-021: the editor switches to another shortcut or profile).
    /// </summary>
    /// <param name="Label">What «Undo» names, for example «Undo: delete shortcut».</param>
    /// <param name="CoalesceKey">Grouping key (usually the edited id), or <see langword="null"/> for none.</param>
    public sealed record Record(MessageKey Label, string? CoalesceKey) : UndoIntent;

    /// <summary>Does not touch the history (usage, presentation settings, positions).</summary>
    public sealed record Transparent : UndoIntent;

    /// <summary>Empties the history (a change that older entries cannot be undone across).</summary>
    public sealed record Barrier : UndoIntent;
}
