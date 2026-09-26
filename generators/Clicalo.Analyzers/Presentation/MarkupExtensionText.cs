using System;
using System.Collections.Generic;
using System.Text;

namespace Clicalo.Analyzers.Presentation;

/// <summary>
/// Minimal reader of XAML markup-extension syntax (<c>{Binding Path, StringFormat='{}{0} items'}</c>): enough to tell a
/// literal attribute from an extension and to pull out the named values that can smuggle literal text or colors
/// (<c>StringFormat</c>, <c>FallbackValue</c>, <c>TargetNullValue</c>).
/// </summary>
internal static class MarkupExtensionText
{
    private const string EscapePrefix = "{}";

    /// <summary>True when the attribute value is a markup extension rather than a literal (<c>{}</c> escapes a literal).</summary>
    public static bool IsExtension(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
            && !trimmed.StartsWith(EscapePrefix, StringComparison.Ordinal);
    }

    /// <summary>The literal an attribute value stands for, without the leading <c>{}</c> escape.</summary>
    public static string Unescape(string value) =>
        value.StartsWith(EscapePrefix, StringComparison.Ordinal)
            ? value.Substring(EscapePrefix.Length)
            : value;

    /// <summary>
    /// The text a .NET format string shows once its format items are removed:
    /// <c>{}{0} atajos</c> shows <c> atajos</c>; <c>{}{0:N2}</c> shows nothing.
    /// </summary>
    public static string VisiblePartOfFormat(string format)
    {
        var text = Unescape(format);
        var builder = new StringBuilder(text.Length);
        var depth = 0;
        foreach (var c in text)
        {
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (depth == 0)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    /// <summary>The <c>Name=Value</c> pairs of an extension whose value is a literal (nested extensions are skipped).</summary>
    public static IReadOnlyList<KeyValuePair<string, string>> NamedLiterals(string extension)
    {
        var result = new List<KeyValuePair<string, string>>();
        var text = extension.Trim();
        if (text.Length < 2 || text[0] != '{' || text[text.Length - 1] != '}')
        {
            return result;
        }

        // Skip the extension type name ("Binding", "x:Static"…).
        var start = 1;
        while (start < text.Length - 1 && !char.IsWhiteSpace(text[start]) && text[start] != '}')
        {
            start++;
        }

        foreach (var segment in SplitArguments(text, start, text.Length - 1))
        {
            var equals = segment.IndexOf('=');
            if (equals <= 0)
            {
                continue;
            }

            var name = segment.Substring(0, equals).Trim();
            var value = segment.Substring(equals + 1).Trim();
            if (value.Length >= 2 && value[0] == '\'' && value[value.Length - 1] == '\'')
            {
                result.Add(
                    new KeyValuePair<string, string>(
                        name,
                        Unquote(value.Substring(1, value.Length - 2))
                    )
                );
            }
            else if (!IsExtension(value))
            {
                result.Add(new KeyValuePair<string, string>(name, Unquote(value)));
            }
        }

        return result;
    }

    /// <summary>Splits the arguments of an extension on top-level commas, honoring quotes, braces and backslash escapes.</summary>
    private static List<string> SplitArguments(string text, int start, int end)
    {
        var segments = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var quoted = false;
        for (var i = start; i < end; i++)
        {
            var c = text[i];
            if (c == '\\' && i + 1 < end)
            {
                current.Append(c).Append(text[++i]);
                continue;
            }

            if (c == '\'')
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '{')
            {
                depth++;
            }
            else if (!quoted && c == '}')
            {
                depth--;
            }
            else if (!quoted && depth == 0 && c == ',')
            {
                segments.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        segments.Add(current.ToString());
        return segments;
    }

    private static string Unquote(string value)
    {
        if (value.IndexOf('\\') < 0)
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
            }

            builder.Append(value[i]);
        }

        return builder.ToString();
    }
}
