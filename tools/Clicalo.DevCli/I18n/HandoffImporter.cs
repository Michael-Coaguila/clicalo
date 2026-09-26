using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Clicalo.Generators.Localization;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// Reproducible conversion of the handoff strings (<c>docs/design/handoff/data/strings.*.json</c>) into
/// <c>data/i18n</c>, driven by the reviewed recipe <c>data/i18n/handoff-import.json</c> (blueprint §8.5):
/// <list type="number">
/// <item>every one-letter marker becomes a named placeholder with the recipe map, with per-key overrides; a marker
/// the recipe does not map is an error, so no unknown marker can slip through;</item>
/// <item>keys with extra plural forms become families <c>key_one</c> … <c>key_other</c>, where <c>_other</c> is the
/// converted original text, so the visible text for the sample arguments stays identical;</item>
/// <item>keys with a replacement text and composed arguments use nested plural messages added by the recipe.</item>
/// </list>
/// Original key names are kept for traceability with the requirements catalog.
/// </summary>
internal static partial class HandoffImporter
{
    public static HandoffImport Run(
        HandoffRecipe recipe,
        IReadOnlyDictionary<string, IReadOnlyList<KeyValuePair<string, string>>> handoff
    )
    {
        var import = new HandoffImport();
        var texts = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        foreach (var language in recipe.Languages)
        {
            if (!handoff.TryGetValue(language, out var entries))
            {
                import.Errors.Add("The handoff has no strings file for '" + language + "'.");
                continue;
            }

            texts[language] = entries.ToDictionary(
                static e => e.Key,
                static e => e.Value,
                StringComparer.Ordinal
            );
            import.Entries[language] = [];
        }

        if (import.Errors.Count > 0)
        {
            return import;
        }

        var order = handoff[recipe.Languages[0]].Select(static e => e.Key).ToList();
        import.HandoffKeyCount = order.Count;
        CheckParity(recipe, texts, order, import.Errors);
        CheckRecipe(recipe, texts[recipe.Languages[0]], import.Errors);
        if (import.Errors.Count > 0)
        {
            return import;
        }

        var emittedAdded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in order)
        {
            ConvertKey(recipe, key, texts, import);
            EmitAddedAfter(recipe, key, import, emittedAdded);
        }

        foreach (var orphan in recipe.Added.Where(a => !emittedAdded.Contains(a.Key)))
        {
            import.Errors.Add(
                "added."
                    + orphan.Key
                    + ".after ('"
                    + orphan.After
                    + "') is not a key of the output."
            );
        }

