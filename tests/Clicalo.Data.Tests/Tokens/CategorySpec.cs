using System.Text.Json;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>The <c>categories</c> section of <c>extra-tokens.json</c> (TEM-003).</summary>
internal sealed record CategorySpec(
    IReadOnlyDictionary<string, double> Hues,
    double TintChroma,
    double WashLightness,
    double WashChroma,
    string HighContrastTint,
    string HighContrastWash
)
{
    public static CategorySpec Read(JsonElement categories)
    {
        var hues = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var hue in TokenDataSet.DataMembers(categories.GetProperty("hues")))
        {
            hues[hue.Name] = hue.Value.GetDouble();
        }

        var highContrast = categories.GetProperty("highContrast");
        return new CategorySpec(
            hues,
            categories.GetProperty("tintChroma").GetDouble(),
            categories.GetProperty("washLightness").GetDouble(),
            categories.GetProperty("washChroma").GetDouble(),
            highContrast.GetProperty("tint").GetString()!,
            highContrast.GetProperty("wash").GetString()!
        );
    }
}
