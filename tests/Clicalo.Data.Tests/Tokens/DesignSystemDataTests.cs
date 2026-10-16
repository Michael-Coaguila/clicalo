using System.Text.Json;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// The measures of direction C (D12) in <c>data/tokens/design-system.json</c>: the closed scales of the mockup, the
/// touch target of every control height (REG-02) and the smallest text (TEM-007).
/// </summary>
public sealed class DesignSystemDataTests
{
    [Fact]
    [Trait("Req", "TEM-002")]
    public void The_scales_are_exactly_the_ones_of_direction_C()
    {
        using var system = TokenDataSet.Read("design-system.json");
        var root = system.RootElement;

        Lengths(root, "space").ShouldBe([4, 8, 12, 16, 24, 32, 48]);
        Lengths(root, "controlHeights").ShouldBe([44, 52, 64]);
        Lengths(root, "radii").ShouldBe([8, 12, 16, 999]);
        Lengths(root, "iconBoxes").ShouldBe([16, 20, 24, 32]);
        Lengths(root, "strokes").ShouldBe([1, 2, 3, 2]);
    }

    [Fact]
    [Trait("Req", "TEM-002")]
    public void Spacing_and_control_heights_stay_on_the_grid_of_4()
    {
        using var system = TokenDataSet.Read("design-system.json");
        var root = system.RootElement;

        Lengths(root, "space").Concat(Lengths(root, "controlHeights")).ShouldAllBe(v => v % 4 == 0);
    }

    [Fact]
    [Trait("Req", "REG-02")]
    public void Every_control_height_is_a_touch_target()
    {
        using var system = TokenDataSet.Read("design-system.json");

        Lengths(system.RootElement, "controlHeights").ShouldAllBe(height => height >= 44);
    }

    [Fact]
    [Trait("Req", "TEM-007")]
    [Trait("Req", "TEM-005")]
    public void Text_styles_never_go_below_11_and_only_keys_use_the_monospaced_font()
    {
        using var system = TokenDataSet.Read("design-system.json");
        var styles = TokenDataSet
            .DataMembers(system.RootElement.GetProperty("typography"))
            .ToList();

        styles.ShouldContain(style =>
            string.Equals(style.Name, "overline", StringComparison.Ordinal)
        );
        foreach (var style in styles)
        {
            var size = style.Value.GetProperty("size").GetDouble();
            size.ShouldBeGreaterThanOrEqualTo(11, style.Name);
            style.Value.GetProperty("lineHeight").GetDouble().ShouldBeGreaterThanOrEqualTo(size);
            var mono = string.Equals(
                style.Value.GetProperty("family").GetString(),
                "keys",
                StringComparison.Ordinal
            );
            mono.ShouldBe(style.Name.StartsWith("keys", StringComparison.Ordinal), style.Name);
        }
    }

    private static double[] Lengths(JsonElement root, string section) =>
        [
            .. TokenDataSet
                .DataMembers(root.GetProperty(section))
                .Select(member => member.Value.GetDouble()),
        ];
}
