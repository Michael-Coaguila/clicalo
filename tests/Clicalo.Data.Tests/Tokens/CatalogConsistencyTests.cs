using System.Text.Json.Nodes;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// data/tokens and data/catalogs describe the same product from two sides; these checks keep the values they share
/// from drifting apart («one threshold, one name», NFR-020).
/// </summary>
public sealed class CatalogConsistencyTests
{
    [Fact]
    [Trait("Req", "TEM-003")]
    public void Every_shortcut_category_has_exactly_one_hue()
    {
        var categories = Read("catalogs", "categories.json")["categories"]!
            .AsArray()
            .Select(category => (string)category!["id"]!)
            .Order(StringComparer.Ordinal);
        var hues = Read("tokens", "extra-tokens.json")["categories"]!["hues"]!
            .AsObject()
            .Select(hue => hue.Key)
            .Order(StringComparer.Ordinal);

        hues.ShouldBe(categories);
    }

    [Fact]
    [Trait("Req", "NFR-020")]
    [Trait("Req", "TEM-006")]
    public void The_panel_opacity_animation_lasts_as_long_as_the_dim_transition()
    {
        var dimTransition = (string)
            Read("catalogs", "timings.json")["groups"]!["Dimming"]!["entries"]!["DimTransition"]![
                "duration"
            ]!;
        var panelOpacity = (int)Read("tokens", "motion.json")["durations"]!["panelOpacity"]!["ms"]!;

        dimTransition.ShouldBe(panelOpacity + "ms");
    }

    private static JsonNode Read(string folder, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(RepoPaths.Data, folder, file)))!;
}
