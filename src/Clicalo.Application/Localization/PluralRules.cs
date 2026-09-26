using System.Collections.Immutable;

namespace Clicalo.Application.Localization;

/// <summary>
/// The plural rules of a language, from <c>locales.json</c>: one CLDR condition per category, tried in canonical
/// order; <see cref="PluralCategory.Other"/> applies when none matches. Adding a language needs no code (IDI-006).
/// </summary>
public sealed class PluralRules
{
    private readonly ImmutableArray<(PluralCategory Category, PluralRule Rule)> _rules;

    private PluralRules(ImmutableArray<(PluralCategory Category, PluralRule Rule)> rules)
    {
        _rules = rules;
        Categories = [.. rules.Select(static r => r.Category), PluralCategory.Other];
    }

    /// <summary>Categories the language uses, in canonical order, always ending with <see cref="PluralCategory.Other"/>.</summary>
    public ImmutableArray<PluralCategory> Categories { get; }

    /// <summary>Parses the rules of a language.</summary>
    /// <param name="rules">Condition per category; <see cref="PluralCategory.Other"/> must not be given.</param>
    /// <exception cref="FormatException">A condition is not valid.</exception>
    /// <exception cref="ArgumentException">A rule is given for <see cref="PluralCategory.Other"/>.</exception>
    public static PluralRules Create(IEnumerable<KeyValuePair<PluralCategory, string>> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var parsed = new List<(PluralCategory Category, PluralRule Rule)>();
        foreach (var (category, condition) in rules)
        {
            if (category == PluralCategory.Other)
            {
                throw new ArgumentException(
                    "'other' is implicit and takes no rule.",
                    nameof(rules)
                );
            }

            if (parsed.Exists(r => r.Category == category))
            {
                throw new ArgumentException(
                    "The category '" + category + "' has two rules.",
                    nameof(rules)
                );
            }

            parsed.Add((category, PluralRule.Parse(condition)));
        }

        return new PluralRules([.. parsed.OrderBy(static r => r.Category)]);
    }

    /// <summary>The category of a number.</summary>
    public PluralCategory Select(PluralOperands operands)
    {
        foreach (var (category, rule) in _rules)
        {
            if (rule.Matches(operands))
            {
                return category;
            }
        }

        return PluralCategory.Other;
    }
}
