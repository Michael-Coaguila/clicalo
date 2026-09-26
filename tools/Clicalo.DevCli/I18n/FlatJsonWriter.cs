using System.Globalization;
using System.Text;

namespace Clicalo.DevCli.I18n;

/// <summary>
/// Writes a flat JSON object of strings byte-for-byte reproducibly: two-space indentation, <c>\n</c> line ends, a
/// final newline and only the escapes JSON requires, so «», emoji and accents stay readable in diffs and in Weblate.
/// </summary>
internal static class FlatJsonWriter
{
    // Valid in JSON but invisible and treated as line breaks by JavaScript and many editors: always escaped.
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;

    public static string Write(IEnumerable<KeyValuePair<string, string>> entries)
    {
        var sb = new StringBuilder("{\n");
        var first = true;
        foreach (var (key, value) in entries)
        {
            if (!first)
            {
                sb.Append(",\n");
            }

            first = false;
            sb.Append("  ");
            AppendString(sb, key);
            sb.Append(": ");
            AppendString(sb, value);
        }

        return sb.Append(first ? "}\n" : "\n}\n").ToString();
    }

    private static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (var c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append(@"\\");
                    break;
                case '\n':
                    sb.Append(@"\n");
                    break;
                case '\r':
                    sb.Append(@"\r");
                    break;
                case '\t':
                    sb.Append(@"\t");
                    break;
                case LineSeparator or ParagraphSeparator:
                case < ' ':
                    sb.Append(@"\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default:
                    sb.Append(c);
                    break;
            }
        }

        sb.Append('"');
    }
}
