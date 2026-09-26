using System.Globalization;

namespace Clicalo.Application.Localization;

/// <summary>A language of the interface, as declared in <c>data/i18n/locales.json</c>.</summary>
public sealed class LocaleInfo
{
    /// <summary>Creates a language description.</summary>
    /// <param name="code">Language code, also used in the strings file name (<c>es</c>).</param>
    /// <param name="cultureName">.NET culture for dates (<c>es</c>).</param>
    /// <param name="nativeName">Name of the language in itself (<c>Español</c>), shown in the language picker.</param>
    /// <param name="shortName">Short label (<c>ES</c>).</param>
    /// <param name="decimalSeparator">Decimal separator (<c>,</c> in Spanish, <c>.</c> in English).</param>
    /// <param name="pluralRules">CLDR plural rules of the language.</param>
    public LocaleInfo(
        string code,
        string cultureName,
        string nativeName,
        string shortName,
        string decimalSeparator,
        PluralRules pluralRules
    )
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(cultureName);
        ArgumentException.ThrowIfNullOrWhiteSpace(nativeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(shortName);
        ArgumentException.ThrowIfNullOrEmpty(decimalSeparator);
        ArgumentNullException.ThrowIfNull(pluralRules);
        Code = code;
        CultureName = cultureName;
        NativeName = nativeName;
        ShortName = shortName;
        DecimalSeparator = decimalSeparator;
        PluralRules = pluralRules;
        Culture = CultureInfo.GetCultureInfo(cultureName);
    }

    /// <summary>Language code (<c>es</c>).</summary>
    public string Code { get; }

    /// <summary>.NET culture name (<c>es</c>).</summary>
    public string CultureName { get; }

    /// <summary>Culture for dates and other culture-sensitive formats.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Name of the language in itself (<c>Español</c>).</summary>
    public string NativeName { get; }

    /// <summary>Short label (<c>ES</c>).</summary>
    public string ShortName { get; }

    /// <summary>Decimal separator used for numbers in messages.</summary>
    public string DecimalSeparator { get; }

    /// <summary>CLDR plural rules.</summary>
    public PluralRules PluralRules { get; }

    /// <inheritdoc/>
    public override string ToString() => Code;
}
