using System.Text;

namespace Clicalo.Build;

/// <summary>
/// The release notes that travel inside the package (ACT-004, ADR-0027): the user-facing sentences of the fragments
/// of <c>changes/unreleased</c> (<c>cl note</c>), one <c>## es</c> and one <c>## en</c> list, and the date of the
/// commit, so the same commit gives the same notes. The app reads exactly this format (<c>ReleaseNotesParser</c>), and
/// GitHub shows it as the body of the release.
/// </summary>
internal static class ReleaseNotesWriter
{
    /// <summary>The notes of <paramref name="version"/> from the fragment texts.</summary>
    /// <param name="version">The version.</param>
    /// <param name="date">The date of the commit, <c>yyyy-MM-dd</c>.</param>
    /// <param name="fragments">The text of each fragment, in file-name order.</param>
    public static string Write(string version, string date, IEnumerable<string> fragments)
    {
        var spanish = new List<string>();
        var english = new List<string>();
        foreach (var fragment in fragments)
        {
            foreach (var raw in fragment.Split('\n'))
            {
                var line = raw.Trim();
                if (Value(line, "es:") is { } es)
                {
                    spanish.Add(es);
                }
                else if (Value(line, "en:") is { } en)
                {
                    english.Add(en);
                }
            }
        }

        var notes = new StringBuilder();
        notes.Append("# Clícalo ").Append(version).Append("\n\n");
        notes.Append("<!-- date: ").Append(date).Append(" -->\n\n");
        Section(notes, "es", spanish);
        notes.Append('\n');
        Section(notes, "en", english);
        return notes.ToString();
    }

    private static void Section(StringBuilder notes, string language, List<string> items)
    {
        notes.Append("## ").Append(language).Append('\n');
        foreach (var item in items)
        {
            notes.Append("- ").Append(item).Append('\n');
        }
    }

    /// <summary>The quoted value of <c>key: "…"</c>, without its comment; null for another key or an empty value.</summary>
    private static string? Value(string line, string key)
    {
        if (!line.StartsWith(key, StringComparison.Ordinal))
        {
            return null;
        }

        var value = line[key.Length..].Trim();
        if (value.StartsWith('"'))
        {
            var end = value.IndexOf('"', 1);
            value = end > 0 ? value[1..end] : value[1..];
        }
        else
        {
            var comment = value.IndexOf(" #", StringComparison.Ordinal);
            if (comment >= 0)
            {
                value = value[..comment];
            }
        }

        value = value.Replace("\\\"", "\"", StringComparison.Ordinal).Trim();
        return value.Length == 0 ? null : value;
    }
}
