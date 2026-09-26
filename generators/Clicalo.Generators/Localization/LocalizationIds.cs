namespace Clicalo.Generators.Localization;

/// <summary>
/// Stable identifiers of the i18n data errors (prefix CLCI). They are reported as compiler errors by
/// <see cref="LocalizationGenerator"/> and as console errors by <c>cl i18n-check</c>; see <c>docs/guides/i18n.md</c>.
/// </summary>
internal static class LocalizationIds
{
    /// <summary>A file of <c>data/i18n</c> is not valid JSON.</summary>
    public const string InvalidJson = "CLCI001";

    /// <summary>A key exists in one language and is missing in another.</summary>
    public const string MissingTranslation = "CLCI002";

    /// <summary>The same key uses different placeholders in two languages.</summary>
    public const string PlaceholderMismatch = "CLCI003";

    /// <summary>A text uses a placeholder that <c>placeholders.json</c> does not declare.</summary>
    public const string UnknownPlaceholder = "CLCI004";

    /// <summary>A text is empty or only whitespace.</summary>
    public const string EmptyText = "CLCI005";

    /// <summary>A key has characters outside <c>[A-Za-z0-9]</c> or an unknown plural suffix.</summary>
    public const string InvalidKey = "CLCI006";

    /// <summary>Two keys produce the same C# member name, or a key produces a reserved name.</summary>
    public const string MemberNameCollision = "CLCI007";

    /// <summary>A brace that does not open or close a valid <c>{name}</c> placeholder.</summary>
    public const string MalformedPlaceholder = "CLCI008";

    /// <summary>A plural family lacks <c>_other</c> or a category required by the language.</summary>
    public const string PluralFormMissing = "CLCI009";

    /// <summary>A plural family has a category that the language's plural rules do not use.</summary>
    public const string PluralFormUnexpected = "CLCI010";

    /// <summary>A plural family does not use a numeric <c>{count}</c>, the plural selector.</summary>
    public const string PluralWithoutCount = "CLCI011";

    /// <summary>A key is plural in one language and plain in another, or both in the same language.</summary>
    public const string PluralShapeMismatch = "CLCI012";

    /// <summary><c>locales.json</c> is missing or invalid, or does not match the strings files.</summary>
    public const string InvalidLocales = "CLCI013";

    /// <summary><c>placeholders.json</c> is missing or invalid.</summary>
    public const string InvalidPlaceholderCatalog = "CLCI014";

    /// <summary>A strings file is not a flat object of string values.</summary>
    public const string InvalidStringsFile = "CLCI015";
}
