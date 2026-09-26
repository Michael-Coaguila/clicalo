namespace Clicalo.Design.Math;

/// <summary>The sRGB transfer function (IEC 61966-2-1) as written in CSS Color 4, extended to negative values.</summary>
public static class TransferFunctions
{
    /// <summary>Linear-light value at or below which the encoding is linear.</summary>
    public const double LinearThreshold = 0.0031308;

    /// <summary>
    /// Encoded value at or below which the decoding is linear. WCAG 2.x historically printed 0.03928; the value
    /// of the sRGB standard (and of the WCAG 2.2 errata) is 0.04045. For 8-bit channels both give identical results.
    /// </summary>
    public const double EncodedThreshold = 0.04045;

    /// <summary>Linear light → gamma-encoded sRGB (CSS <c>gam_sRGB</c>).</summary>
    public static double Encode(double linear)
    {
        var magnitude = System.Math.Abs(linear);
        if (magnitude <= LinearThreshold)
        {
            return 12.92 * linear;
        }

        var encoded = (1.055 * System.Math.Pow(magnitude, 1d / 2.4)) - 0.055;
        return linear < 0d ? -encoded : encoded;
    }

    /// <summary>Gamma-encoded sRGB → linear light (CSS <c>lin_sRGB</c>).</summary>
    public static double Decode(double encoded)
    {
        var magnitude = System.Math.Abs(encoded);
        if (magnitude <= EncodedThreshold)
        {
            return encoded / 12.92;
        }

        var linear = System.Math.Pow((magnitude + 0.055) / 1.055, 2.4);
        return encoded < 0d ? -linear : linear;
    }
}
