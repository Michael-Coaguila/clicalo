using System.Collections.Immutable;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Keys;

/// <summary>
/// A normalized combination in <b>press order</b> (EJE-003, EDI-010): sided catalog keys become a base key and a side
/// (EDI-009), repeated strokes are removed and the saved order is kept. The only way to build one is
/// <see cref="Create"/>, so every chord in the model is normalized. Compare combinations for repetition with
/// <see cref="CanonicalChord"/>, never with this order-sensitive value.
/// </summary>
public sealed record KeyChord
{
    private KeyChord(ValueList<KeyStroke> strokes) => Strokes = strokes;

    /// <summary>The empty chord (a draft or a cleared combination; incomplete, ATJ-009).</summary>
    public static KeyChord Empty { get; } = new([]);

    /// <summary>The strokes in press order.</summary>
    public ValueList<KeyStroke> Strokes { get; }

    /// <summary>Whether the chord has no key.</summary>
    public bool IsEmpty => Strokes.IsEmpty;

    /// <summary>
    /// Whether every stroke is a modifier (<c>altright</c>, <c>ctrl+win</c>): valid and complete (catalog §7.3). The
    /// empty chord is not modifiers-only.
    /// </summary>
    public bool IsModifiersOnly
    {
        get
        {
            if (IsEmpty)
            {
                return false;
            }

            foreach (var stroke in Strokes)
            {
                if (
                    !KeyDefinitions.TryGet(stroke.Key, out var definition) || !definition.IsModifier
                )
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Normalizes <paramref name="strokes"/>: sided keys of the catalog (<see cref="KeyDefinition.BaseKey"/>) become
    /// their base key with a side, duplicates are dropped (first occurrence wins) and the order is kept.
    /// </summary>
    /// <remarks>
    /// Strokes without a key (empty or default <see cref="KeyId"/>) are dropped, so no chord ever holds an empty token
    /// (EJE-015). A catalog key that is not a modifier always has <see cref="KeySide.Any"/>, and an undefined side
    /// becomes <see cref="KeySide.Any"/>.
    /// </remarks>
    /// <param name="strokes">Strokes in press order; keys outside the catalog are kept as given.</param>
    public static KeyChord Create(IEnumerable<KeyStroke> strokes)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        var builder = ImmutableArray.CreateBuilder<KeyStroke>();
        var seen = new HashSet<KeyStroke>();
        foreach (var stroke in strokes)
        {
            if (string.IsNullOrEmpty(stroke.Key.Value))
            {
                continue;
            }

            var normalized = Normalize(stroke);
            if (seen.Add(normalized))
            {
                builder.Add(normalized);
            }
        }

        return builder.Count == 0
            ? Empty
            : new KeyChord(new ValueList<KeyStroke>(builder.ToImmutable()));
    }

    /// <summary>A chord of <paramref name="keys"/> in press order, each on its catalog side (see <see cref="Create"/>).</summary>
    /// <param name="keys">Keys in press order.</param>
    public static KeyChord FromKeys(params ReadOnlySpan<KeyId> keys)
    {
        var strokes = new List<KeyStroke>(keys.Length);
        foreach (var key in keys)
        {
            strokes.Add(new KeyStroke(key));
        }

        return Create(strokes);
    }

    private static KeyStroke Normalize(KeyStroke stroke)
    {
        var side = Enum.IsDefined(stroke.Side) ? stroke.Side : KeySide.Any;
        if (!KeyDefinitions.TryGet(stroke.Key, out var definition))
        {
            return new KeyStroke(stroke.Key, side);
        }

        if (definition.BaseKey is { } baseKey)
        {
            return new KeyStroke(baseKey, definition.Side);
        }

        return new KeyStroke(stroke.Key, definition.IsModifier ? side : KeySide.Any);
    }
}
