using Clicalo.Design.Math;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// OKLCH → OKLab → linear sRGB → sRGB against reference vectors. Hex values were computed independently with
/// Python through the CSS Color 4 XYZ path (not Design.Math) and pinned here.
/// </summary>
[Trait("Req", "TEM-003")]
public sealed class ColorConversionTests
{
    [Theory]
    [InlineData(0.62796, 0.25768, 29.2339, "#FF0000")]
    [InlineData(0.45201, 0.31321, 264.052, "#0000FF")]
    [InlineData(0.86644, 0.29483, 142.4953, "#00FF00")]
    [InlineData(1d, 0d, 0d, "#FFFFFF")]
    [InlineData(0d, 0d, 0d, "#000000")]
    public void Css_reference_primaries_and_extremes_convert_exactly(
        double l,
        double c,
        double h,
        string hex
    ) => GamutMapping.ToSrgb(new Oklch(l, c, h)).Color.ToRgba8().ToRgbHex().ShouldBe(hex);

    [Theory]
    [InlineData(1d, 0d, 0d, 0.6279553639, 0.2248630684, 0.1258462773)]
    [InlineData(0d, 1d, 0d, 0.8664396175, -0.2338875809, 0.1794984452)]
    [InlineData(0d, 0d, 1d, 0.4520137182, -0.0324569752, -0.3115281657)]
    [InlineData(1d, 1d, 1d, 1d, 0d, 0d)]
    [InlineData(0.5, 0.5, 0.5, 0.5981807305, 0d, 0d)]
    public void Srgb_to_oklab_matches_the_css_color_4_values(
        double r,
        double g,
        double b,
        double l,
        double a,
        double bAxis
    )
    {
        var lab = new Srgb(r, g, b).ToOklab();

        lab.L.ShouldBe(l, 1e-6);
        lab.A.ShouldBe(a, 1e-6);
        lab.B.ShouldBe(bAxis, 1e-6);
    }

    [Fact]
    public void Black_is_exactly_zero() =>
        new Srgb(0d, 0d, 0d).ToOklab().ShouldBe(new Oklab(0d, 0d, 0d));

    [Fact]
    public void Every_8_bit_color_of_a_dense_grid_survives_a_round_trip_through_oklch()
    {
        for (var r = 0; r <= 255; r += 15)
        {
            for (var g = 0; g <= 255; g += 15)
            {
                for (var b = 0; b <= 255; b += 15)
                {
                    var original = new Rgba8((byte)r, (byte)g, (byte)b);
                    var back = original.ToOklab().ToOklch().ToOklab().ToSrgb().ToRgba8();

                    back.ShouldBe(original);
                }
            }
        }
    }

    [Fact]
    public void Ottosson_matrices_agree_with_the_css_color_4_xyz_path()
    {
        var random = new Random(20260925);
        for (var i = 0; i < 5000; i++)
        {
            var l = random.NextDouble();
            var c = random.NextDouble() * 0.4;
            var h = random.NextDouble() * 360d;

            var actual = new Oklch(l, c, h).ToOklab().ToLinearSrgb();
            var expected = CssColor4Reference.OklchToLinearSrgb(l, c, h);

            actual.R.ShouldBe(expected.R, 1e-7);
            actual.G.ShouldBe(expected.G, 1e-7);
            actual.B.ShouldBe(expected.B, 1e-7);
        }
    }

    [Fact]
    public void Achromatic_colors_report_hue_zero()
    {
        var gray = new Srgb(0.5, 0.5, 0.5).ToOklab().ToOklch();

        gray.C.ShouldBe(0d, 1e-7);
        gray.H.ShouldBe(0d);
    }

