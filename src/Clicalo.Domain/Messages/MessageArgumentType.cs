namespace Clicalo.Domain.Messages;

/// <summary>Declared type of a placeholder (<c>data/i18n/placeholders.json</c>).</summary>
public enum MessageArgumentType
{
    /// <summary>Verbatim text or a nested localized message.</summary>
    Text,

    /// <summary>Whole number; can select a plural form.</summary>
    WholeNumber,

    /// <summary>Decimal number written with the language's decimal separator; can select a plural form.</summary>
    DecimalNumber,
}