        return import;
    }

    [GeneratedRegex(
        @"\{(?<marker>[^{}]*)\}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex Marker();

    private static void CheckParity(
        HandoffRecipe recipe,
        Dictionary<string, Dictionary<string, string>> texts,
        List<string> order,
        List<string> errors
    )
    {
        var reference = texts[recipe.Languages[0]];
        foreach (var language in recipe.Languages.Skip(1))
        {
            foreach (var key in order.Where(k => !texts[language].ContainsKey(k)))
            {
                errors.Add("The handoff key '" + key + "' is missing in '" + language + "'.");
            }

            foreach (var key in texts[language].Keys.Where(k => !reference.ContainsKey(k)))
            {
                errors.Add("The handoff key '" + key + "' exists only in '" + language + "'.");
            }
        }
    }

    private static void CheckRecipe(
        HandoffRecipe recipe,
        Dictionary<string, string> reference,
        List<string> errors
    )
    {
        var added = recipe.Added.Select(static a => a.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var (key, rule) in recipe.Keys.OrderBy(static k => k.Key, StringComparer.Ordinal))
        {
            if (!reference.ContainsKey(key))
            {
                errors.Add("keys." + key + " is not a key of the handoff.");
                continue;
            }

            if (!rule.Text.IsEmpty && !rule.Plural.IsEmpty)
            {
                errors.Add(
                    "keys."
                        + key
                        + " cannot have both 'text' and 'plural'; add plural keys with 'added'."
                );
            }

            if (!rule.Arguments.IsEmpty && rule.Text.IsEmpty)
            {
                errors.Add(
                    "keys."
                        + key
                        + ".arguments needs a reviewed 'text' that uses those placeholders."
                );
            }

            foreach (
                var name in rule
                    .Arguments.Where(a => !added.Contains(a.Value.Message))
                    .Select(static a => a.Key)
                    .Order(StringComparer.Ordinal)
            )
            {
                errors.Add(
                    "keys." + key + ".arguments." + name + ".message must be a key of 'added'."
                );
            }

            CheckLanguages(
                recipe,
                rule.Text.Keys,
                "keys." + key + ".text",
                !rule.Text.IsEmpty,
                errors
            );
            CheckLanguages(
                recipe,
                rule.Plural.Keys,
                "keys." + key + ".plural",
                !rule.Plural.IsEmpty,
                errors
            );
            foreach (
                var (language, forms) in rule.Plural.OrderBy(
                    static p => p.Key,
                    StringComparer.Ordinal
                )
            )
            {
                CheckCategories(
                    forms,
                    "keys." + key + ".plural." + language,
                    requireOther: false,
                    errors
                );
            }
        }

        foreach (var entry in recipe.Added)
        {
            if (reference.ContainsKey(entry.Key))
            {
                errors.Add("added." + entry.Key + " already exists in the handoff.");
            }

            if (!entry.Text.IsEmpty)
            {
                CheckLanguages(
                    recipe,
                    entry.Text.Keys,
                    "added." + entry.Key + ".text",
                    true,
                    errors
                );
                continue;
            }

            CheckLanguages(
                recipe,
                entry.Plural.Keys,
                "added." + entry.Key + ".plural",
                true,
                errors
            );
            foreach (
                var (language, forms) in entry.Plural.OrderBy(
                    static p => p.Key,
                    StringComparer.Ordinal
                )
            )
            {
                CheckCategories(
                    forms,
                    "added." + entry.Key + ".plural." + language,
                    requireOther: true,
                    errors
                );
            }
        }
    }

    private static void CheckLanguages(
        HandoffRecipe recipe,
        IEnumerable<string> given,
        string owner,
        bool required,
        List<string> errors
    )
    {
        if (!required)
        {
            return;
        }

        var set = given.ToHashSet(StringComparer.Ordinal);
        foreach (var language in recipe.Languages.Where(l => !set.Contains(l)))
        {
            errors.Add(owner + " lacks the language '" + language + "'.");
        }

        foreach (
            var extra in set.Where(l => !recipe.Languages.Contains(l, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal)
        )
        {
            errors.Add(owner + " has the undeclared language '" + extra + "'.");
        }
    }

    private static void CheckCategories(
        ImmutableDictionary<string, string> forms,
        string owner,
        bool requireOther,
        List<string> errors
    )
    {
        foreach (
            var category in forms
                .Keys.Where(static c => !PluralCategories.IsCategory(c))
                .Order(StringComparer.Ordinal)
        )
        {
            errors.Add(owner + "." + category + " is not a CLDR plural category.");
        }

        var hasOther = forms.ContainsKey(PluralCategories.Other);
        if (requireOther && !hasOther)
        {
            errors.Add(owner + " lacks the 'other' form.");
        }
        else if (!requireOther && hasOther)
        {
            errors.Add(owner + ".other is the converted handoff text; do not repeat it.");
        }
    }

    private static void ConvertKey(
        HandoffRecipe recipe,
        string key,
        Dictionary<string, Dictionary<string, string>> texts,
        HandoffImport import
    )
    {
        var rule = recipe.Keys.GetValueOrDefault(key) ?? new HandoffKeyRule();
        var map = recipe.Placeholders.SetItems(rule.Placeholders);
        var composed = rule
            .Arguments.Values.Select(static a => a.CountMarker)
            .ToHashSet(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var language in recipe.Languages)
        {
            foreach (Match match in Marker().Matches(texts[language][key]))
            {
                var marker = match.Groups["marker"].Value;
                used.Add(marker);
                if (!map.ContainsKey(marker) && !composed.Contains(marker))
                {
                    import.Errors.Add(
                        "Unknown marker {"
                            + marker
                            + "} in '"
                            + key
                            + "' ("
                            + language
                            + "); map it in 'placeholders' of "
                            + I18nPaths.RecipeFileName
                            + "."
                    );
                }
            }
        }

        foreach (
            var letter in rule
                .Placeholders.Keys.Concat(composed)
                .Where(l => !used.Contains(l))
                .Order(StringComparer.Ordinal)
        )
        {
            import.Errors.Add(
                "keys." + key + " maps {" + letter + "}, which the handoff text does not use."
            );
        }

        foreach (var language in recipe.Languages)
        {
            var text = rule.Text.IsEmpty
                ? Marker()
                    .Replace(
                        texts[language][key],
                        m =>
                            map.TryGetValue(m.Groups["marker"].Value, out var name)
                                ? "{" + name + "}"
                                : m.Value
                    )
                : rule.Text[language];
            var entries = import.Entries[language];
            if (rule.Plural.IsEmpty)
            {
                entries.Add(new(key, text));
                continue;
            }

            foreach (
                var (category, form) in rule.Plural[language]
                    .OrderBy(static f => PluralCategories.OrderOf(f.Key))
            )
            {
                entries.Add(new(key + "_" + category, form));
            }

            entries.Add(new(key + "_" + PluralCategories.Other, text));
        }
    }

    private static void EmitAddedAfter(
        HandoffRecipe recipe,
        string after,
        HandoffImport import,
        HashSet<string> emitted
    )
    {
        foreach (
            var entry in recipe.Added.Where(a =>
                string.Equals(a.After, after, StringComparison.Ordinal)
            )
        )
        {
            if (!emitted.Add(entry.Key))
            {
                continue;
            }

            foreach (var language in recipe.Languages)
            {
                if (!entry.Text.IsEmpty)
                {
                    import.Entries[language].Add(new(entry.Key, entry.Text[language]));
                    continue;
                }

                foreach (
                    var (category, form) in entry
                        .Plural[language]
                        .OrderBy(static f => PluralCategories.OrderOf(f.Key))
                )
                {
                    import.Entries[language].Add(new(entry.Key + "_" + category, form));
                }
            }

            EmitAddedAfter(recipe, entry.Key, import, emitted);
        }
    }
}
