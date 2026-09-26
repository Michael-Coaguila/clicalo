using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Localization;

/// <summary>Roslyn descriptors of the CLCI data errors. Every one is an error: broken i18n data breaks the build.</summary>
internal static class LocalizationDiagnostics
{
    private const string Category = "Clicalo.Localization";
    private const string HelpLink =
        "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/i18n.md";

    private static readonly Dictionary<string, DiagnosticDescriptor> ById = new(
        System.StringComparer.Ordinal
    )
    {
        [LocalizationIds.InvalidJson] = Create(
            LocalizationIds.InvalidJson,
            "Invalid JSON in data/i18n"
        ),
        [LocalizationIds.MissingTranslation] = Create(
            LocalizationIds.MissingTranslation,
            "Key missing in a language"
        ),
        [LocalizationIds.PlaceholderMismatch] = Create(
            LocalizationIds.PlaceholderMismatch,
            "Different placeholders between languages"
        ),
        [LocalizationIds.UnknownPlaceholder] = Create(
            LocalizationIds.UnknownPlaceholder,
            "Unknown placeholder"
        ),
        [LocalizationIds.EmptyText] = Create(LocalizationIds.EmptyText, "Empty text"),
        [LocalizationIds.InvalidKey] = Create(LocalizationIds.InvalidKey, "Invalid key"),
        [LocalizationIds.MemberNameCollision] = Create(
            LocalizationIds.MemberNameCollision,
            "Generated member name collision"
        ),
        [LocalizationIds.MalformedPlaceholder] = Create(
            LocalizationIds.MalformedPlaceholder,
            "Malformed placeholder"
        ),
        [LocalizationIds.PluralFormMissing] = Create(
            LocalizationIds.PluralFormMissing,
            "Plural form missing"
        ),
        [LocalizationIds.PluralFormUnexpected] = Create(
            LocalizationIds.PluralFormUnexpected,
            "Plural form not used by the language"
        ),
        [LocalizationIds.PluralWithoutCount] = Create(
            LocalizationIds.PluralWithoutCount,
            "Plural family without {count}"
        ),
        [LocalizationIds.PluralShapeMismatch] = Create(
            LocalizationIds.PluralShapeMismatch,
            "Plural and plain text mixed"
        ),
        [LocalizationIds.InvalidLocales] = Create(
            LocalizationIds.InvalidLocales,
            "Invalid locales.json"
        ),
        [LocalizationIds.InvalidPlaceholderCatalog] = Create(
            LocalizationIds.InvalidPlaceholderCatalog,
            "Invalid placeholders.json"
        ),
        [LocalizationIds.InvalidStringsFile] = Create(
            LocalizationIds.InvalidStringsFile,
            "Invalid strings file"
        ),
    };

    /// <summary>All descriptors, for tests and documentation.</summary>
    public static IEnumerable<DiagnosticDescriptor> All => ById.Values;

    /// <summary>Descriptor of an issue id.</summary>
    public static DiagnosticDescriptor For(string id) => ById[id];

    private static DiagnosticDescriptor Create(string id, string title) =>
        new(
            id,
            title,
            "{0}",
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            helpLinkUri: HelpLink + "#" + id.ToLowerInvariant()
        );
}
