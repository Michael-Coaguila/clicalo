using System.Collections.Generic;

namespace Clicalo.Generators.Localization;

/// <summary>
/// Grammar of an i18n text: literal characters, named placeholders <c>{name}</c> with
/// <c>name = [a-z][A-Za-z0-9]*</c>, and the escapes <c>{{</c> and <c>}}</c> for literal braces.
/// Any other brace is an error. <c>Clicalo.Application.Localization.MessageTemplate</c> implements the
/// same grammar at run time; both are tested with the same cases.
/// </summary>
internal static class TemplateSyntax
{
    /// <summary>True for a valid placeholder name.</summary>
    public static bool IsValidName(string name)
    {
        if (name.Length == 0 || name[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var c in name)
        {
            if (!IsNameChar(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Extracts the placeholders of <paramref name="text"/> or reports the first syntax error.</summary>
    public static bool TryParse(
        string text,
        out List<TemplatePlaceholder> placeholders,
        out TemplateSyntaxError error
    )
    {
        placeholders = [];
        error = default;
        var i = 0;
        while (i < text.Length)
        {
            var c = text[i];
            if (c == '}')
            {
                if (i + 1 < text.Length && text[i + 1] == '}')
                {
                    i += 2;
                    continue;
                }

                error = new TemplateSyntaxError(
                    "'}' does not close a placeholder; write '}}' for a literal brace",
                    i
                );
                return false;
            }

            if (c != '{')
            {
                i++;
                continue;
            }

            if (i + 1 < text.Length && text[i + 1] == '{')
            {
                i += 2;
                continue;
            }

            var close = text.IndexOf('}', i + 1);
            if (close < 0)
            {
                error = new TemplateSyntaxError(
                    "'{' is not closed; write '{{' for a literal brace",
                    i
                );
                return false;
            }

            var name = text.Substring(i + 1, close - i - 1);
            if (!IsValidName(name))
            {
                error = new TemplateSyntaxError(
                    "'{"
                        + name
                        + "}' is not a placeholder; names are lower camelCase letters and digits",
                    i
                );
                return false;
            }

            placeholders.Add(new TemplatePlaceholder(name, i));
            i = close + 1;
        }

        return true;
    }

    private static bool IsNameChar(char c) =>
        c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
