using Clicalo.Design.Math;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>The CSS subset of the token data: <c>oklch(L C H / A)</c>, <c>#RRGGBB</c>, <c>&lt;n&gt;px solid …</c>.</summary>
public sealed class CssParserTests
{
    [Theory]
    [InlineData("oklch(0.2 0.012 260 / 0.96)", 0.2, 0.012, 260d, 0.96)]
    [InlineData("oklch(1 0 0)", 1d, 0d, 0d, 1d)]
    [InlineData("  oklch( 0.5   0.1  200 )  ", 0.5, 0.1, 200d, 1d)]
    [InlineData("OKLCH(0.5 0.1 200)", 0.5, 0.1, 200d, 1d)]
    [InlineData("oklch(.5 .1 200)", 0.5, 0.1, 200d, 1d)]
    [InlineData("oklch(0.2 0.012 260/0.96)", 0.2, 0.012, 260d, 0.96)]
    [InlineData("oklch(0 0 0 / 0)", 0d, 0d, 0d, 0d)]
    public void Parses_oklch(string text, double l, double c, double h, double alpha)
    {
        CssParser.TryParseColor(text, out var color, out _).ShouldBeTrue();

        color.Notation.ShouldBe(CssColorNotation.Oklch);
        color.Oklch.ShouldBe(new Oklch(l, c, h, alpha));
    }

    [Theory]
    [InlineData("#ffffff", 255, 255, 255)]
    [InlineData("#FFE600", 255, 230, 0)]
    [InlineData("#1c1c1c", 28, 28, 28)]
    public void Parses_hex(string text, byte r, byte g, byte b)
    {
        CssParser.TryParseColor(text, out var color, out _).ShouldBeTrue();

        color.Notation.ShouldBe(CssColorNotation.Hex);
        color.ToRgba8().ShouldBe(new Rgba8(r, g, b));
    }

    [Theory]
    [InlineData("oklch(1.2 0 0)", 6, "lightness")]
    [InlineData("oklch(0.5 -0.1 200)", 10, "chroma")]
    [InlineData("oklch(0.5 0.1 400)", 14, "hue")]
    [InlineData("oklch(0.5 0.1)", 13, "whitespace")]
    [InlineData("oklch(0.5 0.1 200", 17, "')'")]
    [InlineData("oklch(50% 0.1 200)", 8, "percentages")]
    [InlineData("oklch(0.5 0.1 200deg)", 17, "Units")]
    [InlineData("oklch(0.5 0.1 200 / 1.5)", 20, "alpha")]
    [InlineData("oklch(0.5 0.1 200) x", 19, "after the color")]
    [InlineData("#fff", 3, "six hexadecimal digits")]
    [InlineData("#ffffffff", 0, "#RRGGBB")]
    [InlineData("rgb(0 0 0)", 0, "oklch(")]
    [InlineData("", 0, "oklch(")]
    public void Rejects_invalid_colors_with_the_offset_of_the_problem(
        string text,
        int offset,
        string reason
    )
    {
        CssParser.TryParseColor(text, out _, out var error).ShouldBeFalse();

        error.Offset.ShouldBe(offset);
        error.Message.ShouldContain(reason);
    }

    [Theory]
    [InlineData("1px solid #ffffff", 1d, "#FFFFFFFF")]
    [InlineData("2px solid #ffffff", 2d, "#FFFFFFFF")]
    [InlineData("1px solid oklch(1 0 0 / 0.09)", 1d, "#17FFFFFF")]
    public void Parses_the_border_shorthand(string text, double width, string argb)
    {
        CssParser.TryParseBorder(text, out var border, out _).ShouldBeTrue();

        border.Width.ShouldBe(width);
        border.Color.ToRgba8().ToArgbHex().ShouldBe(argb);
    }

    [Theory]
    [InlineData("1px dashed #ffffff", 4, "solid")]
    [InlineData("solid #ffffff", 0, "width")]
    [InlineData("0px solid #ffffff", 0, "greater than zero")]
    [InlineData("1 solid #ffffff", 1, "px")]
    [InlineData("1px solid", 9, "whitespace")]
    [InlineData("1px solid nope", 10, "oklch(")]
    public void Rejects_invalid_borders_with_the_offset_of_the_problem(
        string text,
        int offset,
        string reason
    )
    {
        CssParser.TryParseBorder(text, out _, out var error).ShouldBeFalse();

        error.Offset.ShouldBe(offset);
        error.Message.ShouldContain(reason);
    }
}
