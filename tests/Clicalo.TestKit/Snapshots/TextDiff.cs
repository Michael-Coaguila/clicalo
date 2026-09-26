using System.Globalization;
using System.Text;

namespace Clicalo.TestKit.Snapshots;

/// <summary>Short, readable descriptions of text differences for failure messages.</summary>
internal static class TextDiff
{
    private const int ContextLines = 2;
    private const int PreviewLines = 10;

    /// <summary>The first differing line with a little context, invisible characters made visible.</summary>
    public static string Describe(string expected, string actual)
    {
        var expectedLines = expected.Split('\n');
        var actualLines = actual.Split('\n');
        var first = 0;
        while (
            first < expectedLines.Length
            && first < actualLines.Length
            && string.Equals(expectedLines[first], actualLines[first], StringComparison.Ordinal)
        )
        {
            first++;
        }

        var builder = new StringBuilder();
        builder
            .Append(CultureInfo.InvariantCulture, $"First difference at line {first + 1}:")
            .AppendLine();
        var start = Math.Max(0, first - ContextLines);
        for (var i = start; i < first; i++)
        {
            builder.Append("    ").AppendLine(Visible(expectedLines[i]));
        }

        AppendRange(builder, "  - ", expectedLines, first);
        AppendRange(builder, "  + ", actualLines, first);
        return builder.ToString().TrimEnd();
    }

    /// <summary>The first lines of a new snapshot, so the failure message shows what would be accepted.</summary>
    public static string Preview(string actual)
    {
        var lines = actual.Split('\n');
        var builder = new StringBuilder("Received text:").AppendLine();
        foreach (var line in lines.Take(PreviewLines))
        {
            builder.Append("  + ").AppendLine(Visible(line));
        }

        if (lines.Length > PreviewLines)
        {
            builder.Append(
                CultureInfo.InvariantCulture,
                $"  ... {lines.Length - PreviewLines} more lines"
            );
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendRange(StringBuilder builder, string prefix, string[] lines, int first)
    {
        var end = Math.Min(lines.Length, first + ContextLines + 1);
        for (var i = first; i < end; i++)
        {
            builder.Append(prefix).AppendLine(Visible(lines[i]));
        }
    }

    /// <summary>Escapes control, format and non-ASCII space characters, which look identical when printed.</summary>
    private static string Visible(string line)
    {
        var builder = new StringBuilder(line.Length);
        foreach (var c in line)
        {
            var category = char.GetUnicodeCategory(c);
            var invisible =
                char.IsControl(c)
                || category
                    is UnicodeCategory.Format
                        or UnicodeCategory.LineSeparator
                        or UnicodeCategory.ParagraphSeparator
                || (category == UnicodeCategory.SpaceSeparator && c != ' ');
            if (invisible)
            {
                builder.Append(CultureInfo.InvariantCulture, $"\\u{(int)c:X4}");
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
