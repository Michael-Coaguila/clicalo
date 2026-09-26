using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Localization;

/// <summary>Roslyn descriptors of the CLCI data errors. Every one is an error: broken i18n data breaks the build.</summary>
internal static class LocalizationDiagnostics
{
    private const string Category = "Clicalo.Localization";
    private const string HelpLink =
        "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/i18n.md";

    private static readonly DiagnosticDescriptor InvalidJsonDescriptor = new(
        LocalizationIds.InvalidJson,
        "Invalid JSON in data/i18n",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci001"
    );

    private static readonly DiagnosticDescriptor MissingTranslationDescriptor = new(
        LocalizationIds.MissingTranslation,
        "Key missing in a language",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci002"
    );

    private static readonly DiagnosticDescriptor PlaceholderMismatchDescriptor = new(
        LocalizationIds.PlaceholderMismatch,
        "Different placeholders between languages",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci003"
    );

    private static readonly DiagnosticDescriptor UnknownPlaceholderDescriptor = new(
        LocalizationIds.UnknownPlaceholder,
        "Unknown placeholder",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci004"
    );

    private static readonly DiagnosticDescriptor EmptyTextDescriptor = new(
        LocalizationIds.EmptyText,
        "Empty text",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci005"
    );

    private static readonly DiagnosticDescriptor InvalidKeyDescriptor = new(
        LocalizationIds.InvalidKey,
        "Invalid key",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci006"
    );

    private static readonly DiagnosticDescriptor MemberNameCollisionDescriptor = new(
        LocalizationIds.MemberNameCollision,
        "Generated member name collision",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci007"
    );

    private static readonly DiagnosticDescriptor MalformedPlaceholderDescriptor = new(
        LocalizationIds.MalformedPlaceholder,
        "Malformed placeholder",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci008"
    );

    private static readonly DiagnosticDescriptor PluralFormMissingDescriptor = new(
        LocalizationIds.PluralFormMissing,
        "Plural form missing",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci009"
    );

    private static readonly DiagnosticDescriptor PluralFormUnexpectedDescriptor = new(
        LocalizationIds.PluralFormUnexpected,
        "Plural form not used by the language",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci010"
    );

    private static readonly DiagnosticDescriptor PluralWithoutCountDescriptor = new(
        LocalizationIds.PluralWithoutCount,
        "Plural family without {count}",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci011"
    );

    private static readonly DiagnosticDescriptor PluralShapeMismatchDescriptor = new(
        LocalizationIds.PluralShapeMismatch,
        "Plural and plain text mixed",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci012"
    );

    private static readonly DiagnosticDescriptor InvalidLocalesDescriptor = new(
        LocalizationIds.InvalidLocales,
        "Invalid locales.json",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci013"
    );

    private static readonly DiagnosticDescriptor InvalidPlaceholderCatalogDescriptor = new(
        LocalizationIds.InvalidPlaceholderCatalog,
        "Invalid placeholders.json",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci014"
    );

    private static readonly DiagnosticDescriptor InvalidStringsFileDescriptor = new(
        LocalizationIds.InvalidStringsFile,
        "Invalid strings file",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clci015"
    );

    private static readonly Dictionary<string, DiagnosticDescriptor> ById = new[]
    {
        InvalidJsonDescriptor,
        MissingTranslationDescriptor,
        PlaceholderMismatchDescriptor,
        UnknownPlaceholderDescriptor,
        EmptyTextDescriptor,
        InvalidKeyDescriptor,
        MemberNameCollisionDescriptor,
        MalformedPlaceholderDescriptor,
        PluralFormMissingDescriptor,
        PluralFormUnexpectedDescriptor,
        PluralWithoutCountDescriptor,
        PluralShapeMismatchDescriptor,
        InvalidLocalesDescriptor,
        InvalidPlaceholderCatalogDescriptor,
        InvalidStringsFileDescriptor,
    }.ToDictionary(static d => d.Id, StringComparer.Ordinal);

    /// <summary>All descriptors, for tests and documentation.</summary>
    public static IEnumerable<DiagnosticDescriptor> All => ById.Values;

    /// <summary>Descriptor of an issue id.</summary>
    public static DiagnosticDescriptor For(string id) => ById[id];
}
