using System.Globalization;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// Orders SemVer 2.0.0 versions (blueprint §11: <c>X.Y.Z</c> and <c>X.Y.Z-beta.N</c>): the numbers, then a version
/// without a pre-release after one with it, then the pre-release identifiers one by one (numbers as numbers). Build
/// metadata (<c>+…</c>) is ignored.
/// </summary>
internal static class VersionOrder
{
    /// <summary>Negative when <paramref name="left"/> is older, positive when newer, zero when equal.</summary>
    /// <param name="left">A version.</param>
    /// <param name="right">Another version.</param>
    public static int Compare(string left, string right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var (leftCore, leftPre) = Split(left);
        var (rightCore, rightPre) = Split(right);
        var core = CompareIdentifiers(leftCore.Split('.'), rightCore.Split('.'), numericOnly: true);
        if (core != 0)
        {
            return core;
        }

        if (leftPre.Length == 0 || rightPre.Length == 0)
        {
            return rightPre.Length.CompareTo(leftPre.Length) switch
            {
                < 0 => -1,
                > 0 => 1,
                _ => 0,
            };
        }

        return CompareIdentifiers(leftPre.Split('.'), rightPre.Split('.'), numericOnly: false);
    }

    /// <summary>Whether <paramref name="candidate"/> is newer than <paramref name="current"/>.</summary>
    /// <param name="candidate">A version.</param>
    /// <param name="current">The version it is compared with.</param>
    public static bool IsNewer(string candidate, string current) => Compare(candidate, current) > 0;

    private static (string Core, string Pre) Split(string version)
    {
        var text = version.Trim();
        var plus = text.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            text = text[..plus];
        }

        var dash = text.IndexOf('-', StringComparison.Ordinal);
        return dash < 0 ? (text, string.Empty) : (text[..dash], text[(dash + 1)..]);
    }

    private static int CompareIdentifiers(string[] left, string[] right, bool numericOnly)
    {
        var count = Math.Max(left.Length, right.Length);
        for (var i = 0; i < count; i++)
        {
            if (i >= left.Length)
            {
                return numericOnly ? CompareParts("0", right[i]) : -1;
            }

            if (i >= right.Length)
            {
                return numericOnly ? CompareParts(left[i], "0") : 1;
            }

            var part = CompareParts(left[i], right[i]);
            if (part != 0)
            {
                return part;
            }
        }

        return 0;
    }

    private static int CompareParts(string left, string right)
    {
        var leftIsNumber = long.TryParse(
            left,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var leftNumber
        );
        var rightIsNumber = long.TryParse(
            right,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var rightNumber
        );
        return (leftIsNumber, rightIsNumber) switch
        {
            (true, true) => Math.Sign(leftNumber.CompareTo(rightNumber)),
            (true, false) => -1,
            (false, true) => 1,
            _ => Math.Sign(string.CompareOrdinal(left, right)),
        };
    }
}
