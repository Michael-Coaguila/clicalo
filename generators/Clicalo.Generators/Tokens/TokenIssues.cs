using System.Collections.Generic;
using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>Collects data errors in the order they are found (deterministic, so the build output is stable).</summary>
internal sealed class TokenIssues
{
    private readonly List<TokenIssue> _items = [];

    public IReadOnlyList<TokenIssue> Items => _items;

    public int Count => _items.Count;

    public void Report(string id, DataPosition? at, string message) =>
        _items.Add(new TokenIssue(id, at?.Path, at?.Line ?? 0, at?.Column ?? 0, message));

    /// <summary>Reports a shape error (CLCT004) at <paramref name="node"/>.</summary>
    public void Malformed(TokenDocument document, JsonNode node, string message) =>
        Report(TokenIds.MalformedFile, document.At(node), document.FileName + ": " + message);
}
