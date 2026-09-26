using System.Collections.Immutable;

namespace Clicalo.Generators.Localization;

/// <summary>A language declared in <c>data/i18n/locales.json</c>.</summary>
internal sealed class LocaleDefinition(
    string code,
    string culture,
    string decimalSeparator,
    ImmutableArray<string> pluralCategories,
    int line,
    int column
)
{
    /// <summary>Language code, also the middle part of its strings file name (<c>es</c>).</summary>
    public string Code { get; } = code;

    /// <summary>.NET culture name used for dates (<c>es-ES</c>).</summary>
    public string Culture { get; } = culture;

    /// <summary>Decimal separator for numbers (<c>,</c> in Spanish, <c>.</c> in English).</summary>
    public string DecimalSeparator { get; } = decimalSeparator;

    /// <summary>Plural categories the language uses, always including <c>other</c>, in canonical order.</summary>
    public ImmutableArray<string> PluralCategories { get; } = pluralCategories;

    /// <summary>Position of the locale entry in <c>locales.json</c>.</summary>
    public int Line { get; } = line;

    public int Column { get; } = column;
}