    [Theory]
    [InlineData("dark", "oklch(0.36 0.02 250)", "#FF353E47")]
    [InlineData("dark", "oklch(0.2 0.012 260 / 0.96)", "#F513161C")]
    [InlineData("dark", "oklch(0.2 0.012 260)", "#FF13161C")]
    [InlineData("dark", "oklch(0.175 0.01 260)", "#FF0E1115")]
    [InlineData("dark", "oklch(0.27 0.014 260)", "#FF22272D")]
    [InlineData("dark", "oklch(0.33 0.016 260)", "#FF31363E")]
    [InlineData("dark", "oklch(0.97 0.004 260)", "#FFF3F5F8")]
    [InlineData("dark", "oklch(0.76 0.012 260)", "#FFADB1B9")]
    [InlineData("dark", "oklch(1 0 0 / 0.14)", "#24FFFFFF")]
    [InlineData("dark", "oklch(0.80 0.11 200)", "#FF56D3DA")]
    [InlineData("dark", "oklch(0.80 0.11 200 / 0.16)", "#2956D3DA")]
    [InlineData("dark", "oklch(0.2 0.03 220)", "#FF04191F")]
    [InlineData("dark", "oklch(0.83 0.13 80)", "#FFF3BD5C")]
    [InlineData("dark", "oklch(0.83 0.13 80 / 0.14)", "#24F3BD5C")]
    [InlineData("dark", "oklch(1 0 0 / 0.09)", "#17FFFFFF")]
    [InlineData("dark", "oklch(0.15 0.01 260)", "#FF090B0F")]
    [InlineData("light", "oklch(0.82 0.02 250)", "#FFBBC5D1")]
    [InlineData("light", "oklch(0.985 0.003 260 / 0.97)", "#F7F9FAFC")]
    [InlineData("light", "oklch(0.985 0.003 260)", "#FFF9FAFC")]
    [InlineData("light", "oklch(0.96 0.004 260)", "#FFF0F2F4")]
    [InlineData("light", "oklch(0.94 0.006 260)", "#FFE9EBEF")]
    [InlineData("light", "oklch(0.9 0.01 260)", "#FFDADEE5")]
    [InlineData("light", "oklch(0.22 0.015 260)", "#FF171B22")]
    [InlineData("light", "oklch(0.45 0.015 260)", "#FF50565E")]
    [InlineData("light", "oklch(0 0 0 / 0.16)", "#29000000")]
    [InlineData("light", "oklch(0.50 0.11 220)", "#FF00708E")]
    [InlineData("light", "oklch(0.50 0.11 220 / 0.12)", "#1F00708E")]
    [InlineData("light", "oklch(0.58 0.13 70)", "#FFAA6A00")]
    [InlineData("light", "oklch(0.75 0.13 80 / 0.18)", "#2ED9A440")]
    [InlineData("light", "oklch(0 0 0 / 0.1)", "#1A000000")]
    [InlineData("corrected", "oklch(0.588 0.18 25)", "#FFD24342")]
    [InlineData("corrected", "oklch(1 0 0 / 0.34)", "#57FFFFFF")]
    [InlineData("corrected", "oklch(0.474 0.11 220)", "#FF006885")]
    [InlineData("corrected", "oklch(0.602 0.13 70)", "#FFB17102")]
    [InlineData("corrected", "oklch(0.503 0.13 70)", "#FF915300")]
    [InlineData("corrected", "oklch(0.506 0.16 25)", "#FFAE3232")]
    [InlineData("corrected", "oklch(0 0 0 / 0.43)", "#6E000000")]
    [InlineData("extra", "oklch(0.72 0.16 25)", "#FFF97770")]
    [InlineData("extra", "oklch(0.2 0.02 80)", "#FF1B150B")]
    [InlineData("extra", "oklch(0.6 0.13 150)", "#FF3B9555")]
    [InlineData("extra", "oklch(0.62 0.18 25 / 0.14)", "#24DE4E4B")]
    [InlineData("extra", "oklch(0 0 0 / 0.35)", "#59000000")]
    public void Palette_values_match_independent_reference_values(
        string origin,
        string value,
        string argb
    ) =>
        TokenDataSet
            .Parse(value)
            .ToRgba8()
            .ToArgbHex()
            .ShouldBe(argb, customMessage: origin + " " + value);

    [Theory]
    [InlineData(
        0.82,
        "#67D2FF #FDB171 #89DA9B #D0B2FF #FFA59D #50DDD5 #A8C1FF #F0A7E9 #4CDBE3 #BAD074"
    )]
    [InlineData(
        0.84,
        "#6ED9FF #FFB777 #8FE1A1 #D6B9FF #FFAEA6 #58E3DC #B1C8FF #F7AEF0 #54E2E9 #C0D67A"
    )]
    [InlineData(
        0.5,
        "#006E9A #934F00 #21763C #6E519D #9C433F #007671 #495DA7 #884783 #00747B #5B6C00"
    )]
    [InlineData(
        0.468,
        "#00648E #894600 #136C33 #654893 #923A36 #006B67 #41549D #7E3E7A #006A70 #526300"
    )]
    [InlineData(
        0.72,
        "#43B2E1 #DB9152 #69BA7C #B093E5 #E6857E #1DBCB5 #87A0F0 #CF88C8 #14BBC2 #9BB054"
    )]
    public void Category_colors_match_independent_reference_values(
        double lightness,
        string expected
    )
    {
        double[] hues = [230, 60, 150, 300, 25, 190, 270, 330, 200, 120];

        var actual = hues.Select(hue =>
            GamutMapping.ToSrgb(new Oklch(lightness, 0.12, hue)).Color.ToRgba8().ToRgbHex()
        );

        string.Join(' ', actual).ShouldBe(expected);
    }
}
