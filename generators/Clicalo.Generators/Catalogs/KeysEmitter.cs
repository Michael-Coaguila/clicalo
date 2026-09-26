using System;
using System.Collections.Generic;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Emits <c>KeyGroup</c>, <c>KeyIds</c> and <c>KeyDefinitions</c> into <c>Clicalo.Domain.Keys</c> from keys.json,
/// and checks that keys.win32.json maps exactly the same keys.
/// </summary>
internal static class KeysEmitter
{
    public const string FileName = "keys.json";
    public const string Win32FileName = "keys.win32.json";
    private const string Namespace = "Clicalo.Domain.Keys";

    private static readonly HashSet<string> Modifiers = new(StringComparer.Ordinal)
    {
        "ctrl",
        "alt",
        "shift",
        "win",
    };

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
        var keysReader = new CatalogReader(input.Primary, diagnostics);
        var catalog = Read(keysReader);
        if (catalog is not null && input.Companion is not null)
        {
            CheckWin32(keysReader, new CatalogReader(input.Companion, diagnostics), catalog);
        }

        foreach (var diagnostic in diagnostics)
        {
            context.ReportDiagnostic(diagnostic);
        }

        if (catalog is null)
        {
            return;
        }

        context.AddSource("Clicalo.Domain.Keys.KeyGroup.g.cs", EmitGroups(catalog));
        context.AddSource("Clicalo.Domain.Keys.KeyIds.g.cs", EmitKeyIds(catalog));
        context.AddSource("Clicalo.Domain.Keys.KeyDefinitions.g.cs", EmitDefinitions(catalog));
    }

    private static KeyCatalog? Read(CatalogReader reader)
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

        var catalog = new KeyCatalog();
        var groupIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in reader.Array(root, "groups"))
        {
            var id = node.Kind == JsonKind.Object ? reader.String(node, "id") : null;
            if (id is null)
            {
                if (node.Kind != JsonKind.Object)
                {
                    reader.Structure(node, $"{FileName}: every group must be an object.");
                }

                continue;
            }

            var idNode = node["id"]!;
            if (!Identifiers.IsLowerWord(id))
            {
                reader.Report(
                    CatalogDiagnostics.NonCanonicalId,
                    idNode,
                    "Key group",
                    id,
                    "use one lower-case ASCII word"
                );
                continue;
            }

            if (!groupIds.Add(id))
            {
                reader.Report(CatalogDiagnostics.DuplicateId, idNode, "Key group", id);
                continue;
            }

            var member = Identifiers.ToPascalCase(id);
            var problem = Identifiers.MemberNameProblem(member);
            if (problem is not null)
            {
                reader.Report(CatalogDiagnostics.InvalidCodeName, idNode, member, problem);
                continue;
            }

            catalog.Groups.Add(new KeyGroupEntry(id, member));
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var codeNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in reader.Array(root, "keys"))
        {
            if (node.Kind != JsonKind.Object)
            {
                reader.Structure(node, $"{FileName}: every key must be an object.");
                continue;
            }

            var key = ReadKey(reader, node, groupIds);
            if (key is null)
            {
                continue;
            }

            if (!ids.Add(key.Id))
            {
                reader.Report(CatalogDiagnostics.DuplicateId, key.IdNode, "Key id", key.Id);
                continue;
            }

            if (!codeNames.Add(key.CodeName))
            {
                reader.Report(
                    CatalogDiagnostics.DuplicateId,
                    node["codeName"]!,
                    "Key code name",
                    key.CodeName
                );
                continue;
            }

            catalog.Keys.Add(key);
        }

        ResolveSides(reader, catalog);
        return catalog;
    }

    private static KeyEntry? ReadKey(CatalogReader reader, JsonNode node, HashSet<string> groupIds)
    {
        var id = reader.String(node, "id");
        var codeName = reader.String(node, "codeName");
        var group = reader.String(node, "group");
        var label = reader.Member(node, "label", JsonKind.Object);
        var english = label is null ? null : reader.String(label, "en");
        if (id is null || codeName is null || group is null || english is null)
        {
            return null;
        }

        var valid = true;
        var idProblem = Identifiers.KeyIdProblem(id);
        if (idProblem is not null)
        {
            reader.Report(CatalogDiagnostics.NonCanonicalId, node["id"]!, "Key id", id, idProblem);
            valid = false;
        }

        var nameProblem = Identifiers.MemberNameProblem(codeName, "KeyIds", "KeyId", "All");
        if (nameProblem is not null)
        {
            reader.Report(
                CatalogDiagnostics.InvalidCodeName,
                node["codeName"]!,
                codeName,
                nameProblem
            );
            valid = false;
        }

        if (!groupIds.Contains(group))
        {
            reader.Structure(
                node["group"]!,
                $"{FileName}: key '{id}' uses the undeclared group '{group}'."
            );
            valid = false;
        }

        var modifier = reader.Optional(node, "modifier", JsonKind.String)?.StringValue;
        if (modifier is not null && !Modifiers.Contains(modifier))
        {
            reader.Structure(
                node["modifier"]!,
                $"{FileName}: key '{id}' has the unknown modifier '{modifier}'."
            );
            valid = false;
        }

        var side = reader.Optional(node, "side", JsonKind.String)?.StringValue;
        if (
            side is not null
            && !string.Equals(side, "left", StringComparison.Ordinal)
            && !string.Equals(side, "right", StringComparison.Ordinal)
        )
        {
            reader.Structure(
                node["side"]!,
                $"{FileName}: key '{id}' has the unknown side '{side}' (left or right)."
            );
            valid = false;
        }

        var sideOf = reader.Optional(node, "sideOf", JsonKind.String)?.StringValue;
        if ((side is null) != (sideOf is null))
        {
            reader.Structure(
                node,
                $"{FileName}: key '{id}' must declare both 'side' and 'sideOf', or neither."
            );
            valid = false;
        }

        return valid
            ? new KeyEntry(
                id,
                codeName,
                Identifiers.ToPascalCase(group),
                english,
                modifier,
                side,
                sideOf,
                node["id"]!,
                node
            )
            : null;
    }

    private static void ResolveSides(CatalogReader reader, KeyCatalog catalog)
    {
        var byId = new Dictionary<string, KeyEntry>(StringComparer.Ordinal);
        foreach (var key in catalog.Keys)
        {
            byId[key.Id] = key;
        }

        var resolved = new List<KeyEntry>(catalog.Keys.Count);
        foreach (var key in catalog.Keys)
        {
            if (key.SideOf is null)
            {
                resolved.Add(key);
                continue;
            }

            if (!byId.TryGetValue(key.SideOf, out var baseKey))
            {
                reader.Structure(
                    key.Node["sideOf"]!,
                    $"{FileName}: key '{key.Id}' is a side of the unknown key '{key.SideOf}'."
                );
                continue;
            }

            if (
                baseKey.Modifier is null
                || baseKey.SideOf is not null
                || !string.Equals(baseKey.Modifier, key.Modifier, StringComparison.Ordinal)
            )
            {
                reader.Structure(
                    key.Node["sideOf"]!,
                    $"{FileName}: key '{key.Id}' must be a side of an any-side modifier of the same family, not of '{key.SideOf}'."
                );
                continue;
            }

            resolved.Add(key with { BaseCodeName = baseKey.CodeName });
        }

        catalog.Keys.Clear();
        catalog.Keys.AddRange(resolved);
    }

    private static void CheckWin32(
        CatalogReader keysReader,
        CatalogReader reader,
        KeyCatalog catalog
    )
    {
        var root = reader.Parse();
        if (root is null)
        {
            return;
        }

        if (root.Kind != JsonKind.Object)
        {
            reader.Structure(root, $"{Win32FileName}: the root must be an object.");
            return;
        }

        var members = reader.Object(root, "keys");
        var mapped = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in members)
        {
            mapped.Add(member.Key);
        }

        var known = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in catalog.Keys)
        {
            known.Add(key.Id);
            if (!mapped.Contains(key.Id))
            {
                // Reported on keys.json, where the key that cannot be sent is declared.
                keysReader.Report(CatalogDiagnostics.KeyWithoutWin32Mapping, key.IdNode, key.Id);
            }
        }

        foreach (var member in members)
        {
            if (!known.Contains(member.Key))
            {
                reader.Report(
                    CatalogDiagnostics.Win32MappingForUnknownKey,
                    member.Value,
                    member.Key
                );
            }
        }
    }

    private static string EmitGroups(KeyCatalog catalog)
    {
        var writer = SourceWriter.ForFile(Namespace, "data/catalogs/" + FileName);
        writer.Summary(
            "Picker group of a key (EDI-008), in display order. Generated from data/catalogs/keys.json."
        );
        writer.GeneratedCode();
        writer.Open("public enum KeyGroup");
        foreach (var group in catalog.Groups)
        {
            writer.Summary("Group «" + group.Id + "».");
            writer.Line(group.Member + ",");
        }

        writer.Close();
        return writer.ToString();
    }

    private static string EmitKeyIds(KeyCatalog catalog)
    {
        var writer = SourceWriter.ForFile(Namespace, "data/catalogs/" + FileName);
        writer.Summary(
            "Every key of the catalog, in picker order. Generated from data/catalogs/keys.json."
        );
        writer.GeneratedCode();
        writer.Open("public static partial class KeyIds");
        foreach (var key in catalog.Keys)
        {
            writer.Summary(key.EnglishLabel + " (" + key.Id + ", group " + key.GroupMember + ").");
            writer.Line(
                "public static readonly KeyId "
                    + key.CodeName
                    + " = new(\""
                    + SourceWriter.EscapeString(key.Id)
                    + "\");"
            );
            writer.Line();
        }

        writer.Summary("Every key of the catalog, in picker order.");
        writer.Line(
            "public static global::System.Collections.Immutable.ImmutableArray<KeyId> All { get; } ="
        );
        writer.Line("[");
        foreach (var key in catalog.Keys)
        {
            writer.Line("    " + key.CodeName + ",");
        }

        writer.Line("];");
        writer.Close();
        return writer.ToString();
    }

    private static string EmitDefinitions(KeyCatalog catalog)
    {
        var writer = SourceWriter.ForFile(Namespace, "data/catalogs/" + FileName);
        writer.Summary(
            "Group, modifier family and side of every catalog key. Generated from data/catalogs/keys.json."
        );
        writer.GeneratedCode();
        writer.Open("public static partial class KeyDefinitions");
        writer.Summary("Every key definition, in picker order.");
        writer.Line(
            "public static global::System.Collections.Immutable.ImmutableArray<KeyDefinition> All { get; } ="
        );
        writer.Line("[");
        foreach (var key in catalog.Keys)
        {
            var modifier = key.Modifier is null
                ? "null"
                : "ModifierKind." + Identifiers.ToPascalCase(key.Modifier);
            var side = key.Side is null
                ? "KeySide.Any"
                : "KeySide." + Identifiers.ToPascalCase(key.Side);
            var baseKey = key.BaseCodeName is null ? "null" : "KeyIds." + key.BaseCodeName;
            writer.Line(
                "    new KeyDefinition(KeyIds."
                    + key.CodeName
                    + ", KeyGroup."
                    + key.GroupMember
                    + ", "
                    + modifier
                    + ", "
                    + side
                    + ", "
                    + baseKey
                    + "),"
            );
        }

        writer.Line("];");
        writer.Line();
        writer.Line(
            "private static readonly global::System.Collections.Frozen.FrozenDictionary<KeyId, KeyDefinition> ById ="
        );
        writer.Line(
            "    global::System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary(All, static definition => definition.Id);"
        );
        writer.Line();
        writer.Summary("Finds the definition of a catalog key.");
        writer.Line("public static bool TryGet(");
        writer.Line("    KeyId id,");
        writer.Line(
            "    [global::System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out KeyDefinition? definition"
        );
        writer.Line(") => ById.TryGetValue(id, out definition);");
        writer.Close();
        return writer.ToString();
    }

    private sealed class KeyCatalog
    {
        public List<KeyGroupEntry> Groups { get; } = [];

        public List<KeyEntry> Keys { get; } = [];
    }

    private sealed record KeyGroupEntry(string Id, string Member);

    private sealed record KeyEntry(
        string Id,
        string CodeName,
        string GroupMember,
        string EnglishLabel,
        string? Modifier,
        string? Side,
        string? SideOf,
        JsonNode IdNode,
        JsonNode Node
    )
    {
        public string? BaseCodeName { get; init; }
    }
}
