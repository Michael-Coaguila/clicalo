using System.Collections.Immutable;

namespace Clicalo.Generators.Localization;

/// <summary>CLDR plural categories, used as key suffixes (<c>comboN_one</c>, <c>comboN_other</c>) as in i18next v4.</summary>
internal static class PluralCategories
{
    /// <summary>The category every language has; its form is mandatory in every plural family.</summary>
    public const string Other = "other";

    /// <summary>The selector argument of every plural family, as in i18next.</summary>
    public const string SelectorArgument = "count";

    /// <summary>All CLDR categories in canonical order.</summary>
    public static readonly ImmutableArray<string> All =
    [
        "zero",
        "one",
        "two",
        "few",
        "many",
        Other,
    ];

    /// <summary>True for a CLDR category name.</summary>
    public static bool IsCategory(string value) => All.IndexOf(value) >= 0;

    /// <summary>Canonical position of a category, used to order forms deterministically.</summary>
    public static int OrderOf(string category) => All.IndexOf(category);
}
