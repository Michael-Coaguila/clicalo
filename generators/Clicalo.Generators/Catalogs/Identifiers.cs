using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis.CSharp;

namespace Clicalo.Generators.Catalogs;

/// <summary>Naming rules shared by the catalog emitters.</summary>
internal static class Identifiers
{
    private static readonly HashSet<string> ObjectMembers = new(StringComparer.Ordinal)
    {
        "Equals",
        "Finalize",
        "GetHashCode",
        "GetType",
        "MemberwiseClone",
        "ReferenceEquals",
        "ToString",
    };

    /// <summary>
    /// Checks a name that becomes a member of a generated type: a valid PascalCase name that neither hides a
    /// member of <see cref="object"/> nor collides with the members the generator adds (<paramref name="reserved"/>).
    /// </summary>
    public static string? MemberNameProblem(string name, params string[] reserved)
    {
        var problem = PascalCaseProblem(name);
        if (problem is not null)
        {
            return problem;
        }

        if (ObjectMembers.Contains(name))
        {
            return "it hides a member of System.Object";
        }

        foreach (var member in reserved)
        {
            if (string.Equals(member, name, StringComparison.Ordinal))
            {
                return "the generated type already declares a member with that name";
            }
        }

        return null;
    }

    /// <summary>
    /// Checks a name that becomes a C# member: PascalCase ASCII letters and digits, not a keyword.
    /// Returns <see langword="null"/> when valid, or the reason it is not.
    /// </summary>
    public static string? PascalCaseProblem(string name)
    {
        if (name.Length == 0)
        {
            return "it is empty";
        }

        if (name[0] < 'A' || name[0] > 'Z')
        {
            return "it must start with an upper-case ASCII letter";
        }

        foreach (var c in name)
        {
            if (!IsAsciiLetterOrDigit(c))
            {
                return "it may only contain ASCII letters and digits";
            }
        }

        return
            SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None
            || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? "it is a C# keyword"
            : null;
    }

    /// <summary>Converts <c>mods</c>, <c>mild-tremor</c> or <c>widthPx</c> to <c>Mods</c>, <c>MildTremor</c>, <c>WidthPx</c>.</summary>
    public static string ToPascalCase(string name)
    {
        var builder = new StringBuilder(name.Length);
        var upper = true;
        foreach (var c in name)
        {
            if (c == '-' || c == '_' || c == '.')
            {
                upper = true;
                continue;
            }

            builder.Append(upper ? char.ToUpperInvariant(c) : c);
            upper = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Canonical key id: dot-separated lower-case ASCII words (<c>ctrl</c>, <c>num.add</c>, <c>f12</c>), or
    /// <c>char:</c> plus a single visible character that is not an ASCII letter or digit and has no upper case.
    /// Returns <see langword="null"/> when canonical, or the reason it is not.
    /// </summary>
    public static string? KeyIdProblem(string id)
    {
        const string CharacterPrefix = "char:";
        if (id.StartsWith(CharacterPrefix, System.StringComparison.Ordinal))
        {
            var payload = id.Substring(CharacterPrefix.Length);
            if (payload.Length != 1)
            {
                return "a character key holds exactly one precomposed character";
            }

            var c = payload[0];
            if (char.IsWhiteSpace(c) || char.IsControl(c) || char.IsSurrogate(c))
            {
                return "the character must be visible";
            }

            if (IsAsciiLetterOrDigit(c))
            {
                return "ASCII letters and digits use their plain name";
            }

            return char.ToLowerInvariant(c) != c ? "the character must be lower case" : null;
        }

        return IsDottedLowerName(id)
            ? null
            : "use lower-case ASCII letters and digits separated by dots";
    }

    /// <summary>Lower-case ASCII word of letters and digits starting with a letter (group ids).</summary>
    public static bool IsLowerWord(string text)
    {
        if (text.Length == 0 || text[0] < 'a' || text[0] > 'z')
        {
            return false;
        }

        foreach (var c in text)
        {
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Lower-case words joined by single hyphens (preset ids such as <c>mild-tremor</c>).</summary>
    public static bool IsKebabCase(string text)
    {
        foreach (var part in text.Split('-'))
        {
            if (part.Length == 0)
            {
                return false;
            }

            foreach (var c in part)
            {
                if (c < 'a' || c > 'z')
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Formats an integer for generated code, independent of the build machine culture.</summary>
    public static string Literal(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static bool IsDottedLowerName(string id)
    {
        foreach (var part in id.Split('.'))
        {
            if (part.Length == 0)
            {
                return false;
            }

            foreach (var c in part)
            {
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9'))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsAsciiLetterOrDigit(char c) =>
        (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
}
