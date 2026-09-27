using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Clicalo.TestKit;

namespace Clicalo.Performance;

/// <summary>
/// The budgets of <c>data/catalogs/budgets.json</c> (blueprint §10.3, schema <c>data/schemas/budgets.schema.json</c>):
/// the numbers come from the plan and live only there; changing one is a change of requirement (§6.1 of the catalog).
/// </summary>
internal sealed partial class PerformanceBudgets
{
    private static readonly Lazy<PerformanceBudgets> Catalog = new(() =>
        Parse(File.ReadAllText(CatalogPath))
    );

    private readonly Dictionary<string, PerformanceBudget> _byName;

    private PerformanceBudgets(Dictionary<string, PerformanceBudget> byName) => _byName = byName;

    /// <summary>The versioned catalog.</summary>
    public static string CatalogPath => Path.Combine(RepoPaths.Data, "catalogs", "budgets.json");

    /// <summary>The budgets of the repository.</summary>
    public static PerformanceBudgets Shared => Catalog.Value;

    /// <summary>Every budget, by name.</summary>
    public IReadOnlyDictionary<string, PerformanceBudget> All => _byName;

    /// <summary>Touch → <c>SendInput</c> p95 (NFR-001, M2 exit criterion).</summary>
    public PerformanceBudget TouchToSendInput => Get(nameof(TouchToSendInput));

    /// <summary>First frame of the first start after publishing: maximum.</summary>
    public PerformanceBudget ColdFirstFrameMax => Get(nameof(ColdFirstFrameMax));

    /// <summary>First frame of a warm start: p50.</summary>
    public PerformanceBudget WarmFirstFrame => Get(nameof(WarmFirstFrame));

    /// <summary>First frame of a warm start: maximum.</summary>
    public PerformanceBudget WarmFirstFrameMax => Get(nameof(WarmFirstFrameMax));

    /// <summary>Working set of Clicalo.exe.</summary>
    public PerformanceBudget WorkingSet => Get(nameof(WorkingSet));

    /// <summary>Reads a catalog with the shape of <c>budgets.schema.json</c>.</summary>
    /// <param name="json">The catalog.</param>
    public static PerformanceBudgets Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var budgets = new Dictionary<string, PerformanceBudget>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.GetProperty("budgets").EnumerateObject())
        {
            var value = entry.Value;
            budgets.Add(
                entry.Name,
                new PerformanceBudget(
                    entry.Name,
                    ParseStatistic(entry.Name, value.GetProperty("statistic").GetString()),
                    value.TryGetProperty("duration", out var duration)
                        ? ParseDuration(duration.GetString())
                        : null,
                    value.TryGetProperty("bytes", out var bytes)
                        ? ParseBytes(bytes.GetString())
                        : null,
                    value.TryGetProperty("minSamples", out var minSamples)
                        ? minSamples.GetInt32()
                        : 1,
                    ParseGate(entry.Name, value.GetProperty("gate").GetString())
                )
            );
        }

        return new PerformanceBudgets(budgets);
    }

    private PerformanceBudget Get(string name) =>
        _byName.TryGetValue(name, out var budget)
            ? budget
            : throw new InvalidDataException("data/catalogs/budgets.json has no budget " + name);

    private static BudgetStatistic ParseStatistic(string name, string? text) =>
        text switch
        {
            "p50" => BudgetStatistic.P50,
            "p95" => BudgetStatistic.P95,
            "max" => BudgetStatistic.Max,
            _ => throw new InvalidDataException(name + ": unknown statistic " + text),
        };

    private static BudgetGate ParseGate(string name, string? text) =>
        text switch
        {
            "everyRun" => BudgetGate.EveryRun,
            "touchLab" => BudgetGate.TouchLab,
            _ => throw new InvalidDataException(name + ": unknown gate " + text),
        };

    private static TimeSpan ParseDuration(string? text)
    {
        var match = DurationPattern().Match(text ?? string.Empty);
        if (!match.Success)
        {
            throw new InvalidDataException("Not a duration: " + text);
        }

        var amount = double.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "ms" => TimeSpan.FromMilliseconds(amount),
            "s" => TimeSpan.FromSeconds(amount),
            "min" => TimeSpan.FromMinutes(amount),
            "h" => TimeSpan.FromHours(amount),
            _ => TimeSpan.FromDays(amount),
        };
    }

    private static long ParseBytes(string? text)
    {
        var match = BytesPattern().Match(text ?? string.Empty);
        if (!match.Success)
        {
            throw new InvalidDataException("Not a size: " + text);
        }

        var amount = long.Parse(match.Groups["amount"].Value, CultureInfo.InvariantCulture);
        return match.Groups["unit"].Value switch
        {
            "B" => amount,
            "KiB" => amount * 1024,
            "MiB" => amount * 1024 * 1024,
            _ => amount * 1024 * 1024 * 1024,
        };
    }

    [GeneratedRegex(
        @"^(?<amount>(?:0|[1-9][0-9]*)(?:\.[0-9]+)?)(?<unit>ms|s|min|h|d)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex DurationPattern();

    [GeneratedRegex(
        @"^(?<amount>[1-9][0-9]*)(?<unit>B|KiB|MiB|GiB)$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex BytesPattern();
}
