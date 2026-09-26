using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Application.Store;

/// <summary>
/// The undo stack of the document store: at most <c>Timings.Persistence.UndoDepth</c> entries (20, DAT-006); the
/// oldest is dropped. Redo is possible with this structure, but no requirement asks for it, so the UI does not expose it.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class UndoHistory
{
    /// <summary>Entries in the stack.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>The newest entry, or <see langword="null"/> when empty.</summary>
    public UndoEntry? Top => throw new NotImplementedException();

    /// <summary>Pushes an entry, or joins the open top entry with the same coalescing key (keeping its original <c>Before</c>).</summary>
    /// <param name="entry">The entry.</param>
    public void Record(UndoEntry entry) => throw new NotImplementedException();

    /// <summary>Removes and returns the newest entry.</summary>
    /// <param name="entry">The entry.</param>
    public bool TryPop([NotNullWhen(true)] out UndoEntry? entry) =>
        throw new NotImplementedException();

    /// <summary>Seals the top entry so the next change starts a new one (EDI-021).</summary>
    public void Seal() => throw new NotImplementedException();

    /// <summary>Empties the stack (<c>UndoIntent.Barrier</c>).</summary>
    public void Clear() => throw new NotImplementedException();
}
