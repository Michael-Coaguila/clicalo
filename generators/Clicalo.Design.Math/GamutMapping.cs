namespace Clicalo.Design.Math;

/// <summary>
/// CSS Color 4 gamut mapping to sRGB (§13.2, "CSS gamut mapping to an RGB destination"): binary search on
/// OKLCH chroma at constant lightness and hue, accepting the clipped color as soon as it is within one
/// just noticeable difference (ΔEOK &lt; 0.02) of the chroma-reduced color.
/// </summary>
/// <seealso href="https://www.w3.org/TR/css-color-4/#gamut-mapping"/>
public static class GamutMapping
{
    /// <summary>The CSS Color 4 just noticeable difference in ΔEOK.</summary>
    public const double JustNoticeableDifference = 0.02;

    /// <summary>The CSS Color 4 chroma search resolution.</summary>
    public const double Epsilon = 0.0001;

    /// <summary>Maps an OKLCH color into the sRGB gamut. Colors already in gamut are returned unchanged.</summary>
    public static GamutMappingResult ToSrgb(Oklch origin)
    {
        if (origin.L >= 1d)
        {
            return Result(origin, new Srgb(1d, 1d, 1d, origin.Alpha), wasInGamut: true);
        }

        if (origin.L <= 0d)
        {
            return Result(origin, new Srgb(0d, 0d, 0d, origin.Alpha), wasInGamut: true);
        }

        var direct = origin.ToOklab().ToSrgb();
        if (direct.IsInGamut())
        {
            return Result(origin, direct, wasInGamut: true);
        }

        var current = origin;
        var clipped = direct.Clip();
        if (Delta(clipped, current) < JustNoticeableDifference)
        {
            return Result(origin, clipped, wasInGamut: false);
        }

        var min = 0d;
        var max = origin.C;
        var minInGamut = true;
        while (max - min > Epsilon)
        {
            var chroma = (min + max) / 2d;
            current = current.WithChroma(chroma);
            var candidate = current.ToOklab().ToSrgb();
            if (minInGamut && candidate.IsInGamut())
            {
                min = chroma;
                continue;
            }

            clipped = candidate.Clip();
            var e = Delta(clipped, current);
            if (e < JustNoticeableDifference)
            {
                if (JustNoticeableDifference - e < Epsilon)
                {
                    return Result(origin, clipped, wasInGamut: false);
                }

                minInGamut = false;
                min = chroma;
            }
            else
            {
                max = chroma;
            }
        }

        return Result(origin, clipped, wasInGamut: false);
    }

    private static double Delta(Srgb clipped, Oklch current) =>
        clipped.ToOklab().DistanceTo(current.ToOklab());

    private static GamutMappingResult Result(Oklch origin, Srgb mapped, bool wasInGamut) =>
        new(mapped, wasInGamut, wasInGamut ? 0d : mapped.ToOklab().DistanceTo(origin.ToOklab()));
}
