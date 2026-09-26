using System.Collections.Frozen;
using System.Collections.Immutable;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Localization;

/// <summary>
/// The parsed texts of one language. Infrastructure reads the JSON files and calls <see cref="Create"/>; invalid
/// entries are skipped and listed in <see cref="Problems"/> instead of failing, because the build already rejects
/// broken Spanish and English data (CLCI errors) and a broken third-language file must not break the interface.
/// </summary>
public sealed class LanguagePack
{
    private readonly FrozenDictionary<string, LanguagePackEntry> _entries;

    private LanguagePack(
        LocaleInfo locale,
        FrozenDictionary<string, LanguagePackEntry> entries,
        ImmutableArray<LanguagePackProblem> problems
    )
    {
        Locale = locale;
        _entries = entries;
        Problems = problems;
    }

    /// <summary>The language.</summary>
    public LocaleInfo Locale { get; }

    /// <summary>Entries that were skipped, with the reason.</summary>
    public ImmutableArray<LanguagePackProblem> Problems { get; }

    /// <summary>Number of usable keys (a plural family counts once).</summary>
    public int Count => _entries.Count;

    /// <summary>Builds a pack from the physical entries of a strings file (<c>comboN_one</c>, <c>comboN_other</c>…).</summary>
    public static LanguagePack Create(
        LocaleInfo locale,
        IEnumerable<KeyValuePair<string, string>> entries
    )
    {
        ArgumentNullException.ThrowIfNull(locale);
        ArgumentNullException.ThrowIfNull(entries);
        var map = new Dictionary<string, LanguagePackEntry>(StringComparer.Ordinal);
        var problems = ImmutableArray.CreateBuilder<LanguagePackProblem>();
        foreach (var (key, text) in entries)
        {
            var problem = Add(locale, map, key, text);
            if (problem is not null)
            {
                problems.Add(new LanguagePackProblem(key, problem));
            }
        }

        foreach (
            var incomplete in map.Where(static e => !e.Value.IsComplete)
                .Select(static e => e.Key)
                .ToList()
        )
        {
            map.Remove(incomplete);
            problems.Add(
                new LanguagePackProblem(
                    incomplete + "_other",
                    "The plural family has no 'other' form."
                )
            );
        }

        return new LanguagePack(
            locale,
            map.ToFrozenDictionary(StringComparer.Ordinal),
            problems.ToImmutable()
        );
    }

    /// <summary>True when the pack has a usable text for <paramref name="key"/>.</summary>
    public bool Contains(MessageKey key) =>
        key.Value is not null && _entries.ContainsKey(key.Value);

    internal bool TryGetEntry(MessageKey key, out LanguagePackEntry entry) =>
        _entries.TryGetValue(key.Value ?? string.Empty, out entry!);

    private static string? Add(
        LocaleInfo locale,
        Dictionary<string, LanguagePackEntry> map,
        string key,
        string text
    )
    {
        if (!TrySplitKey(key, out var baseKey, out var category))
        {
            return "Not a valid key (letters and digits, optionally with a plural suffix such as _one).";
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return "The text is empty.";
        }

        if (!MessageTemplate.TryParse(text, out var template, out var error))
        {
            return "Malformed placeholder: " + error + ".";
        }

        if (category is { } form && !locale.PluralRules.Categories.Contains(form))
        {
            return "The plural rules of '" + locale.Code + "' do not use this category.";
        }

        if (!map.TryGetValue(baseKey, out var entry))
        {
            entry = new LanguagePackEntry();
            map.Add(baseKey, entry);
        }

        var added = category is { } c ? entry.TrySetForm(c, template) : entry.TrySetPlain(template);
        return added ? null : "The key mixes a plain text and plural forms, or repeats a form.";
    }

    private static bool TrySplitKey(string key, out string baseKey, out PluralCategory? category)
    {
        category = null;
        var separator = key.IndexOf('_', StringComparison.Ordinal);
        baseKey = separator < 0 ? key : key[..separator];
        if (
            baseKey.Length == 0
            || !char.IsAsciiLetter(baseKey[0])
            || !baseKey.All(char.IsAsciiLetterOrDigit)
        )
        {
            return false;
        }

        if (separator < 0)
        {
            return true;
        }

        category = key[(separator + 1)..] switch
        {
            "zero" => PluralCategory.Zero,
            "one" => PluralCategory.One,
            "two" => PluralCategory.Two,
            "few" => PluralCategory.Few,
            "many" => PluralCategory.Many,
            "other" => PluralCategory.Other,
            _ => null,
        };
        return category is not null;
    }
}
