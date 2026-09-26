using System.Collections.Generic;
using Clicalo.Generators.Common;
using Microsoft.CodeAnalysis;

namespace Clicalo.Generators.Catalogs;

/// <summary>
/// Reads the members a generator needs from a parsed catalog and turns every problem into a located diagnostic.
/// The full shape is enforced by the JSON schemas in data/schemas (Data tests); the generator only checks what
/// it must rely on to emit correct code.
/// </summary>
internal sealed class CatalogReader
{
    private readonly CatalogFile _file;
    private readonly List<Diagnostic> _diagnostics;

    public CatalogReader(CatalogFile file, List<Diagnostic> diagnostics)
    {
        _file = file;
        _diagnostics = diagnostics;
    }

    public string FileName => _file.FileName;

    /// <summary>Parses the file, reporting CLCC001 at the offending position when it is not valid JSON.</summary>
    public JsonNode? Parse()
    {
        try
        {
            return MiniJson.Parse(_file.Text);
        }
        catch (JsonParseException ex)
        {
            _diagnostics.Add(
                Diagnostic.Create(
                    CatalogDiagnostics.InvalidJson,
                    GeneratorContext.At(_file, ex.Line, ex.Column),
                    _file.FileName,
                    ex.Message
                )
            );
            return null;
        }
    }

    public Location At(JsonNode node) => GeneratorContext.At(_file, node.Line, node.Column);

    public void Report(DiagnosticDescriptor descriptor, JsonNode node, params object[] arguments) =>
        _diagnostics.Add(Diagnostic.Create(descriptor, At(node), arguments));

    public void Structure(JsonNode node, string message) =>
        Report(CatalogDiagnostics.InvalidStructure, node, message);

    /// <summary>Returns the member when it exists with the expected kind; otherwise reports CLCC009.</summary>
    public JsonNode? Member(JsonNode owner, string name, JsonKind kind)
    {
        var member = owner[name];
        if (member is null)
        {
            Structure(owner, $"{_file.FileName}: missing member '{name}'.");
            return null;
        }

        if (member.Kind != kind)
        {
            Structure(member, $"{_file.FileName}: member '{name}' must be {Describe(kind)}.");
            return null;
        }

        return member;
    }

    /// <summary>Returns a member of any kind, reporting CLCC009 when it is missing.</summary>
    public JsonNode? Required(JsonNode owner, string name)
    {
        var member = owner[name];
        if (member is null)
        {
            Structure(owner, $"{_file.FileName}: missing member '{name}'.");
        }

        return member;
    }

    /// <summary>Returns an optional member, reporting CLCC009 only when it exists with another kind.</summary>
    public JsonNode? Optional(JsonNode owner, string name, JsonKind kind)
    {
        var member = owner[name];
        if (member is null)
        {
            return null;
        }

        if (member.Kind != kind)
        {
            Structure(member, $"{_file.FileName}: member '{name}' must be {Describe(kind)}.");
            return null;
        }

        return member;
    }

    public string? String(JsonNode owner, string name) =>
        Member(owner, name, JsonKind.String)?.StringValue;

    public IReadOnlyList<JsonNode> Array(JsonNode owner, string name) =>
        Member(owner, name, JsonKind.Array)?.Items ?? System.Array.Empty<JsonNode>();

    public IReadOnlyList<KeyValuePair<string, JsonNode>> Object(JsonNode owner, string name) =>
        Member(owner, name, JsonKind.Object)?.Members
        ?? System.Array.Empty<KeyValuePair<string, JsonNode>>();

    /// <summary>Reads a non-negative integer, reporting CLCC004 when negative and CLCC009 when not an integer.</summary>
    public long? NonNegativeInteger(JsonNode node, string what)
    {
        if (node.Kind != JsonKind.Number || !node.TryGetInt64(out var value))
        {
            Structure(node, $"{_file.FileName}: '{what}' must be an integer.");
            return null;
        }

        if (value < 0)
        {
            Report(CatalogDiagnostics.NegativeValue, node, what);
            return null;
        }

        return value;
    }

    private static string Describe(JsonKind kind) =>
        kind switch
        {
            JsonKind.Object => "an object",
            JsonKind.Array => "an array",
            JsonKind.String => "a string",
            JsonKind.Number => "a number",
            JsonKind.Boolean => "a boolean",
            _ => "null",
        };
}
