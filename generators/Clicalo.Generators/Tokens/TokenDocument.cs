using Clicalo.Generators.Common;

namespace Clicalo.Generators.Tokens;

/// <summary>A parsed token file: where it lives and its JSON root.</summary>
internal sealed class TokenDocument(string path, string fileName, JsonNode root)
{
    public string Path { get; } = path;

    /// <summary>File name used in messages, for example <c>extra-tokens.json</c>.</summary>
    public string FileName { get; } = fileName;

    public JsonNode Root { get; } = root;

    public DataPosition At(JsonNode node) => new(Path, node.Line, node.Column);
}
