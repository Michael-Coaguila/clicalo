namespace Clicalo.Generators.Tokens;

/// <summary>A token file handed to the model builder: its path and text (<c>null</c> when unreadable).</summary>
internal sealed class TokenSourceFile(string path, string? text)
{
    public string Path { get; } = path;

    public string? Text { get; } = text;
}
