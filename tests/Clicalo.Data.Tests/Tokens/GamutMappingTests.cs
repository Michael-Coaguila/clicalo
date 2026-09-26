using Clicalo.Design.Math;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>CSS Color 4 gamut mapping (TEM-003: "conversión a sRGB con un mapeo de gamut definido").</summary>
[Trait("Req", "TEM-003")]
public sealed class GamutMappingTests
{
    [Theory]
    [InlineData(0.7, 0.4, 150d, "#00C248", 0.190410)]
    [InlineData(0.5, 0.3, 30d, "#C30000", 0.090289)]
    [InlineData(0.9, 0.3, 100d, "#FFDF00", 0.113412)]
    [InlineData(0.5, 0.11, 220d, "#00708E", 0.018170)]
    [InlineData(0.5, 0.12, 190d, "#007671", 0.033244)]
    [InlineData(0.82, 0.12, 270d, "#A8C1FF", 0.028458)]
    public void Out_of_gamut_colors_map_to_the_reference_result(
        double l,
        double c,
        double h,
        string hex,
        double deltaEok
    )
    {
        var result = GamutMapping.ToSrgb(new Oklch(l, c, h));

        result.WasInGamut.ShouldBeFalse();
        result.Color.ToRgba8().ToRgbHex().ShouldBe(hex);
        result.DeltaEok.ShouldBe(deltaEok, 1e-4);
    }

    [Fact]
    public void In_gamut_colors_are_returned_unchanged()
    {
        var color = new Oklch(0.8, 0.11, 200d, 0.16);

        var result = GamutMapping.ToSrgb(color);

        result.WasInGamut.ShouldBeTrue();
        result.DeltaEok.ShouldBe(0d);
        result.Color.ShouldBe(color.ToOklab().ToSrgb());
    }

    [Theory]
    [InlineData(1d, 0.2, 1d, 1d, 1d)]
    [InlineData(1.2, 0.3, 1d, 1d, 1d)]
    [InlineData(0d, 0.2, 0d, 0d, 0d)]
    public void Lightness_at_the_ends_gives_white_or_black_and_keeps_alpha(
        double l,
        double c,
        double r,
        double g,
        double b
    )
    {
        var result = GamutMapping.ToSrgb(new Oklch(l, c, 120d, 0.4));

        result.Color.ShouldBe(new Srgb(r, g, b, 0.4));
    }

    [Fact]
    public void Every_mapped_color_is_in_gamut_and_keeps_lightness_within_one_jnd()
    {
        for (var l = 0.05; l < 1d; l += 0.05)
        {
            for (var c = 0d; c <= 0.4; c += 0.05)
            {
                for (var h = 0d; h < 360d; h += 15d)
                {
                    var origin = new Oklch(l, c, h);
                    var result = GamutMapping.ToSrgb(origin);
                    var mapped = result.Color.ToOklab().ToOklch();

                    result.Color.IsInGamut().ShouldBeTrue();
                    Math.Abs(mapped.L - l).ShouldBeLessThan(GamutMapping.JustNoticeableDifference);
                    mapped.C.ShouldBeLessThanOrEqualTo(c + GamutMapping.JustNoticeableDifference);
                    if (!result.WasInGamut)
                    {
                        result.DeltaEok.ShouldBeGreaterThan(0d);
                    }
                }
            }
        }
    }
}
