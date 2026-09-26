using System.Collections.Immutable;

namespace Clicalo.Domain.Primitives;

/// <summary>
/// A user text in several languages (a profile or shortcut name). Keyed by <see cref="LangCode"/> so it admits N
/// languages (IDI-006); new names get the same text in every language (docs/02). Immutable, with value equality.
/// </summary>
public sealed class LocalizedText : IEquatable<LocalizedText>
{
    private static readonly IComparer<LangCode> Order = Comparer<LangCode>.Create(
        static (left, right) => string.CompareOrdinal(left.Value, right.Value)
    );

    /// <summary>Creates a text from its values.</summary>
    /// <param name="values">One text per language; a repeated language keeps the last value.</param>
    public LocalizedText(IEnumerable<KeyValuePair<LangCode, string>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var builder = ImmutableSortedDictionary.CreateBuilder<LangCode, string>(
            Order,
            StringComparer.Ordinal
        );
        foreach (var pair in values)
        {
            builder[pair.Key] = pair.Value ?? string.Empty;
        }

        Values = builder.ToImmutable();
    }

    /// <summary>The value of each language, sorted by language tag.</summary>
    public ImmutableSortedDictionary<LangCode, string> Values { get; }

    /// <summary>The same text in every language of <paramref name="languages"/>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="languages">The languages that receive it.</param>
    public static LocalizedText Same(string text, params ReadOnlySpan<LangCode> languages)
    {
        var pairs = new List<KeyValuePair<LangCode, string>>(languages.Length);
        foreach (var language in languages)
        {
            pairs.Add(new(language, text));
        }

        return new LocalizedText(pairs);
    }

    /// <summary>
    /// The value in <paramref name="language"/>, or else in <paramref name="fallback"/>, or else the first one, or an
    /// empty string.
    /// </summary>
    /// <param name="language">Preferred language.</param>
    /// <param name="fallback">Language to use when the preferred one is missing.</param>
    public string Get(LangCode language, LangCode fallback)
    {
        if (Values.TryGetValue(language, out var value))
        {
            return value;
        }

        if (Values.TryGetValue(fallback, out var other))
        {
            return other;
        }

        foreach (var pair in Values)
        {
            return pair.Value;
        }

        return string.Empty;
    }

    /// <inheritdoc />
    public bool Equals(LocalizedText? other)
    {
        if (other is null || Values.Count != other.Values.Count)
        {
            return false;
        }

        foreach (var pair in Values)
        {
            if (
                !other.Values.TryGetValue(pair.Key, out var value)
                || !string.Equals(pair.Value, value, StringComparison.Ordinal)
            )
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as LocalizedText);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var pair in Values)
        {
            hash.Add(pair.Key);
            hash.Add(pair.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
