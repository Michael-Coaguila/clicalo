namespace Clicalo.Generators.Tokens;

/// <summary>A data error found while reading the token files, with its 1-based position (0 when unknown).</summary>
internal sealed class TokenIssue(string id, string? path, int line, int column, string message)
{
    public string Id { get; } = id;

    /// <summary>Path of the additional file, or <c>null</c> for errors that are not tied to a file.</summary>
    public string? Path { get; } = path;

    public int Line { get; } = line;

    public int Column { get; } = column;

    public string Message { get; } = message;
}
