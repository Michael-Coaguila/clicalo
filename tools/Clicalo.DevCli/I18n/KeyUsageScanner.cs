using System.Text.Json;
using System.Text.RegularExpressions;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// Finds which i18n keys the product uses, without compiling it:
/// <list type="bullet">
/// <item><c>src/**/*.cs</c>: members of the generated class, <c>L.DupHead</c>;</item>
/// <item><c>src/**/*.xaml</c>: the markup extension <c>{loc:T dupHead}</c>;</item>
/// <item><c>data/**/*.json</c> (except <c>data/i18n</c>): string values of properties whose name ends in
/// <c>Key</c>, such as <c>"labelKey": "copy"</c>.</item>
/// </list>
/// It errs on the side of «used», which is the safe side for its only consumer: the allow-unused check.
/// </summary>
internal static partial class KeyUsageScanner
{
    private static readonly string[] SkippedFolders = ["bin", "obj"];

    private static ReadOnlySpan<byte> Utf8ByteOrderMark => [0xEF, 0xBB, 0xBF];

    /// <param name="root">Repository root.</param>
    /// <param name="memberToKey">Generated member name → base key.</param>
    /// <returns>First usage of every used base key.</returns>
    public static Dictionary<string, KeyUsage> Scan(
        string root,
        IReadOnlyDictionary<string, string> memberToKey
    )
    {
        var keys = memberToKey.Values.ToHashSet(StringComparer.Ordinal);
        var usages = new Dictionary<string, KeyUsage>(StringComparer.Ordinal);
        var src = Path.Combine(root, "src");
        foreach (var file in Files(src, "*.cs"))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in CSharpUsage().Matches(text))
            {
                if (memberToKey.TryGetValue(match.Groups["member"].Value, out var key))
                {
                    Add(usages, key, file, text, match.Index);
                }
            }
        }

        foreach (var file in Files(src, "*.xaml"))
        {
            var text = File.ReadAllText(file);
            foreach (Match match in XamlUsage().Matches(text))
            {
                var key = match.Groups["key"].Value;
                if (keys.Contains(key))
                {
                    Add(usages, key, file, text, match.Index);
                }
            }
        }

        var i18n = I18nPaths.Directory(root);
        foreach (
            var file in Files(Path.Combine(root, "data"), "*.json").Where(f => !IsUnder(f, i18n))
        )
        {
            ScanJson(file, keys, usages);
        }

        return usages;
    }

    [GeneratedRegex(
        @"\bL\.(?<member>[A-Z][A-Za-z0-9]*)\b",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex CSharpUsage();

    [GeneratedRegex(
        @"\{loc:T\s+(?<key>[A-Za-z][A-Za-z0-9]*)\s*\}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex XamlUsage();

    private static IEnumerable<string> Files(string directory, string pattern) =>
        Directory.Exists(directory)
            ? Directory
                .EnumerateFiles(directory, pattern, SearchOption.AllDirectories)
                .Where(static f =>
                    !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        .Any(IsSkipped)
                )
                .Order(StringComparer.Ordinal)
            : [];

    private static bool IsSkipped(string segment) =>
        SkippedFolders.Contains(segment, StringComparer.OrdinalIgnoreCase);

    private static bool IsUnder(string file, string directory) =>
        Path.GetFullPath(file)
            .StartsWith(
                Path.GetFullPath(directory) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase
            );

    private static void ScanJson(
        string file,
        HashSet<string> keys,
        Dictionary<string, KeyUsage> usages
    )
    {
        var bytes = File.ReadAllBytes(file);

        // Utf8JsonReader rejects a byte order mark, which editors on Windows often write.
        var start = bytes.AsSpan().StartsWith(Utf8ByteOrderMark) ? Utf8ByteOrderMark.Length : 0;
        var reader = new Utf8JsonReader(
            bytes.AsSpan(start),
            new JsonReaderOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            }
        );
        string? property = null;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    property = reader.GetString();
                    continue;
                }

                if (
                    reader.TokenType == JsonTokenType.String
                    && property is not null
                    && property.EndsWith("Key", StringComparison.Ordinal)
                    && reader.GetString() is { } value
                    && keys.Contains(value)
                )
                {
                    var (line, column) = PositionOf(
                        bytes,
                        start,
                        start + (int)reader.TokenStartIndex
                    );
                    usages.TryAdd(value, new KeyUsage(value, file, line, column));
                }

                property = null;
            }
        }
        catch (JsonException)
        {
            // Malformed data files are reported by their own validation (catalog generator and Data.Tests).
        }
    }

    private static (int Line, int Column) PositionOf(byte[] utf8, int contentStart, int byteOffset)
    {
        var line = 1;
        var lineStart = contentStart;
        for (var i = contentStart; i < byteOffset; i++)
        {
            if (utf8[i] == (byte)'\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (
            line,
            System.Text.Encoding.UTF8.GetCharCount(utf8, lineStart, byteOffset - lineStart) + 1
        );
    }

    private static void Add(
        Dictionary<string, KeyUsage> usages,
        string key,
        string file,
        string text,
        int index
    )
    {
        if (usages.ContainsKey(key))
        {
            return;
        }

        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        usages.Add(key, new KeyUsage(key, file, line, index - lineStart + 1));
    }
}
