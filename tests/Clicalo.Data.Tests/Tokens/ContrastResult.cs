using System.Globalization;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>A measured pair: theme, foreground, background stack, category (if any), ratio and minimum.</summary>
internal sealed record ContrastResult(
    string Theme,
    PairSpec Pair,
    string Background,
    string Category,
    double Ratio,
    double Minimum
)
{
    public bool Passes => Ratio >= Minimum;

    public override string ToString() =>
        string.Format(
            CultureInfo.InvariantCulture,
            "{0}: {1} '{2}'{3} on '{4}' = {5:0.000}:1 (minimum {6}:1)",
            Theme,
            Pair.IsText ? "text" : "graphic",
            Pair.Foreground,
            Category.Length == 0 ? string.Empty : " [" + Category + "]",
            Background,
            Ratio,
            Minimum
        );
}
