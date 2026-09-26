using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Domain.Catalog;

namespace Clicalo.Domain.Migration.V1;

/// <summary>
/// Turns the free colour of a v1 button into a colour category (catalog §7.4, PQ-08): the category whose hue is
/// closest in OKLCH, the space the category tints are defined in (<c>data/tokens/extra-tokens.json</c>). Greys have no
/// meaningful hue and go to <c>win</c>, the category of the v1 grey buttons (desktop, lock, task manager). Accepts
/// <c>#RGB</c>, <c>#RRGGBB</c>, Qt's <c>#AARRGGBB</c> and the basic colour names; anything else is reported, never
/// fatal (EC-MIG-05).
/// </summary>
internal static class V1Colors
{
    /// <summary>The colour of a v1 button without <c>color</c> (the default of its editor).</summary>
    public const string DefaultRgb = "#2980B9";

    /// <summary>Below this OKLCH chroma a colour is a grey.</summary>
    private const double AchromaticChroma = 0.04;

    /// <summary>The nine colours of the v1 palette (catalog §7.2); a colour from it is not reported.</summary>
    public static FrozenSet<string> Palette { get; } =
        new[]
        {
            "#2980B9",
            "#E67E22",
            "#8E44AD",
            "#27AE60",
            "#7F8C8D",
            "#C0392B",
            "#16A085",
            "#F39C12",
            "#E74C3C",
        }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// The hue of each category tint, in degrees (<c>categories.hues</c> of <c>data/tokens/extra-tokens.json</c>, which a
    /// test keeps in step), in catalog order so a tie goes to the first.
    /// </summary>
    public static ImmutableArray<(CategoryId Category, double Hue)> CategoryHues { get; } =
    [
        (new CategoryId("edit"), 230),
        (new CategoryId("hist"), 60),
        (new CategoryId("file"), 150),
        (new CategoryId("sel"), 300),
        (new CategoryId("win"), 25),
        (new CategoryId("voice"), 190),
        (new CategoryId("nav"), 270),
        (new CategoryId("fmt"), 330),
        (new CategoryId("web"), 200),
        (new CategoryId("text"), 120),
    ];

    /// <summary>The category of greys.</summary>
    public static CategoryId Neutral { get; } = new("win");

    private static FrozenDictionary<string, string> Named { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["black"] = "#000000",
            ["white"] = "#FFFFFF",
            ["gray"] = "#808080",
            ["grey"] = "#808080",
            ["silver"] = "#C0C0C0",
            ["red"] = "#FF0000",
            ["maroon"] = "#800000",
            ["orange"] = "#FFA500",
            ["yellow"] = "#FFFF00",
            ["olive"] = "#808000",
            ["lime"] = "#00FF00",
            ["green"] = "#008000",
            ["teal"] = "#008080",
            ["aqua"] = "#00FFFF",
            ["cyan"] = "#00FFFF",
            ["blue"] = "#0000FF",
            ["navy"] = "#000080",
            ["purple"] = "#800080",
            ["fuchsia"] = "#FF00FF",
            ["magenta"] = "#FF00FF",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The category of a v1 colour and whether the report mentions it.</summary>
    /// <param name="color">The <c>color</c> of the button as written, or <see langword="null"/>.</param>
    public static V1ColorMapping Map(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return new V1ColorMapping(Nearest(DefaultRgb), V1ColorOrigin.Default, DefaultRgb);
        }

        var rgb = Normalize(color.Trim());
        if (rgb is null)
        {
            return new V1ColorMapping(Nearest(DefaultRgb), V1ColorOrigin.Invalid, null);
        }

        var origin = Palette.Contains(rgb) ? V1ColorOrigin.Palette : V1ColorOrigin.Custom;
        return new V1ColorMapping(Nearest(rgb), origin, rgb);
    }

    /// <summary>The colour as <c>#RRGGBB</c> in upper case, or <see langword="null"/> when it is not a colour.</summary>
    /// <param name="text">The trimmed colour text.</param>
    public static string? Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (!text.StartsWith('#'))
        {
            return Named.TryGetValue(text, out var named) ? named : null;
        }

        var hex = text.AsSpan(1);
        foreach (var c in hex)
        {
            if (!char.IsAsciiHexDigit(c))
            {
                return null;
            }
        }

        var rrggbb = hex.Length switch
        {
            3 => string.Create(
                6,
                text,
                static (span, source) =>
                {
                    for (var i = 0; i < 3; i++)
                    {
                        span[2 * i] = source[i + 1];
                        span[(2 * i) + 1] = source[i + 1];
                    }
                }
            ),
            6 => hex.ToString(),

            // Qt writes the alpha first (#AARRGGBB); the category ignores it.
            8 => hex[2..].ToString(),
            _ => null,
        };
        return rrggbb is null ? null : "#" + rrggbb.ToUpperInvariant();
    }

    /// <summary>The category closest in hue to <paramref name="rgb"/>, or <see cref="Neutral"/> for a grey.</summary>
    /// <param name="rgb">A colour as <c>#RRGGBB</c>.</param>
    public static CategoryId Nearest(string rgb)
    {
        var (chroma, hue) = Oklch(rgb);
        if (chroma < AchromaticChroma)
        {
            return Neutral;
        }

        var best = CategoryHues[0];
        var bestDistance = double.MaxValue;
        foreach (var candidate in CategoryHues)
        {
            var distance = Math.Abs(hue - candidate.Hue) % 360;
            distance = Math.Min(distance, 360 - distance);
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best.Category;
    }

    /// <summary>Chroma and hue (degrees) of an sRGB colour in OKLCH.</summary>
    /// <param name="rgb">A colour as <c>#RRGGBB</c>.</param>
    public static (double Chroma, double Hue) Oklch(string rgb)
    {
        ArgumentNullException.ThrowIfNull(rgb);
        var r = Linear(Channel(rgb, 1));
        var g = Linear(Channel(rgb, 3));
        var b = Linear(Channel(rgb, 5));

        var l = Math.Cbrt((0.4122214708 * r) + (0.5363325363 * g) + (0.0514459929 * b));
        var m = Math.Cbrt((0.2119034982 * r) + (0.6806995451 * g) + (0.1073969566 * b));
        var s = Math.Cbrt((0.0883024619 * r) + (0.2817188376 * g) + (0.6299787005 * b));

        var a = (1.9779984951 * l) - (2.4285922050 * m) + (0.4505937099 * s);
        var bb = (0.0259040371 * l) + (0.7827717662 * m) - (0.8086757660 * s);
        var hue = Math.Atan2(bb, a) * 180 / Math.PI;
        return (Math.Sqrt((a * a) + (bb * bb)), hue < 0 ? hue + 360 : hue);
    }

    private static double Channel(string rgb, int start) =>
        int.Parse(
            rgb.AsSpan(start, 2),
            NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture
        ) / 255.0;

    private static double Linear(double channel) =>
        channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
}
