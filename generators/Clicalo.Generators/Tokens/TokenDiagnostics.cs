using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Tokens;

/// <summary>Roslyn descriptors of the CLCT data errors. Every one is an error: broken design tokens break the build.</summary>
internal static class TokenDiagnostics
{
    private const string Category = "Clicalo.Tokens";
    private const string HelpLink =
        "https://github.com/Michael-Coaguila/clicalo/blob/main/docs/guides/design-tokens.md";

    private static readonly Dictionary<string, DiagnosticDescriptor> ById = new(
        StringComparer.Ordinal
    )
    {
        [TokenIds.InvalidColor] = Create(TokenIds.InvalidColor, "Invalid color value"),
        [TokenIds.ContrastTooLow] = Create(
            TokenIds.ContrastTooLow,
            "Contrast below the required minimum"
        ),
        [TokenIds.OutOfGamut] = Create(TokenIds.OutOfGamut, "Color too far outside the sRGB gamut"),
        [TokenIds.MalformedFile] = Create(TokenIds.MalformedFile, "Malformed design-token file"),
        [TokenIds.UnknownToken] = Create(
            TokenIds.UnknownToken,
            "Unknown, duplicated or missing token"
        ),
        [TokenIds.StaleCorrection] = Create(TokenIds.StaleCorrection, "Stale contrast correction"),
        [TokenIds.MissingFile] = Create(TokenIds.MissingFile, "Design-token file missing"),
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
