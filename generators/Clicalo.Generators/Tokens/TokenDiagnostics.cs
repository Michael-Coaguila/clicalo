using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tokens;

/// <summary>Roslyn descriptors of the CLCT data errors. Every one is an error: broken design tokens break the build.</summary>
internal static class TokenDiagnostics
{
    private const string Category = "Clicalo.Tokens";
    private const string HelpLink =
        "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/design-tokens.md";

    private static readonly DiagnosticDescriptor InvalidColorDescriptor = new(
        TokenIds.InvalidColor,
        "Invalid color value",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct001"
    );

    private static readonly DiagnosticDescriptor ContrastTooLowDescriptor = new(
        TokenIds.ContrastTooLow,
        "Contrast below the required minimum",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct002"
    );

    private static readonly DiagnosticDescriptor OutOfGamutDescriptor = new(
        TokenIds.OutOfGamut,
        "Color too far outside the sRGB gamut",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct003"
    );

    private static readonly DiagnosticDescriptor MalformedFileDescriptor = new(
        TokenIds.MalformedFile,
        "Malformed design-token file",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct004"
    );

    private static readonly DiagnosticDescriptor UnknownTokenDescriptor = new(
        TokenIds.UnknownToken,
        "Unknown, duplicated or missing token",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct005"
    );

    private static readonly DiagnosticDescriptor StaleCorrectionDescriptor = new(
        TokenIds.StaleCorrection,
        "Stale contrast correction",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct006"
    );

    private static readonly DiagnosticDescriptor MissingFileDescriptor = new(
        TokenIds.MissingFile,
        "Design-token file missing",
        "{0}",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink + "#clct007"
    );

    private static readonly Dictionary<string, DiagnosticDescriptor> ById = new[]
    {
        InvalidColorDescriptor,
        ContrastTooLowDescriptor,
        OutOfGamutDescriptor,
        MalformedFileDescriptor,
        UnknownTokenDescriptor,
        StaleCorrectionDescriptor,
        MissingFileDescriptor,
    }.ToDictionary(static d => d.Id, StringComparer.Ordinal);

    /// <summary>All descriptors, for tests and documentation.</summary>
    public static IEnumerable<DiagnosticDescriptor> All => ById.Values;

    /// <summary>Descriptor of an issue id.</summary>
    public static DiagnosticDescriptor For(string id) => ById[id];
}
