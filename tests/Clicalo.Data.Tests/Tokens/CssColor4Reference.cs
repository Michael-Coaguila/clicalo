namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// Independent reference for the OKLab → linear sRGB conversion: the CSS Color 4 sample code path through
/// CIE XYZ (D65) with the specification's own matrices (rational sRGB ↔ XYZ, 16-digit OKLab ↔ LMS ↔ XYZ).
/// Design.Math instead uses Ottosson's direct OKLab ↔ linear sRGB matrices, so agreement checks both.
/// </summary>
internal static class CssColor4Reference
{
    private static readonly double[,] XyzToLinearSrgb =
    {
        { 12831d / 3959d, -329d / 214d, -1974d / 3959d },
        { -851781d / 878810d, 1648619d / 878810d, 36519d / 878810d },
        { 705d / 12673d, -2585d / 12673d, 705d / 667d },
    };

    private static readonly double[,] LmsToXyz =
    {
        { 1.2268798758459243, -0.5578149944602171, 0.2813910456659647 },
        { -0.0405757452148008, 1.1122868032803170, -0.0717110580655164 },
        { -0.0763729366746601, -0.4214933324022432, 1.5869240198367816 },
    };

    private static readonly double[,] OklabToLms =
    {
        { 1d, 0.3963377773761749, 0.2158037573099136 },
        { 1d, -0.1055613458156586, -0.0638541728258133 },
        { 1d, -0.0894841775298119, -1.2914855480194092 },
    };

    public static (double R, double G, double B) OklchToLinearSrgb(double l, double c, double h)
    {
        var radians = h * Math.PI / 180d;
        var lab = new[] { l, c * Math.Cos(radians), c * Math.Sin(radians) };
        var lms = Multiply(OklabToLms, lab).Select(x => x * x * x).ToArray();
        var rgb = Multiply(XyzToLinearSrgb, Multiply(LmsToXyz, lms));
        return (rgb[0], rgb[1], rgb[2]);
    }

    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        var result = new double[3];
        for (var row = 0; row < 3; row++)
        {
            result[row] =
                (matrix[row, 0] * vector[0])
                + (matrix[row, 1] * vector[1])
                + (matrix[row, 2] * vector[2]);
        }

        return result;
    }
}
