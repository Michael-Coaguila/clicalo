using System.Text;

namespace Clicalo.Build;

/// <summary>
/// Minimal, safe Markdown building blocks for reports that are read both raw in the editor and in the
/// preview. Only characters that would change the rendering are escaped, to keep the raw text readable.
/// </summary>
internal static class Markdown
{
    private const string Escaped = "\\`*[]<>|";

    /// <summary>Plain text on one line: markup characters escaped and line breaks folded into spaces.</summary>
    public static string Text(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is '\r' or '\n')
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                {
                    builder.Append(' ');
                }

                continue;
            }

            if (
                Escaped.Contains(character, StringComparison.Ordinal)
                || IsEmphasisUnderscore(value, index)
            )
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    /// <summary>A fenced code block whose fence is longer than any backtick run inside the content.</summary>
    public static string CodeBlock(string content)
    {
        var normalized = Lines(content).TrimEnd('\n');
        var fence = new string('`', Math.Max(3, LongestBacktickRun(normalized) + 1));
        return fence + "text\n" + normalized + "\n" + fence;
    }

    /// <summary>An inline code span (for commands people may copy), safe for any content.</summary>
    public static string InlineCode(string value)
    {
        var singleLine = Lines(value).Replace('\n', ' ');
        var fence = new string('`', LongestBacktickRun(singleLine) + 1);
        var padding = singleLine.StartsWith('`') || singleLine.EndsWith('`') ? " " : string.Empty;
        return fence + padding + singleLine + padding + fence;
    }

    /// <summary>A link with a destination in angle brackets, so paths with spaces stay valid.</summary>
    public static string Link(string text, string destination) =>
        "[" + Text(text) + "](<" + destination.Replace('\\', '/') + ">)";

    /// <summary>Normalizes line endings to <c>\n</c>.</summary>
    public static string Lines(string content) =>
        content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    /// An underscore between two letters or digits never starts emphasis in CommonMark (test names such as
    /// <c>Fails_when_empty</c> stay readable); any other underscore is escaped.
    /// </summary>
    private static bool IsEmphasisUnderscore(string value, int index) =>
        value[index] == '_'
        && !(
            index > 0
            && index < value.Length - 1
            && IsWordCharacter(value[index - 1])
            && IsWordCharacter(value[index + 1])
        );

    private static bool IsWordCharacter(char character) =>
        char.IsLetterOrDigit(character) || character == '_';

    private static int LongestBacktickRun(string content)
    {
        var longest = 0;
        var current = 0;
        foreach (var character in content)
        {
            current = character == '`' ? current + 1 : 0;
            longest = Math.Max(longest, current);
        }

        return longest;
    }
}
