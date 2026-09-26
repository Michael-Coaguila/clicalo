namespace Clicalo.DevCli.AnonymizeV1;

/// <summary>
/// Placeholders «of the same length and type»: every letter becomes a letter (same case), every digit a digit, and
/// spaces and punctuation stay where they were, so a label keeps its words, a path its separators and an address its
/// dots. The same text always gets the same placeholder and two different texts never share one, so repeated names
/// (MIG-008) and references between profiles (<c>active_profile</c>) survive the anonymization.
/// </summary>
internal sealed class Placeholders
{
    private const string Letters = "xabcdefghijklmnopqrstuvwyz";
    private const string Digits = "0123456789";

    private readonly Dictionary<string, string> _assigned = new(StringComparer.Ordinal);
    private readonly HashSet<string> _used = new(StringComparer.Ordinal);
    private readonly Func<string, bool> _isReserved;

    /// <summary>Creates an allocator.</summary>
    /// <param name="isReserved">Texts a placeholder must never equal (the kept public names).</param>
    public Placeholders(Func<string, bool> isReserved) => _isReserved = isReserved;

    /// <summary>How many different texts were replaced.</summary>
    public int Count => _assigned.Count;

    /// <summary>The replaced texts, for the leak check.</summary>
    public IReadOnlyCollection<string> Originals => _assigned.Keys;

    /// <summary>The placeholder of <paramref name="text"/>.</summary>
    /// <param name="text">A text to hide.</param>
    public string For(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_assigned.TryGetValue(text, out var existing))
        {
            return existing;
        }

        if (!text.Any(char.IsLetterOrDigit))
        {
            // Only spaces and symbols: nothing to hide, and no letter to vary.
            _assigned[text] = text;
            return text;
        }

        for (var k = (long)_used.Count; ; k++)
        {
            var candidate = Encode(text, k);
            if (candidate is null)
            {
                throw new InvalidOperationException(
                    "Too many different texts of the same shape to keep their length."
                );
            }

            if (
                !string.Equals(candidate, text, StringComparison.Ordinal)
                && !_isReserved(candidate)
                && _used.Add(candidate)
            )
            {
                _assigned[text] = candidate;
                return candidate;
            }
        }
    }

    /// <summary>
    /// Writes <paramref name="k"/> in mixed radix over the letter (26) and digit (10) positions, from the right;
    /// <see langword="null"/> when it does not fit.
    /// </summary>
    private static string? Encode(string text, long k)
    {
        var chars = text.ToCharArray();
        for (var i = chars.Length - 1; i >= 0; i--)
        {
            var c = chars[i];
            if (char.IsLetter(c))
            {
                var letter = Letters[(int)(k % Letters.Length)];
                chars[i] = char.IsUpper(c) ? char.ToUpperInvariant(letter) : letter;
                k /= Letters.Length;
            }
            else if (char.IsDigit(c))
            {
                chars[i] = Digits[(int)(k % Digits.Length)];
                k /= Digits.Length;
            }
        }

        return k == 0 ? new string(chars) : null;
    }
}
