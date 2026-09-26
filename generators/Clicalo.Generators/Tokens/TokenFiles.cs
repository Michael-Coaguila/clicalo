using System;

namespace Clicalo.Generators.Tokens;

/// <summary>The design-token files under <c>data/tokens</c> (blueprint §8.4).</summary>
internal static class TokenFiles
{
    public const string Directory = "data/tokens";

    /// <summary>Faithful copy of the design package's palettes (dark, light, high contrast).</summary>
    public const string ThemePalettes = "theme-palettes.json";

    /// <summary>Tokens the package lacks (TEM-002), category hues (TEM-003), corrections (TEM-004), shapes.</summary>
    public const string ExtraTokens = "extra-tokens.json";

    /// <summary>Text and graphic pairs with their WCAG minimum, measured on the real composite.</summary>
    public const string ContrastPairs = "contrast-pairs.json";

    /// <summary>Windows contrast-theme system colors that replace every token (TEM-001).</summary>
    public const string HighContrastSystemMap = "hc-system-map.json";

    /// <summary>Transition durations and their reduced-motion values (TEM-006).</summary>
    public const string Motion = "motion.json";

    public static readonly string[] All =
    [
        ThemePalettes,
        ExtraTokens,
        ContrastPairs,
        HighContrastSystemMap,
        Motion,
    ];

    public static bool IsRelevant(string fileName) =>
        Array.Exists(
            All,
            name => string.Equals(name, fileName, StringComparison.OrdinalIgnoreCase)
        );
}
