using Clicalo.Generators.Tokens;
using Clicalo.TestKit;

namespace Clicalo.Generators.Tests.Tokens;

/// <summary>The real <c>data/tokens</c> files, optionally edited, as inputs for the generator and its model builder.</summary>
internal static class TokenTestData
{
    public const string Directory = "/repo/data/tokens/";

    private static readonly Lazy<IReadOnlyDictionary<string, string>> Real = new(() =>
        TokenFiles.All.ToDictionary(
            name => name,
            name =>
                File.ReadAllText(RepoPaths.Combine("data", "tokens", name))
                    .Replace("\r\n", "\n", StringComparison.Ordinal),
            StringComparer.Ordinal
        )
    );

    /// <summary>The real files (file name → content).</summary>
    public static IReadOnlyDictionary<string, string> Files => Real.Value;

    /// <summary>The real files with one textual edit applied to <paramref name="file"/>; the edit must match once.</summary>
    public static Dictionary<string, string> With(string file, string find, string replace)
    {
        var files = new Dictionary<string, string>(Real.Value, StringComparer.Ordinal);
        var text = files[file];
        var index = text.IndexOf(find, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"'{find}' is not in {file}");
        files[file] = text[..index] + replace + text[(index + find.Length)..];
        return files;
    }

    public static Dictionary<string, string> Without(string file)
    {
        var files = new Dictionary<string, string>(Real.Value, StringComparer.Ordinal);
        files.Remove(file);
        return files;
    }

    public static TokenModel Build(IReadOnlyDictionary<string, string> files) =>
        TokenModelBuilder.Build([
            .. files.Select(file => new TokenSourceFile(Directory + file.Key, file.Value)),
        ]);

    /// <summary>1-based line and column of the first occurrence of <paramref name="snippet"/> at or after <paramref name="after"/>.</summary>
    public static (int Line, int Column) PositionOf(
        string text,
        string snippet,
        string? after = null
    )
    {
        var start = after is null ? 0 : text.IndexOf(after, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, $"'{after}' not found");
        var index = text.IndexOf(snippet, start, StringComparison.Ordinal);
        index.ShouldBeGreaterThanOrEqualTo(0, $"'{snippet}' not found");
        var line = 1 + text[..index].Count(c => c == '\n');
        var column = index - (text.LastIndexOf('\n', Math.Max(0, index - 1)) + 1) + 1;
        return (line, column);
    }
}
