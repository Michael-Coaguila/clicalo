using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Timing;

namespace Clicalo.Application.Store;

/// <summary>
/// The undo stack of the document store: at most <c>Timings.Persistence.UndoDepth</c> entries (20, DAT-006); the
/// oldest is dropped. Redo is possible with this structure, but no requirement asks for it, so the UI does not expose it.
/// Not thread-safe: <see cref="DocumentStore"/> uses it under its lock.
/// </summary>
public sealed class UndoHistory
{
    private readonly List<UndoEntry> _entries = new(Timings.Persistence.UndoDepth + 1);

    /// <summary>Entries in the stack.</summary>
    public int Count => _entries.Count;

    /// <summary>The newest entry, or <see langword="null"/> when empty.</summary>
    public UndoEntry? Top => _entries.Count == 0 ? null : _entries[^1];

    /// <summary>
    /// Whether an entry with <paramref name="coalesceKey"/> would join the top entry: the top is open (not sealed) and
    /// has the same key. A <see langword="null"/> key never joins.
    /// </summary>
    /// <param name="coalesceKey">The key of the next entry.</param>
    public bool WouldJoin(string? coalesceKey) =>
        coalesceKey is not null
        && Top is { Sealed: false } top
        && string.Equals(top.CoalesceKey, coalesceKey, StringComparison.Ordinal);

    /// <summary>
    /// Pushes an entry, or joins the open top entry with the same coalescing key (keeping its original <c>Before</c>
    /// and label, and adding the new slices).
    /// </summary>
    /// <param name="entry">The entry.</param>
    public void Record(UndoEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (WouldJoin(entry.CoalesceKey))
        {
            var top = _entries[^1];
            _entries[^1] = top with { Slices = top.Slices | entry.Slices };
            return;
        }

        _entries.Add(entry);
        if (_entries.Count > Timings.Persistence.UndoDepth)
        {
            _entries.RemoveAt(0);
        }
    }

    /// <summary>Removes and returns the newest entry.</summary>
    /// <param name="entry">The entry.</param>
    public bool TryPop([NotNullWhen(true)] out UndoEntry? entry)
    {
        if (_entries.Count == 0)
        {
            entry = null;
            return false;
        }

        entry = _entries[^1];
        _entries.RemoveAt(_entries.Count - 1);
        return true;
    }

    /// <summary>Seals the top entry so the next change starts a new one (EDI-021).</summary>
    public void Seal()
    {
        if (Top is { Sealed: false } top)
        {
            _entries[^1] = top with { Sealed = true };
        }
    }

    /// <summary>Empties the stack (<c>UndoIntent.Barrier</c>).</summary>
    public void Clear() => _entries.Clear();
}
