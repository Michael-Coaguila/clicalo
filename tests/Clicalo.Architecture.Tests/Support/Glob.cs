using System.Text;
using System.Text.RegularExpressions;

namespace Clicalo.Architecture.Tests.Support;

/// <summary>
/// Repository path globs used by the architecture registries: forward slashes, <c>*</c> within one segment,
/// <c>**</c> across segments (including none). Matching is ordinal and case-insensitive, as on Windows.
/// </summary>
internal sealed class Glob
{
    private readonly Regex _regex;

    public Glob(string pattern)
    {
        Pattern = pattern;
        _regex = new Regex(
            ToRegex(pattern),
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            TimeSpan.FromSeconds(1)
        );
    }

    public string Pattern { get; }

    /// <summary>True when the repository-relative <paramref name="path"/> matches.</summary>
    public bool IsMatch(string path) => _regex.IsMatch(path.Replace('\\', '/'));

    private static string ToRegex(string pattern)
    {
        var regex = new StringBuilder("^");
        for (var i = 0; i < pattern.Length; i++)
        {
            var c = pattern[i];
            if (c == '*' && i + 1 < pattern.Length && pattern[i + 1] == '*')
            {
                var slashFollows = i + 2 < pattern.Length && pattern[i + 2] == '/';
                regex.Append(slashFollows ? "(?:.*/)?" : ".*");
                i += slashFollows ? 2 : 1;
            }
            else if (c == '*')
            {
                regex.Append("[^/]*");
            }
            else
            {
                regex.Append(Regex.Escape(c.ToString()));
            }
        }

        return regex.Append('$').ToString();
    }
}
