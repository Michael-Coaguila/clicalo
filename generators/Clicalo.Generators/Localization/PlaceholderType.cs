namespace Clicalo.Generators.Localization;

/// <summary>Type of a named placeholder, as declared in <c>data/i18n/placeholders.json</c>.</summary>
internal enum PlaceholderType
{
    /// <summary>Verbatim text or a nested localized message (<c>MessageText</c>).</summary>
    Text,

    /// <summary>Whole number (<c>long</c>); can select a plural form.</summary>
    Integer,

    /// <summary>Decimal number (<c>decimal</c>), written with the language's decimal separator; can select a plural form.</summary>
    Number,
}
