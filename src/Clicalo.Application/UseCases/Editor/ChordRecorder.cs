using Clicalo.Domain.Keys;

namespace Clicalo.Application.UseCases.Editor;

/// <summary>
/// «Grabar con teclado» as a rule (EDI-010): the modifiers held are remembered in the order they were pressed and
/// with their side; the first key that is not a modifier closes the recording with those modifiers and that key. Esc
/// cancels. A key that is not in the catalog is ignored. Pure: the view feeds it the keys the Control Center window
/// receives while the recording is on, and nothing observes the keyboard outside it.
/// </summary>
public sealed class ChordRecorder
{
    private static readonly Dictionary<string, KeyDefinition> Known =
        KeyDefinitions.All.ToDictionary(static d => d.Id.Value, StringComparer.Ordinal);

    private readonly List<KeyId> _held = [];

    /// <summary>A key went down.</summary>
    /// <param name="key">The catalog key, with its side for a modifier (<c>lctrl</c>, <c>altgr</c>).</param>
    /// <returns>What the recording did with it.</returns>
    public ChordRecording Down(KeyId key)
    {
        if (key == KeyIds.Escape)
        {
            _held.Clear();
            return new ChordRecording.Cancelled();
        }

        if (!Known.TryGetValue(key.Value ?? string.Empty, out var definition))
        {
            return new ChordRecording.Waiting();
        }

        if (definition.IsModifier)
        {
            // Auto-repeat sends the same modifier again: it keeps its place.
            if (!_held.Exists(held => SameModifier(held, definition)))
            {
                _held.Add(key);
            }

            return new ChordRecording.Waiting();
        }

        var chord = KeyChord.Create([.. _held.Select(static k => new KeyStroke(k)), new(key)]);
        _held.Clear();
        return chord.IsEmpty ? new ChordRecording.Waiting() : new ChordRecording.Recorded(chord);
    }

    /// <summary>A key went up: a released modifier is no longer part of the combination.</summary>
    /// <param name="key">The catalog key.</param>
    public void Up(KeyId key) =>
        _ = _held.RemoveAll(held => string.Equals(held.Value, key.Value, StringComparison.Ordinal));

    private static bool SameModifier(KeyId held, KeyDefinition pressed) =>
        Known.TryGetValue(held.Value ?? string.Empty, out var other)
        && other.Modifier == pressed.Modifier
        && other.Side == pressed.Side;
}
