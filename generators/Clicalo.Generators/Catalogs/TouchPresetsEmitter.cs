using System;
using System.Collections.Generic;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>Emits <c>Clicalo.Domain.Catalog.TouchPresets</c> from touch-presets.json (TAC-001).</summary>
internal static class TouchPresetsEmitter
{
    public const string FileName = "touch-presets.json";

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
        var catalog = Read(new CatalogReader(input.Primary, diagnostics));
        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }

        if (catalog is not null)
        {
            context.AddSource("Clicalo.Domain.Catalog.TouchPresets.g.cs", Emit(catalog));
        }
    }

    private static PresetCatalog? Read(CatalogReader reader)
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

        var presets = new List<Preset>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in reader.Array(root, "presets"))
        {
            if (node.Kind != JsonKind.Object)
            {
                reader.Structure(node, $"{FileName}: every preset must be an object.");
                continue;
            }

            var preset = ReadPreset(reader, node);
            if (preset is null)
            {
                continue;
            }

            if (!ids.Add(preset.Id))
            {
                reader.Report(
                    CatalogDiagnostics.DuplicateId,
                    node["id"]!,
                    "Touch preset",
                    preset.Id
                );
                continue;
            }

            presets.Add(preset);
        }

        var defaultNode = reader.Member(root, "default", JsonKind.String);
        Preset? defaultPreset = null;
        if (defaultNode is not null)
        {
            defaultPreset = presets.Find(p =>
                string.Equals(p.Id, defaultNode.StringValue, StringComparison.Ordinal)
            );
            if (defaultPreset is null)
            {
                reader.Structure(
                    defaultNode,
                    $"{FileName}: the default preset '{defaultNode.StringValue}' is not defined."
                );
            }
        }

        return defaultPreset is null ? null : new PresetCatalog(presets, defaultPreset);
    }

    private static Preset? ReadPreset(CatalogReader reader, JsonNode node)
    {
        var id = reader.String(node, "id");
        if (id is null)
        {
            return null;
        }

        if (!Identifiers.IsKebabCase(id))
        {
            reader.Report(
                CatalogDiagnostics.NonCanonicalId,
                node["id"]!,
                "Touch preset",
                id,
                "use lower-case words joined by hyphens"
            );
            return null;
        }

        var member = Identifiers.ToPascalCase(id);
        var problem = Identifiers.MemberNameProblem(
            member,
            "TouchPresets",
            "TouchPreset",
            "Default",
            "All",
            "Find"
        );
        if (problem is not null)
        {
            reader.Report(CatalogDiagnostics.InvalidCodeName, node["id"]!, member, problem);
            return null;
        }

        var debounceNode = reader.Required(node, "debounce");
        var minContactNode = reader.Required(node, "minContact");
        var hitSlopNode = reader.Member(node, "hitSlopPx", JsonKind.Number);
        var cancelMoveNode = reader.Member(node, "cancelMovePx", JsonKind.Number);
        var debounce = debounceNode is null
            ? null
            : Quantity.Ticks(reader, debounceNode, id + ".debounce");
        var minContact = minContactNode is null
            ? null
            : Quantity.Ticks(reader, minContactNode, id + ".minContact");
        var hitSlop = hitSlopNode is null
            ? null
            : reader.NonNegativeInteger(hitSlopNode, id + ".hitSlopPx");
        var cancelMove = cancelMoveNode is null
            ? null
            : reader.NonNegativeInteger(cancelMoveNode, id + ".cancelMovePx");
        if (debounce is null || minContact is null || hitSlop is null || cancelMove is null)
        {
            return null;
        }

        return new Preset(
            id,
            member,
            debounce.Value,
            hitSlop.Value,
            cancelMove.Value,
            minContact.Value
        );
    }

    private static string Emit(PresetCatalog catalog)
    {
        var writer = SourceWriter.ForFile("Clicalo.Domain.Catalog", "data/catalogs/" + FileName);
        writer.Summary(
            "Presets of the touch filter (TAC-001). Generated from data/catalogs/touch-presets.json."
        );
        writer.GeneratedCode();
        writer.Open("public static partial class TouchPresets");
        foreach (var preset in catalog.Presets)
        {
            writer.Summary("Preset «" + preset.Id + "».");
            writer.Line("public static TouchPreset " + preset.Member + " { get; } =");
            writer.Line(
                "    new(\""
                    + SourceWriter.EscapeString(preset.Id)
                    + "\", global::System.TimeSpan.FromTicks("
                    + Identifiers.Literal(preset.DebounceTicks)
                    + "L), "
                    + Identifiers.Literal(preset.HitSlopPx)
                    + ", "
                    + Identifiers.Literal(preset.CancelMovePx)
                    + ", global::System.TimeSpan.FromTicks("
                    + Identifiers.Literal(preset.MinContactTicks)
                    + "L));"
            );
            writer.Line();
        }

        writer.Summary("Preset applied until the user chooses another one.");
        writer.Line("public static TouchPreset Default => " + catalog.Default.Member + ";");
        writer.Line();
        writer.Summary("Every preset, in display order.");
        var members = new List<string>();
        foreach (var preset in catalog.Presets)
        {
            members.Add(preset.Member);
        }

        writer.Line(
            "public static global::System.Collections.Immutable.ImmutableArray<TouchPreset> All { get; } = ["
                + string.Join(", ", members)
                + "];"
        );
        writer.Line();
        writer.Summary("Finds a preset by its persisted identifier.");
        writer.Line("public static TouchPreset? Find(string id) =>");
        writer.Line("    id switch");
        writer.Line("    {");
        foreach (var preset in catalog.Presets)
        {
            writer.Line(
                "        \"" + SourceWriter.EscapeString(preset.Id) + "\" => " + preset.Member + ","
            );
        }

        writer.Line("        _ => null,");
        writer.Line("    };");
        writer.Close();
        return writer.ToString();
    }

    private sealed record PresetCatalog(List<Preset> Presets, Preset Default);

    private sealed record Preset(
        string Id,
        string Member,
        long DebounceTicks,
        long HitSlopPx,
        long CancelMovePx,
        long MinContactTicks
    );
}
