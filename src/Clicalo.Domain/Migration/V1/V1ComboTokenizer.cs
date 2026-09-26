using System.Collections.Immutable;
using System.Text;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// The v1 combination grammar (catalog §7.3, MIG-003): chords separated by spaces, <c>+</c> as a key after another
/// <c>+</c> (<c>ctrl++</c>) or after <c>num</c> (<c>num+</c>), aliases and sides, U+2212 as minus, saved order kept.
/// </summary>
public static class V1ComboTokenizer
{
    private const char Separator = '+';
    private const string NumPrefix = "num";

    /// <summary>Tokenizes a v1 combination; never produces an empty token (MIG-003).</summary>
    /// <param name="text">The <c>hotkey</c> text.</param>
    public static V1ComboParse Tokenize(string text)
    {
        var scan = Scan(text);
        var chords = ImmutableArray.CreateBuilder<KeyChord>(scan.Chords.Count);
        foreach (var strokes in scan.Chords)
        {
            chords.Add(KeyChord.Create(strokes));
        }

        return new V1ComboParse(new ValueList<KeyChord>(chords.MoveToImmutable()), scan.Unresolved);
    }

    /// <summary>
    /// Splits a v1 combination into strokes without building chords: lower case, spaces around a <c>+</c> trimmed
    /// (v1 trimmed every token), one chord per remaining space-separated group, in the saved order.
    /// </summary>
    /// <param name="text">The <c>hotkey</c> text.</param>
    public static V1ComboScan Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var chords = ImmutableArray.CreateBuilder<ValueList<KeyStroke>>();
        var unresolved = ImmutableArray.CreateBuilder<string>();
        var strokes = ImmutableArray.CreateBuilder<KeyStroke>();
        foreach (var chord in SplitChords(text.ToLowerInvariant()))
        {
            strokes.Clear();
            ScanChord(chord, strokes, unresolved);
            if (strokes.Count > 0)
            {
                chords.Add(new ValueList<KeyStroke>(strokes.ToImmutable()));
            }
        }

        return new V1ComboScan(
            new ValueList<ValueList<KeyStroke>>(chords.ToImmutable()),
            new ValueList<string>(unresolved.ToImmutable())
        );
    }

    /// <summary>
    /// The chords of <paramref name="text"/>: whitespace next to a <c>+</c> belongs to the token (v1 trimmed it), any
    /// other run of whitespace separates two chords.
    /// </summary>
    private static List<string> SplitChords(string text)
    {
        var chords = new List<string>();
        var current = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (!char.IsWhiteSpace(text[i]))
            {
                current.Append(text[i]);
                continue;
            }

            var next = i;
            while (next < text.Length && char.IsWhiteSpace(text[next]))
            {
                next++;
            }

            var touchesSeparator =
                (current.Length > 0 && current[^1] == Separator)
                || (next < text.Length && text[next] == Separator);
            if (!touchesSeparator && current.Length > 0)
            {
                chords.Add(current.ToString());
                current.Clear();
            }

            i = next - 1;
        }

        if (current.Length > 0)
        {
            chords.Add(current.ToString());
        }

        return chords;
    }

    private static void ScanChord(
        string chord,
        ImmutableArray<KeyStroke>.Builder strokes,
        ImmutableArray<string>.Builder unresolved
    )
    {
        var token = new StringBuilder(chord.Length);
        var endsWithSeparator = false;
        foreach (var c in chord)
        {
            endsWithSeparator = false;
            if (c != Separator)
            {
                token.Append(c);
                continue;
            }

            if (token.Length == 0 || token.Equals(NumPrefix.AsSpan()))
            {
                // «+» right after a separator (or first) is the plus key; right after «num» it is the keypad key.
                token.Append(c);
                continue;
            }

            Resolve(token.ToString(), strokes, unresolved);
            token.Clear();
            endsWithSeparator = true;
        }

        if (token.Length > 0)
        {
            Resolve(token.ToString(), strokes, unresolved);
        }
        else if (endsWithSeparator)
        {
            // «ctrl+»: a separator with nothing after it is never an empty token; the chord is marked for review.
            unresolved.Add(chord);
        }
    }

    private static void Resolve(
        string token,
        ImmutableArray<KeyStroke>.Builder strokes,
        ImmutableArray<string>.Builder unresolved
    )
    {
        if (V1KeyTokens.TryResolve(token, out var stroke))
        {
            strokes.Add(stroke);
        }
        else
        {
            unresolved.Add(token);
        }
    }
}
