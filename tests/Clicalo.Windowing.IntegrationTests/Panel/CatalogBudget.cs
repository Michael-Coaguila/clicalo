using System.Globalization;
using System.IO;
using System.Text.Json;
using Clicalo.TestKit;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// A duration budget of <c>data/catalogs/budgets.json</c> (blueprint §10.3, NFR-001) as a desktop test reads it: the
/// numbers live only in the catalog, whose shape <c>Clicalo.Data.Tests</c> validates against its schema.
/// </summary>
/// <param name="Name">The budget's key.</param>
/// <param name="Percentile">The nearest-rank percentile of its statistic (0.5, 0.95; 1 for the maximum).</param>
/// <param name="Limit">The duration the statistic may reach.</param>
/// <param name="MinSamples">The fewest samples the statistic may be computed from.</param>
internal sealed record CatalogBudget(string Name, double Percentile, TimeSpan Limit, int MinSamples)
{
    /// <summary>Reads the duration budget <paramref name="name"/>.</summary>
    public static CatalogBudget Read(string name)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "budgets.json"))
        );
        var budget = document.RootElement.GetProperty("budgets").GetProperty(name);
        var statistic = budget.GetProperty("statistic").GetString();
        var duration = budget.GetProperty("duration").GetString() ?? string.Empty;
        return new CatalogBudget(
            name,
            statistic switch
            {
                "p50" => 0.5,
                "p95" => 0.95,
                "max" => 1,
                _ => throw new FormatException(
                    "Unknown statistic '" + statistic + "' of " + name + "."
                ),
            },
            ParseDuration(name, duration),
            budget.TryGetProperty("minSamples", out var samples) ? samples.GetInt32() : 1
        );
    }

    /// <summary>The nearest-rank statistic of <paramref name="values"/>.</summary>
    public TimeSpan Of(IReadOnlyCollection<TimeSpan> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var sorted = values.Order().ToList();
        var rank = (int)Math.Ceiling(Percentile * sorted.Count);
        return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
    }

    private static TimeSpan ParseDuration(string name, string duration) =>
        duration switch
        {
            _ when duration.EndsWith("ms", StringComparison.Ordinal) => TimeSpan.FromMilliseconds(
                double.Parse(duration[..^2], CultureInfo.InvariantCulture)
            ),
            _ when duration.EndsWith('s') => TimeSpan.FromSeconds(
                double.Parse(duration[..^1], CultureInfo.InvariantCulture)
            ),
            _ => throw new FormatException("Unknown duration '" + duration + "' of " + name + "."),
        };
}
