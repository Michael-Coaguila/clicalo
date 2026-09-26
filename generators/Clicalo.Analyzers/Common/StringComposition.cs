using Microsoft.CodeAnalysis;

namespace Clicalo.Analyzers.Common;

/// <summary>The <c>string</c> methods that build a new string out of their arguments.</summary>
internal static class StringComposition
{
    /// <summary>Name of the composite-format parameter of <c>string.Format</c>.</summary>
    public const string FormatParameter = "format";

    /// <summary>True for <c>string.Format</c>, <c>string.Concat</c> and <c>string.Join</c>.</summary>
    public static bool IsComposition(IMethodSymbol method) =>
        method.IsStatic
        && method.ContainingType?.SpecialType == SpecialType.System_String
        && method.Name is "Format" or "Concat" or "Join";

    /// <summary>
    /// The text a composite format string shows once its format items are removed: <c>"{0} atajos"</c> shows
    /// <c>" atajos"</c>, <c>"{0:N0}"</c> shows nothing and <c>"{{0}}"</c> shows <c>"{0}"</c>.
    /// </summary>
    public static string VisiblePartOfFormat(string format)
    {
        var builder = new System.Text.StringBuilder(format.Length);
        for (var i = 0; i < format.Length; i++)
        {
            var c = format[i];
            if ((c == '{' || c == '}') && i + 1 < format.Length && format[i + 1] == c)
            {
                builder.Append(c);
                i++;
            }
            else if (c == '{')
            {
                var close = format.IndexOf('}', i + 1);
                if (close < 0)
                {
                    break;
                }

                i = close;
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }
}
