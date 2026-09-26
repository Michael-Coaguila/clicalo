using System;
using System.Collections.Generic;
using System.Globalization;

namespace Clicalo.Generators.Common;

/// <summary>
/// Immutable JSON value with its source position, so generators can report data errors
/// at the exact line and column of the offending file (data errors become compile errors).
/// </summary>
/// <remarks>
/// Generators cannot safely depend on System.Text.Json (the compiler host decides which version is loaded),
/// so every generator shares this small RFC 8259 reader. Object member order is preserved.
/// </remarks>
internal sealed class JsonNode
{
    private static readonly IReadOnlyList<JsonNode> EmptyItems = System.Array.Empty<JsonNode>();
    private static readonly IReadOnlyList<KeyValuePair<string, JsonNode>> EmptyMembers =
        System.Array.Empty<KeyValuePair<string, JsonNode>>();

    private JsonNode(JsonKind kind, int line, int column)
    {
        Kind = kind;
        Line = line;
        Column = column;
        Items = EmptyItems;
        Members = EmptyMembers;
    }

    public JsonKind Kind { get; }

    /// <summary>1-based line of the first character of the value.</summary>
    public int Line { get; }

    /// <summary>1-based column of the first character of the value.</summary>
    public int Column { get; }

    public bool BooleanValue { get; private set; }

    public double NumberValue { get; private set; }

    /// <summary>Original lexeme of a number, useful to detect integers without floating point loss.</summary>
    public string? NumberText { get; private set; }

    public string? StringValue { get; private set; }

    public IReadOnlyList<JsonNode> Items { get; private set; }

    public IReadOnlyList<KeyValuePair<string, JsonNode>> Members { get; private set; }

    /// <summary>Returns the member with the given name, or <c>null</c> when absent or when this is not an object.</summary>
    public JsonNode? this[string name]
    {
        get
        {
            foreach (var member in Members)
            {
                if (string.Equals(member.Key, name, StringComparison.Ordinal))
                {
                    return member.Value;
                }
            }

            return null;
        }
    }

    public bool TryGetInt64(out long value)
    {
        value = 0;
        return Kind == JsonKind.Number
            && NumberText is not null
            && long.TryParse(
                NumberText,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out value
            );
    }

    internal static JsonNode Null(int line, int column) => new(JsonKind.Null, line, column);

    internal static JsonNode Bool(bool value, int line, int column) =>
        new(JsonKind.Boolean, line, column) { BooleanValue = value };

    internal static JsonNode Number(double value, string text, int line, int column) =>
        new(JsonKind.Number, line, column) { NumberValue = value, NumberText = text };

    internal static JsonNode String(string value, int line, int column) =>
        new(JsonKind.String, line, column) { StringValue = value };

    internal static JsonNode Array(List<JsonNode> items, int line, int column) =>
        new(JsonKind.Array, line, column) { Items = items };

    internal static JsonNode Object(
        List<KeyValuePair<string, JsonNode>> members,
        int line,
        int column
    ) => new(JsonKind.Object, line, column) { Members = members };
}
