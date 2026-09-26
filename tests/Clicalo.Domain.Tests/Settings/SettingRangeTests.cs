using Clicalo.Domain.Settings;

namespace Clicalo.Domain.Tests.Settings;

/// <summary>The ranges of the settings and the v1 opacity conversion (MIG-006, GEN-009).</summary>
public sealed class SettingRangeTests
{
    [Theory]
    [Trait("Req", "MIG-006")]
    [InlineData(0.68, 0.70)]
    [InlineData(0.54, 0.55)]
    [InlineData(0.78, 0.80)]
    [InlineData(0.92, 0.90)]
    [InlineData(0.10, 0.30)]
    [InlineData(1.20, 1.00)]
    public void V1_opacity_snaps_to_the_nearest_step_inside_the_range(double v1, double expected) =>
        SettingsSchema.Opacity.Snap(v1).ShouldBe(expected);

    [Fact]
    [Trait("Req", "GEN-009")]
    public void Opacity_and_dimming_have_the_ranges_of_the_catalog()
    {
        SettingsSchema.Opacity.ShouldBe(new SettingRange(0.30, 1.00, 0.05));
        SettingsSchema.DimTo.ShouldBe(new SettingRange(0.10, 0.80, 0.05));
    }
}
