using System.Collections.Generic;

namespace Clicalo.Generators.Tokens;

/// <summary>Everything the emitter needs, plus the data errors found while building it.</summary>
internal sealed class TokenModel
{
    public List<TokenIssue> Issues { get; } = [];

    /// <summary>False when structural errors prevent emitting code (contrast and gamut errors do not).</summary>
    public bool CanEmit { get; set; }

    /// <summary>Themes in the order of <c>theme-palettes.json</c>.</summary>
    public List<ThemeModel> Themes { get; } = [];

    /// <summary>Color token keys in declaration order: palette tokens first, then the extra ones.</summary>
    public List<string> ColorTokens { get; } = [];

    /// <summary>Category keys (CAT) in declaration order.</summary>
    public List<string> Categories { get; } = [];

    public List<KeyValuePair<string, double>> Radii { get; } = [];

    public List<ShadowModel> Shadows { get; } = [];

    public double FocusRingThickness { get; set; }

    public double FocusRingOffset { get; set; }

    public List<MotionModel> Motion { get; } = [];

    /// <summary>The WPF <c>SystemColors</c> color property (for example <c>WindowColor</c>) of every color token.</summary>
    public Dictionary<string, string> SystemColorByToken { get; } =
        new(System.StringComparer.Ordinal);

    public string SystemCategoryTint { get; set; } = string.Empty;

    public string SystemCategoryWash { get; set; } = string.Empty;
}
