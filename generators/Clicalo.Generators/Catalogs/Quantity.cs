using System.Globalization;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Parses the unit-bearing values of the catalogs: durations (<c>600ms</c>, <c>2.5s</c>, <c>10min</c>, <c>24h</c>,
/// <c>7d</c>) into ticks and byte sizes (<c>16KiB</c>) into bytes. A value without unit is never guessed.
/// </summary>
internal static class Quantity
{
    public const string DurationUnits = "ms, s, min, h, d";
    public const string ByteUnits = "B, KiB, MiB, GiB";

    private static readonly (string Unit, decimal Ticks)[] DurationScale =
    [
        // Longest suffix first so "min" is not read as "m" + "in", and "ms" not as "s".
        ("min", 600_000_000m),
        ("ms", 10_000m),
        ("s", 10_000_000m),
        ("h", 36_000_000_000m),
        ("d", 864_000_000_000m),
    ];

    private static readonly (string Unit, decimal Bytes)[] ByteScale =
    [
        ("KiB", 1024m),
        ("MiB", 1024m * 1024m),
        ("GiB", 1024m * 1024m * 1024m),
        ("B", 1m),
    ];

    /// <summary>Reads a duration node, reporting CLCC004 (negative) or CLCC005 (no or unknown unit).</summary>
    public static long? Ticks(CatalogReader reader, JsonNode node, string what) =>
        Read(reader, node, what, DurationScale, DurationUnits);

    /// <summary>Reads a byte size node, reporting CLCC004 (negative) or CLCC005 (no or unknown unit).</summary>
    public static long? Bytes(CatalogReader reader, JsonNode node, string what) =>
        Read(reader, node, what, ByteScale, ByteUnits);

    private static long? Read(
        CatalogReader reader,
        JsonNode node,
        string what,
        (string Unit, decimal Factor)[] scale,
        string units
    )
    {
        if (node.Kind == JsonKind.Number)
        {
            reader.Report(
                node.NumberValue < 0
                    ? CatalogDiagnostics.NegativeValue
                    : CatalogDiagnostics.MissingUnit,
                node,
                what,
                units
            );
            return null;
        }

        if (node.Kind != JsonKind.String || string.IsNullOrEmpty(node.StringValue))
        {
            reader.Structure(
                node,
                $"{reader.FileName}: '{what}' must be a string such as 600ms or 16KiB."
            );
            return null;
        }

        var text = node.StringValue!;
        if (text[0] == '-')
        {
            reader.Report(CatalogDiagnostics.NegativeValue, node, what);
            return null;
        }

        foreach (var (unit, factor) in scale)
        {
            if (!text.EndsWith(unit, System.StringComparison.Ordinal))
            {
                continue;
            }

            var number = text.Substring(0, text.Length - unit.Length);
            if (!IsPlainNumber(number))
            {
                reader.Structure(
                    node,
                    $"{reader.FileName}: '{what}' must be a number followed by a unit ({units})."
                );
                return null;
            }

            var value =
                decimal.Parse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture)
                * factor;
            if (value != decimal.Truncate(value) || value > long.MaxValue)
            {
                reader.Structure(
                    node,
                    $"{reader.FileName}: '{what}' is not representable exactly."
                );
                return null;
            }

            return (long)value;
        }

        if (IsPlainNumber(text))
        {
            reader.Report(CatalogDiagnostics.MissingUnit, node, what, units);
        }
        else
        {
            reader.Structure(
                node,
                $"{reader.FileName}: '{what}' must be a number followed by a unit ({units})."
            );
        }

        return null;
    }

    /// <summary>Digits with an optional fractional part: no sign, exponent or spaces.</summary>
    private static bool IsPlainNumber(string text)
    {
        if (text.Length == 0 || text[0] == '.' || text[text.Length - 1] == '.')
        {
            return false;
        }

        var dots = 0;
        foreach (var c in text)
        {
            if (c == '.')
            {
                dots++;
            }
            else if (c < '0' || c > '9')
            {
                return false;
            }
        }

        return dots <= 1;
    }
}
