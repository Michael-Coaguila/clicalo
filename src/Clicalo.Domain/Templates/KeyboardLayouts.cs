using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Templates;

/// <summary>
/// The keyboard line of Plantillas (PLA-009): the four layouts and the two programs languages it offers, and which of
/// them Windows suggests ([detected]). Pure: the composition root reads the input language and the Windows display
/// language and passes their culture names.
/// </summary>
public static class KeyboardLayouts
{
    /// <summary>Español (Latinoamérica).</summary>
    public const string SpanishLatinAmerica = "es-LA";

    /// <summary>Español (España).</summary>
    public const string SpanishSpain = "es-ES";

    /// <summary>Inglés (EE. UU.).</summary>
    public const string EnglishUs = "en-US";

    /// <summary>Inglés internacional.</summary>
    public const string EnglishInternational = "en-INT";

    /// <summary>The layouts, in the order of the prototype.</summary>
    public static ValueList<string> All { get; } =
        [SpanishLatinAmerica, SpanishSpain, EnglishUs, EnglishInternational];

    /// <summary>The programs languages, in the order of the prototype.</summary>
    public static ValueList<LangCode> AppsLanguages { get; } = [LangCode.Es, LangCode.En];

    /// <summary>
    /// The layout of an input language: <c>es-ES</c> is Spain, any other Spanish is Latin America, and anything else is
    /// US English (the international variant cannot be told from the language alone).
    /// </summary>
    /// <param name="inputCulture">The culture name of the keyboard in use, such as <c>es-MX</c>.</param>
    public static string Detect(string? inputCulture)
    {
        var culture = inputCulture ?? string.Empty;
        return culture.Equals("es-ES", StringComparison.OrdinalIgnoreCase) ? SpanishSpain
            : culture.StartsWith("es", StringComparison.OrdinalIgnoreCase) ? SpanishLatinAmerica
            : EnglishUs;
    }

    /// <summary>The programs language of a Windows display language: Spanish for any Spanish, else English.</summary>
    /// <param name="displayCulture">The culture name of the Windows display language.</param>
    public static LangCode DetectAppsLanguage(string? displayCulture) =>
        (displayCulture ?? string.Empty).StartsWith("es", StringComparison.OrdinalIgnoreCase)
            ? LangCode.Es
            : LangCode.En;

    /// <summary>The layout of the settings, or the detected one while none was chosen (an empty layout).</summary>
    /// <param name="layout">The layout of the keyboard settings.</param>
    /// <param name="detected">The detected layout.</param>
    public static string Effective(string? layout, string detected) =>
        All.Items.Contains(layout ?? string.Empty, StringComparer.Ordinal) ? layout! : detected;
}
