using System.Collections.Generic;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>Shape checks for the token files. Members whose name starts with <c>$</c> are comments.</summary>
internal static class JsonShape
{
    /// <summary>The member <paramref name="name"/> of <paramref name="parent"/>, required to be of <paramref name="kind"/>.</summary>
    public static JsonNode? Required(
        TokenDocument document,
        JsonNode parent,
        string name,
        JsonKind kind,
        TokenIssues issues
    )
    {
        var node = parent[name];
        if (node is null)
        {
            issues.Malformed(document, parent, "the member '" + name + "' is required.");
            return null;
        }

        return Expect(document, node, kind, "'" + name + "'", issues) ? node : null;
    }

    /// <summary>Like <see cref="Required"/>, but an absent member is not an error.</summary>
    public static JsonNode? Optional(
        TokenDocument document,
        JsonNode parent,
        string name,
        JsonKind kind,
        TokenIssues issues
    )
    {
        var node = parent[name];
        return node is not null && Expect(document, node, kind, "'" + name + "'", issues)
            ? node
            : null;
    }

    public static bool Expect(
        TokenDocument document,
        JsonNode node,
        JsonKind kind,
        string what,
        TokenIssues issues
    )
    {
        if (node.Kind == kind)
        {
            return true;
        }

        issues.Malformed(document, node, what + " must be " + Describe(kind) + ".");
        return false;
    }

    /// <summary>Members that carry data (skips <c>$comment</c> and friends).</summary>
    public static IEnumerable<KeyValuePair<string, JsonNode>> DataMembers(JsonNode node)
    {
        foreach (var member in node.Members)
        {
            if (!member.Key.StartsWith("$", System.StringComparison.Ordinal))
            {
                yield return member;
            }
        }
    }

    /// <summary>Data keys are camelCase identifiers: <c>[a-z][A-Za-z0-9]*</c>.</summary>
    public static bool IsIdentifier(string key)
    {
        if (key.Length == 0 || key[0] < 'a' || key[0] > 'z')
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Generated C# names are PascalCase identifiers: <c>[A-Z][A-Za-z0-9]*</c>.</summary>
    public static bool IsPascalIdentifier(string name) =>
        name.Length > 0
        && name[0] is >= 'A' and <= 'Z'
        && IsIdentifier(char.ToLowerInvariant(name[0]) + name.Substring(1));

    public static string ToPascalCase(string key) =>
        char.ToUpperInvariant(key[0]) + key.Substring(1);

    private static string Describe(JsonKind kind) =>
        kind switch
        {
            JsonKind.Object => "an object",
            JsonKind.Array => "an array",
            JsonKind.String => "a string",
            JsonKind.Number => "a number",
            JsonKind.Boolean => "true or false",
            _ => "null",
        };
}
