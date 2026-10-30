using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Ports;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// Reads the release notes that travel with each package (ACT-004, written by <c>cl package</c> from
/// <c>changes/unreleased</c>; see <c>docs/guides/release.md</c>): one <c>## es</c> and one <c>## en</c> heading, each
/// followed by <c>- item</c> lines, and an optional <c>&lt;!-- date: yyyy-MM-dd --&gt;</c> line. Anything else is
/// ignored, so a note written by hand in the release never breaks the tab. Untrusted text: it is only shown, never
/// executed, and each list is capped.
/// </summary>
internal static class ReleaseNotesParser
{
    /// <summary>The most changes kept per language.</summary>
    public const int MaxItems = 20;

    /// <summary>The longest change kept, in characters.</summary>
    public const int MaxItemLength = 200;

    private const string DateMarker = "<!-- date:";

    /// <summary>Parses <paramref name="markdown"/> for <paramref name="version"/>.</summary>
    /// <param name="version">The version the notes belong to.</param>
    /// <param name="markdown">The notes, or null.</param>
    /// <param name="isNew">Whether the version is the one found and not installed yet.</param>
    public static ReleaseNotes Parse(string version, string? markdown, bool isNew)
    {
        var items = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        DateOnly? date = null;
        List<string>? current = null;
        foreach (var raw in (markdown ?? string.Empty).Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith(DateMarker, StringComparison.OrdinalIgnoreCase))
            {
                var value = line[DateMarker.Length..]
                    .Replace("-->", string.Empty, StringComparison.Ordinal)
                    .Trim();
                if (
                    DateOnly.TryParseExact(
                        value,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var parsed
                    )
                )
                {
                    date = parsed;
                }
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                var language = line[3..].Trim().ToLowerInvariant();
                current = language.Length is >= 2 and <= 5 ? Items(items, language) : null;
            }
            else if (current is not null && line.StartsWith("- ", StringComparison.Ordinal))
            {
                var item = line[2..].Trim();
                if (item.Length > 0 && current.Count < MaxItems)
                {
                    current.Add(item.Length > MaxItemLength ? item[..MaxItemLength] : item);
                }
            }
        }

        return new ReleaseNotes(
            version,
            date,
            isNew,
            items.ToImmutableDictionary(
                pair => pair.Key,
                pair => pair.Value.ToImmutableArray(),
                StringComparer.Ordinal
            )
        );
    }

    private static List<string> Items(Dictionary<string, List<string>> items, string language)
    {
        if (!items.TryGetValue(language, out var list))
        {
            list = [];
            items[language] = list;
        }

        return list;
    }
}
