using System;
using System.Collections.Generic;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Emits <c>PanelSize</c> and <c>PanelSizes</c> into <c>Clicalo.Domain.Catalog</c> from sizes.json (docs/04).
/// Every numeric leaf becomes an init property named after its path (<c>tile.widthPx</c> → <c>TileWidthPx</c>);
/// the hand-written <c>SizeMetrics</c> and <c>LayoutMetrics</c> declare them as required, so the compiler
/// rejects any drift between the file and the types.
/// </summary>
internal static class SizesEmitter
{
    public const string FileName = "sizes.json";
    private const string Namespace = "Clicalo.Domain.Catalog";

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

        if (catalog is null)
        {
            return;
        }

        context.AddSource("Clicalo.Domain.Catalog.PanelSize.g.cs", EmitEnum(catalog));
        context.AddSource("Clicalo.Domain.Catalog.PanelSizes.g.cs", EmitSizes(catalog));
    }

    private static SizesCatalog? Read(CatalogReader reader)
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

        var catalog = new SizesCatalog();
        var layout = reader.Member(root, "layout", JsonKind.Object);
        if (layout is not null)
        {
            Flatten(reader, layout, string.Empty, "layout", catalog.Layout);
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in reader.Array(root, "sizes"))
        {
            if (node.Kind != JsonKind.Object)
            {
                reader.Structure(node, $"{FileName}: every size must be an object.");
                continue;
            }

            var id = reader.String(node, "id");
            if (id is null)
            {
                continue;
            }

            var problem = Identifiers.MemberNameProblem(
                id,
                "PanelSizes",
                "PanelSize",
                "SizeMetrics",
                "LayoutMetrics",
                "Layout",
                "All",
                "Get"
            );
            if (problem is not null)
            {
                reader.Report(CatalogDiagnostics.InvalidCodeName, node["id"]!, id, problem);
                continue;
            }

            if (!ids.Add(id))
            {
                reader.Report(CatalogDiagnostics.DuplicateId, node["id"]!, "Size", id);
                continue;
            }

            var size = new SizeEntry(id);
            foreach (var member in node.Members)
            {
                if (
                    string.Equals(member.Key, "id", StringComparison.Ordinal)
                    || string.Equals(member.Key, "labelKey", StringComparison.Ordinal)
                )
                {
                    continue;
                }

                FlattenMember(reader, member, string.Empty, "sizes." + id, size.Properties);
            }

            catalog.Sizes.Add(size);
        }

        return catalog;
    }

    private static void Flatten(
        CatalogReader reader,
        JsonNode node,
        string prefix,
        string path,
        List<SizeProperty> output
    )
    {
        foreach (var member in node.Members)
        {
            FlattenMember(reader, member, prefix, path, output);
        }
    }

    private static void FlattenMember(
        CatalogReader reader,
        KeyValuePair<string, JsonNode> member,
        string prefix,
        string path,
        List<SizeProperty> output
    )
    {
        var name = prefix + Identifiers.ToPascalCase(member.Key);
        var memberPath = path + "." + member.Key;
        var value = member.Value;
        switch (value.Kind)
        {
            case JsonKind.Object:
                Flatten(reader, value, name, memberPath, output);
                return;
            case JsonKind.Number:
                if (value.NumberValue < 0)
                {
                    reader.Report(CatalogDiagnostics.NegativeValue, value, memberPath);
                    return;
                }

                var problem = Identifiers.PascalCaseProblem(name);
                if (problem is not null)
                {
                    reader.Report(CatalogDiagnostics.InvalidCodeName, value, name, problem);
                    return;
                }

                output.Add(new SizeProperty(name, value.NumberText ?? "0"));
                return;
            default:
                reader.Structure(
                    value,
                    $"{FileName}: '{memberPath}' must be a number or an object of numbers."
                );
                return;
        }
    }

    private static string EmitEnum(SizesCatalog catalog)
    {
        var writer = SourceWriter.ForFile(Namespace, "data/catalogs/" + FileName);
        writer.Summary("Panel size (docs/04). Generated from data/catalogs/sizes.json.");
        writer.GeneratedCode();
        writer.Open("public enum PanelSize");
        foreach (var size in catalog.Sizes)
        {
            writer.Summary("Size " + size.Id + ".");
            writer.Line(size.Id + ",");
        }

        writer.Close();
        return writer.ToString();
    }

    private static string EmitSizes(SizesCatalog catalog)
    {
        var writer = SourceWriter.ForFile(Namespace, "data/catalogs/" + FileName);
        writer.Summary(
            "Measures of every panel size and of the size-independent layout. Generated from data/catalogs/sizes.json."
        );
        writer.GeneratedCode();
        writer.Open("public static partial class PanelSizes");
        writer.Summary("Size-independent layout measures.");
        writer.Line("public static LayoutMetrics Layout { get; } =");
        writer.Line("    new()");
        writer.Line("    {");
        foreach (var property in catalog.Layout)
        {
            writer.Line("        " + property.Name + " = " + property.Literal + ",");
        }

        writer.Line("    };");
        foreach (var size in catalog.Sizes)
        {
            writer.Line();
            writer.Summary("Measures of size " + size.Id + ".");
            writer.Line("public static SizeMetrics " + size.Id + " { get; } =");
            writer.Line("    new()");
            writer.Line("    {");
            writer.Line("        Size = PanelSize." + size.Id + ",");
            foreach (var property in size.Properties)
            {
                writer.Line("        " + property.Name + " = " + property.Literal + ",");
            }

            writer.Line("    };");
        }

        writer.Line();
        writer.Summary("Every size, in catalog order.");
        writer.Line(
            "public static global::System.Collections.Immutable.ImmutableArray<SizeMetrics> All { get; } = ["
                + string.Join(", ", Ids(catalog))
                + "];"
        );
        writer.Line();
        writer.Summary("Measures of the given size.");
        writer.Line("public static SizeMetrics Get(PanelSize size) =>");
        writer.Line("    size switch");
        writer.Line("    {");
        foreach (var size in catalog.Sizes)
        {
            writer.Line("        PanelSize." + size.Id + " => " + size.Id + ",");
        }

        writer.Line(
            "        _ => throw new global::System.ArgumentOutOfRangeException(nameof(size), size, null),"
        );
        writer.Line("    };");
        writer.Close();
        return writer.ToString();
    }

    private static IEnumerable<string> Ids(SizesCatalog catalog)
    {
        foreach (var size in catalog.Sizes)
        {
            yield return size.Id;
        }
    }

    private sealed class SizesCatalog
    {
        public List<SizeProperty> Layout { get; } = [];

        public List<SizeEntry> Sizes { get; } = [];
    }

    private sealed class SizeEntry(string id)
    {
        public string Id { get; } = id;

        public List<SizeProperty> Properties { get; } = [];
    }

    /// <param name="Name">Property name built from the JSON path.</param>
    /// <param name="Literal">Number exactly as written in the file (a valid C# literal).</param>
    private sealed record SizeProperty(string Name, string Literal);
}
