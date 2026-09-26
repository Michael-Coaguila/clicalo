using Clicalo.Design.Math;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>WCAG 2.x luminance, contrast and compositing on the real background (TEM-004).</summary>
[Trait("Req", "TEM-004")]
public sealed class WcagContrastTests
{
    private static readonly Rgba8 Black = new(0, 0, 0);
    private static readonly Rgba8 White = new(255, 255, 255);

    [Fact]
    public void Black_on_white_is_21_to_1_in_both_directions()
    {
        Wcag.ContrastRatio(Black, White).ShouldBe(21d, 1e-12);
        Wcag.ContrastRatio(White, Black).ShouldBe(21d, 1e-12);
    }

    [Fact]
    public void A_color_against_itself_is_1_to_1() =>
        Wcag.ContrastRatio(new Rgba8(18, 52, 86), new Rgba8(18, 52, 86)).ShouldBe(1d);

    [Theory]
    [InlineData("#767676", 0.1811642442, 4.542225)]
    [InlineData("#777777", 0.1844749945, 4.478089)]
    [InlineData("#949494", 0.2961382708, 3.033470)]
    [InlineData("#959595", 0.3005437944, 2.995346)]
    [InlineData("#808080", 0.2158605001, 3.949440)]
    [InlineData("#FF0000", 0.2126, 3.998477)]
    [InlineData("#0000FF", 0.0722, 8.592471)]
    public void Known_grays_and_primaries_have_their_reference_luminance_and_contrast_on_white(
        string hex,
        double luminance,
        double ratioOnWhite
    )
    {
        var color = TokenDataSet.Parse(hex).ToRgba8();

        Wcag.RelativeLuminance(color).ShouldBe(luminance, 1e-9);
        Wcag.ContrastRatio(color, White).ShouldBe(ratioOnWhite, 1e-6);
    }

    [Fact]
    public void The_gray_767676_is_the_lightest_that_passes_text_on_white()
    {
        Wcag.ContrastRatio(new Rgba8(0x76, 0x76, 0x76), White)
            .ShouldBeGreaterThanOrEqualTo(Wcag.MinimumTextContrast);
        Wcag.ContrastRatio(new Rgba8(0x77, 0x77, 0x77), White)
            .ShouldBeLessThan(Wcag.MinimumTextContrast);
    }

    [Fact]
    public void Half_transparent_black_over_white_gives_middle_gray()
    {
        var result = Compositing.Over(new Srgb(0d, 0d, 0d, 0.5), new Srgb(1d, 1d, 1d));

        result.ShouldBe(new Srgb(0.5, 0.5, 0.5, 1d));
    }

    [Fact]
    public void Two_translucent_layers_combine_alpha_like_porter_duff_source_over()
    {
        var result = Compositing.Over(new Srgb(1d, 0d, 0d, 0.5), new Srgb(0d, 0d, 1d, 0.5));

        result.Alpha.ShouldBe(0.75, 1e-12);
        result.R.ShouldBe(2d / 3d, 1e-12);
        result.B.ShouldBe(1d / 3d, 1e-12);
    }

    [Fact]
    public void A_translucent_surface_is_measured_over_every_backdrop_and_reports_the_worst()
    {
        var halfBlack = new Rgba8(0, 0, 0, 128);

        var measurement = ContrastEvaluator.Measure(White, [halfBlack], [Black, White]);

        measurement.BackdropIndex.ShouldBe(1);
        measurement.Ratio.ShouldBe(
            Wcag.ContrastRatio(White.ToSrgb(), measurement.Background),
            1e-12
        );
        measurement.Background.R.ShouldBe(127d / 255d, 1e-9);
    }

    [Fact]
    public void An_opaque_layer_hides_the_backdrops()
    {
        var measurement = ContrastEvaluator.Measure(
            White,
            [new Rgba8(0, 0, 0, 40), Black],
            [White]
        );

        measurement.BackdropIndex.ShouldBe(-1);
        measurement.Ratio.ShouldBe(21d, 1e-12);
    }

    [Fact]
    public void A_translucent_foreground_is_composited_before_measuring()
    {
        var measurement = ContrastEvaluator.Measure(new Rgba8(255, 255, 255, 0), [Black], []);

        measurement.Ratio.ShouldBe(1d);
    }

    [Fact]
    public void A_translucent_background_without_backdrops_cannot_be_measured() =>
        Should.Throw<ArgumentException>(() =>
            ContrastEvaluator.Measure(White, [new Rgba8(0, 0, 0, 10)], [])
        );
}
