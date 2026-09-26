using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.Build;

/// <summary>Reads the files that <c>csharpier check</c> reports as not formatted.</summary>
internal static partial class CSharpierReport
{
    /// <summary>Paths reported as errors, relative and with forward slashes, in report order.</summary>
    public static IReadOnlyList<string> UnformattedFiles(string output)
    {
        var files = new List<string>();
        foreach (var line in Markdown.Lines(output).Split('\n'))
        {
            var match = ErrorLine().Match(line);
            if (!match.Success)
            {
                continue;
            }

            var path = match.Groups["path"].Value.Replace('\\', '/');
            if (path.StartsWith("./", StringComparison.Ordinal))
            {
                path = path[2..];
            }

            if (!files.Contains(path, StringComparer.Ordinal))
            {
                files.Add(path);
            }
        }

        return files;
    }

    /// <summary>Renders the files as a list.</summary>
    public static string Render(IReadOnlyList<string> files)
    {
        var list = new StringBuilder();
        foreach (var file in files)
        {
            list.Append("- ").Append(Markdown.Text(file)).Append('\n');
        }

        return list.ToString();
    }

    // "Error ./src/Foo.cs - Was not formatted."
    [GeneratedRegex(
        @"^\s*Error\s+(?<path>.+?)\s+-\s+\S.*$",
        RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture,
        matchTimeoutMilliseconds: 1000
    )]
    private static partial Regex ErrorLine();
}
