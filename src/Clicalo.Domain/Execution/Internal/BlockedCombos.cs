using System.Collections.Frozen;
using Clicalo.Domain.Keys;

namespace Clicalo.Domain.Execution.Internal;

/// <summary>
/// The combinations the panel never sends (EJE-014): the «blocked» entries of <c>data/catalogs/blocked-combos.json</c>
/// (Ctrl+Alt+Del, and Win+L, which the «Lock» system action replaces). Compared as sets of base keys, whatever the
/// order and the side, so a blocked combination can never slip through with another side. <c>BlockedCombosTests</c>
/// compares this list with the JSON.
/// </summary>
internal static class BlockedCombos
{
    /// <summary>The blocked combinations, as sets of base key ids.</summary>
    public static IReadOnlyList<FrozenSet<string>> All { get; } =
    [
        new[] { "ctrl", "alt", "delete" }.ToFrozenSet(StringComparer.Ordinal),
        new[] { "win", "l" }.ToFrozenSet(StringComparer.Ordinal),
    ];

    private static readonly FrozenDictionary<string, string> BaseOf = KeyDefinitions
        .All.Where(static d => d.BaseKey is not null)
        .ToFrozenDictionary(
            static d => d.Id.Value,
            static d => d.BaseKey!.Value.Value,
            StringComparer.Ordinal
        );

    /// <summary>Whether <paramref name="chord"/> is a blocked combination.</summary>
    /// <param name="chord">The combination.</param>
    public static bool IsBlocked(KeyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        if (chord.IsEmpty)
        {
            return false;
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stroke in chord.Strokes)
        {
            var id = stroke.Key.Value ?? string.Empty;
            keys.Add(BaseOf.TryGetValue(id, out var baseKey) ? baseKey : id);
        }

        return All.Any(blocked => blocked.SetEquals(keys));
    }
}
