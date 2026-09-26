using System.Globalization;
using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Test view of timings.json, with an independent parser of its unit-bearing values.</summary>
internal sealed class TimingsCatalog
{
    private static readonly Lazy<TimingsCatalog> Instance = new(() => new TimingsCatalog());

    private TimingsCatalog()
    {
        var root = CatalogFiles.LoadObject(CatalogFiles.Catalog("timings.json"));
        Entries =
        [
            .. root["groups"]!
                .AsObject()
                .SelectMany(group =>
                    group.Value!["entries"]!
                        .AsObject()
                        .Select(entry => new TimingEntry(
                            group.Key,
                            entry.Key,
                            entry.Value!.AsObject()
                        ))
                ),
        ];
    }

    public static TimingsCatalog Shared => Instance.Value;

    public IReadOnlyList<TimingEntry> Entries { get; }

    public JsonObject Entry(string group, string name) =>
        Entries
            .Single(e =>
                string.Equals(e.Group, group, StringComparison.Ordinal)
                && string.Equals(e.Name, name, StringComparison.Ordinal)
            )
            .Node;

    /// <summary>Parses <c>600ms</c>, <c>2.5s</c>, <c>10min</c>, <c>24h</c> or <c>7d</c>.</summary>
    public static TimeSpan Duration(string text)
    {
        (string Unit, double Milliseconds)[] units =
        [
            ("min", 60_000),
            ("ms", 1),
            ("s", 1_000),
            ("h", 3_600_000),
            ("d", 86_400_000),
        ];
        foreach (var (unit, milliseconds) in units)
        {
            if (text.EndsWith(unit, StringComparison.Ordinal))
            {
                var value = decimal.Parse(
                    text[..^unit.Length],
                    NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture
                );
                return TimeSpan.FromMilliseconds((double)(value * (decimal)milliseconds));
            }
        }

        throw new FormatException(text + " has no unit.");
    }

    /// <summary>Parses <c>16KiB</c>, <c>5MiB</c> or <c>1024B</c>.</summary>
    public static long Bytes(string text)
    {
        (string Unit, long Factor)[] units =
        [
            ("KiB", 1L << 10),
            ("MiB", 1L << 20),
            ("GiB", 1L << 30),
            ("B", 1),
        ];
        foreach (var (unit, factor) in units)
        {
            if (text.EndsWith(unit, StringComparison.Ordinal))
            {
                return long.Parse(
                        text[..^unit.Length],
                        NumberStyles.None,
                        CultureInfo.InvariantCulture
                    ) * factor;
            }
        }

        throw new FormatException(text + " has no unit.");
    }
}

/// <summary>An entry of timings.json with its group.</summary>
internal sealed record TimingEntry(string Group, string Name, JsonObject Node)
{
    private static readonly string[] Kinds =
    [
        "duration",
        "durations",
        "px",
        "count",
        "ratio",
        "bytes",
        "countWindow",
        "durationRange",
    ];

    public string Path => Group + "." + Name;

    public string Kind => Kinds.Single(k => Node[k] is not null);

    public JsonNode Value => Node[Kind]!;

    public IReadOnlyList<string> Requirements =>
        Node["req"] is JsonArray req ? [.. req.Select(r => r!.GetValue<string>())] : [];
}
