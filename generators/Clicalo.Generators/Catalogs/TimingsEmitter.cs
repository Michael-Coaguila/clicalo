using System;
using System.Collections.Generic;
using System.Globalization;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Emits <c>Clicalo.Domain.Timing.Timings</c> from timings.json: one nested class per group and one member per
/// entry, typed by the entry kind (TimeSpan, int, long, double, CountWindow, DurationRange). NFR-020.
/// </summary>
internal static class TimingsEmitter
{
    public const string FileName = "timings.json";

    private const string TimeSpan = "global::System.TimeSpan";

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

    public static void Execute(SourceProductionContext context, CatalogInput input)
    {
        if (!input.Enabled)
        {
            return;
        }

        if (input.Primary is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    CatalogDiagnostics.MissingCatalog,
                    Location.None,
                    input.PrimaryName
                )
            );
            return;
        }

        var diagnostics = new List<Diagnostic>();
        var groups = Read(new CatalogReader(input.Primary, diagnostics));
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }

        if (groups is not null)
        {
            context.AddSource("Clicalo.Domain.Timing.Timings.g.cs", Emit(groups));
        }
    }

    private static List<TimingGroup>? Read(CatalogReader reader)
    {
        var root = reader.Parse();
        if (root is null)
        {
            return null;
        }

        if (root.Kind != JsonKind.Object)
        {
            reader.Structure(root, $"{FileName}: the root must be an object.");
            return null;
        }

        var groups = new List<TimingGroup>();
        foreach (var group in reader.Object(root, "groups"))
        {
            if (!ValidName(reader, group.Value, group.Key, "Timings"))
            {
                continue;
            }

            if (group.Value.Kind != JsonKind.Object)
            {
                reader.Structure(
                    group.Value,
                    $"{FileName}: group '{group.Key}' must be an object."
                );
                continue;
            }

            var description = reader.String(group.Value, "description") ?? string.Empty;
            var entries = new List<TimingEntry>();
            foreach (var entry in reader.Object(group.Value, "entries"))
            {
                if (!ValidName(reader, entry.Value, entry.Key, group.Key))
                {
                    continue;
                }

                var parsed = ReadEntry(reader, group.Key + "." + entry.Key, entry.Key, entry.Value);
                if (parsed is not null)
                {
                    entries.Add(parsed);
                }
            }

            groups.Add(new TimingGroup(group.Key, description, entries));
        }

        return groups;
    }

    private static bool ValidName(
        CatalogReader reader,
        JsonNode node,
        string name,
        string enclosing
    )
    {
        var problem = Identifiers.MemberNameProblem(name);
        if (problem is null && string.Equals(name, enclosing, StringComparison.Ordinal))
        {
            problem = "it repeats the name of its enclosing class";
        }

        if (problem is not null)
        {
            reader.Report(CatalogDiagnostics.InvalidCodeName, node, name, problem);
            return false;
        }

        return true;
    }

    private static TimingEntry? ReadEntry(
        CatalogReader reader,
        string path,
        string name,
        JsonNode node
    )
    {
        if (node.Kind != JsonKind.Object)
        {
            reader.Structure(node, $"{FileName}: entry '{path}' must be an object.");
            return null;
        }

        var description = reader.String(node, "description");
        string? kind = null;
        foreach (var candidate in Kinds)
        {
            if (node[candidate] is null)
            {
                continue;
            }

            if (kind is not null)
            {
                reader.Structure(
                    node,
                    $"{FileName}: entry '{path}' has both '{kind}' and '{candidate}'; keep exactly one value."
                );
                return null;
            }

            kind = candidate;
        }

        if (kind is null)
        {
            reader.Structure(
                node,
                $"{FileName}: entry '{path}' has no value ({string.Join(", ", Kinds)})."
            );
            return null;
        }

        var value = node[kind]!;
        var declaration = kind switch
        {
            "duration" => Duration(reader, value, path, name),
            "durations" => Durations(reader, value, path, name),
            "px" or "count" => Integer(reader, value, path, name),
            "ratio" => Ratio(reader, value, path, name),
            "bytes" => Bytes(reader, value, path, name),
            "countWindow" => CountWindow(reader, value, path, name),
            _ => DurationRange(reader, value, path, name),
        };

        return declaration is null || description is null
            ? null
            : new TimingEntry(declaration, description, References(node), Literal(value));
    }

    private static string? Duration(CatalogReader reader, JsonNode value, string path, string name)
    {
        var ticks = Quantity.Ticks(reader, value, path);
        return ticks is null
            ? null
            : $"public static readonly {TimeSpan} {name} = {FromTicks(ticks.Value)};";
    }

    private static string? Durations(CatalogReader reader, JsonNode value, string path, string name)
    {
        if (value.Kind != JsonKind.Array || value.Items.Count == 0)
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' must be a non-empty array of durations."
            );
            return null;
        }

        var items = new List<string>();
        foreach (var item in value.Items)
        {
            var ticks = Quantity.Ticks(reader, item, path);
            if (ticks is null)
            {
                return null;
            }

            items.Add(FromTicks(ticks.Value));
        }

        return $"public static global::System.Collections.Immutable.ImmutableArray<{TimeSpan}> {name} {{ get; }} = [{string.Join(", ", items)}];";
    }

    private static string? Integer(CatalogReader reader, JsonNode value, string path, string name)
    {
        var number = reader.NonNegativeInteger(value, path);
        if (number is null)
        {
            return null;
        }

        if (number.Value > int.MaxValue)
        {
            reader.Structure(value, $"{FileName}: '{path}' does not fit in a 32-bit integer.");
            return null;
        }

        return $"public const int {name} = {Identifiers.Literal(number.Value)};";
    }

    private static string? Ratio(CatalogReader reader, JsonNode value, string path, string name)
    {
        if (value.Kind != JsonKind.Number)
        {
            reader.Structure(value, $"{FileName}: '{path}' must be a number.");
            return null;
        }

        if (value.NumberValue < 0)
        {
            reader.Report(CatalogDiagnostics.NegativeValue, value, path);
            return null;
        }

        if (value.NumberValue == 0 || double.IsInfinity(value.NumberValue))
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' must be a finite number greater than zero."
            );
            return null;
        }

        return $"public const double {name} = {value.NumberValue.ToString("R", CultureInfo.InvariantCulture)}d;";
    }

    private static string? Bytes(CatalogReader reader, JsonNode value, string path, string name)
    {
        var bytes = Quantity.Bytes(reader, value, path);
        return bytes is null
            ? null
            : $"public const long {name} = {Identifiers.Literal(bytes.Value)}L;";
    }

    private static string? CountWindow(
        CatalogReader reader,
        JsonNode value,
        string path,
        string name
    )
    {
        if (value.Kind != JsonKind.Object)
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' must be an object with 'count' and 'window'."
            );
            return null;
        }

        var countNode = reader.Member(value, "count", JsonKind.Number);
        var windowNode = value["window"];
        if (windowNode is null)
        {
            reader.Structure(value, $"{FileName}: '{path}' is missing 'window'.");
        }

        var count = countNode is null
            ? null
            : reader.NonNegativeInteger(countNode, path + ".count");
        var window = windowNode is null
            ? null
            : Quantity.Ticks(reader, windowNode, path + ".window");
        if (count is null || window is null)
        {
            return null;
        }

        if (count.Value == 0 || count.Value > int.MaxValue || window.Value == 0)
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' needs a positive count and a positive window."
            );
            return null;
        }

        return $"public static readonly global::Clicalo.Domain.Timing.CountWindow {name} = new({Identifiers.Literal(count.Value)}, {FromTicks(window.Value)});";
    }

    private static string? DurationRange(
        CatalogReader reader,
        JsonNode value,
        string path,
        string name
    )
    {
        if (value.Kind != JsonKind.Object)
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' must be an object with 'min', 'max' and 'step'."
            );
            return null;
        }

        long? Part(string part)
        {
            var node = value[part];
            if (node is null)
            {
                reader.Structure(value, $"{FileName}: '{path}' is missing '{part}'.");
                return null;
            }

            return Quantity.Ticks(reader, node, path + "." + part);
        }

        var min = Part("min");
        var max = Part("max");
        var step = Part("step");
        if (min is null || max is null || step is null)
        {
            return null;
        }

        if (min.Value > max.Value || step.Value == 0)
        {
            reader.Structure(
                value,
                $"{FileName}: '{path}' needs min <= max and a step greater than zero."
            );
            return null;
        }

        return $"public static readonly global::Clicalo.Domain.Timing.DurationRange {name} = new({FromTicks(min.Value)}, {FromTicks(max.Value)}, {FromTicks(step.Value)});";
    }

    private static string References(JsonNode entry)
    {
        var parts = new List<string>();
        if (entry["req"] is { Kind: JsonKind.Array } requirements)
        {
            foreach (var item in requirements.Items)
            {
                if (item.StringValue is { } id)
                {
                    parts.Add(id);
                }
            }
        }

        if (entry["source"]?.StringValue is { } source)
        {
            parts.Add(source);
        }

        return string.Join("; ", parts);
    }

    /// <summary>Compact rendering of the data value for the documentation comment.</summary>
    private static string Literal(JsonNode value) =>
        value.Kind switch
        {
            JsonKind.String => value.StringValue ?? string.Empty,
            JsonKind.Number => value.NumberText ?? string.Empty,
            JsonKind.Array => string.Join(", ", ItemLiterals(value)),
            JsonKind.Object => string.Join(", ", MemberLiterals(value)),
            _ => string.Empty,
        };

    private static IEnumerable<string> ItemLiterals(JsonNode value)
    {
        foreach (var item in value.Items)
        {
            yield return Literal(item);
        }
    }

    private static IEnumerable<string> MemberLiterals(JsonNode value)
    {
        foreach (var member in value.Members)
        {
            yield return member.Key + " " + Literal(member.Value);
        }
    }

    private static string FromTicks(long ticks) =>
        $"{TimeSpan}.FromTicks({Identifiers.Literal(ticks)}L)";

    private static string Emit(List<TimingGroup> groups)
    {
        var writer = SourceWriter.ForFile("Clicalo.Domain.Timing", "data/catalogs/" + FileName);
        writer.Summary(
            "Every named time and threshold of the product, defined once in data/catalogs/timings.json (NFR-020). Never write these values as literals."
        );
        writer.GeneratedCode();
        writer.Open("public static partial class Timings");
        var firstGroup = true;
        foreach (var group in groups)
        {
            if (!firstGroup)
            {
                writer.Line();
            }

            firstGroup = false;
            writer.Summary(group.Description);
            writer.Open("public static partial class " + group.Name);
            var firstEntry = true;
            foreach (var entry in group.Entries)
            {
                if (!firstEntry)
                {
                    writer.Line();
                }

                firstEntry = false;
                var references =
                    entry.References.Length == 0 ? string.Empty : "; " + entry.References;
                writer.Summary(
                    entry.Description.TrimEnd('.') + " (" + entry.Literal + references + ")."
                );
                writer.Line(entry.Declaration);
            }

            writer.Close();
        }

        writer.Close();
        return writer.ToString();
    }

    private sealed record TimingGroup(string Name, string Description, List<TimingEntry> Entries);

    private sealed record TimingEntry(
        string Declaration,
        string Description,
        string References,
        string Literal
    );
}
