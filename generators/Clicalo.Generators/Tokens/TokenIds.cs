namespace Clicalo.Generators.Tokens;

/// <summary>Diagnostic ids of the design-token generator (prefix CLCT). Documented in docs/guides/design-tokens.md.</summary>
internal static class TokenIds
{
    /// <summary>A color value is not valid CSS of the supported subset, or is out of range.</summary>
    public const string InvalidColor = "CLCT001";

    /// <summary>A text or graphic pair is below its WCAG minimum on the composited background.</summary>
    public const string ContrastTooLow = "CLCT002";

    /// <summary>An OKLCH color is outside sRGB and gamut mapping moves it more than the allowed ΔEOK.</summary>
    public const string OutOfGamut = "CLCT003";

    /// <summary>A token file is not valid JSON or does not have the expected shape.</summary>
    public const string MalformedFile = "CLCT004";

    /// <summary>A token or theme is referenced but not defined, defined twice, or missing in some theme.</summary>
    public const string UnknownToken = "CLCT005";

    /// <summary>A documented correction no longer matches the value it corrects.</summary>
    public const string StaleCorrection = "CLCT006";

    /// <summary>A required token file is not among the additional files of the project.</summary>
    public const string MissingFile = "CLCT007";
}
