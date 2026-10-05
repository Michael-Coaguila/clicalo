using System.Text.Json;
using System.Text.RegularExpressions;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.I18n;

/// <summary>Reads <c>data/i18n</c> with System.Text.Json, independently of the generator's own reader.</summary>
internal static partial class I18nData
{
    public static readonly string[] Languages = ["es", "en"];

    public static readonly string[] PluralCategories =
    [
        "zero",
        "one",
        "two",
        "few",
        "many",
        "other",
    ];

    public static string Directory => Path.Combine(RepoPaths.Data, "i18n");

    /// <summary>Physical entries of a strings file, in file order.</summary>
    public static List<KeyValuePair<string, string>> Strings(string language) =>
        ReadFlat(Path.Combine(Directory, "strings." + language + ".json"));

    public static List<KeyValuePair<string, string>> Handoff(string language) =>
        ReadFlat(Path.Combine(RepoPaths.Handoff, "data", "strings." + language + ".json"));

    /// <summary>
    /// The handoff keys a user decision removed from the product: <c>retired</c> of <c>handoff-import.json</c>.
    /// </summary>
    public static HashSet<string> RetiredHandoffKeys()
    {
        using var recipe = Json("handoff-import.json");
        return recipe.RootElement.TryGetProperty("retired", out var retired)
            ? retired
                .EnumerateObject()
                .Select(static p => p.Name)
                .Where(static name => !string.Equals(name, "notes", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    public static JsonDocument Json(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, fileName)));

    /// <summary>Base key of a physical key (<c>comboN_one</c> → <c>comboN</c>).</summary>
    public static string BaseKey(string key)
    {
        var separator = key.IndexOf('_', StringComparison.Ordinal);
        return separator < 0 ? key : key[..separator];
    }

    /// <summary>Plural category of a physical key, or <c>null</c> for a plain text.</summary>
    public static string? Category(string key)
    {
        var separator = key.IndexOf('_', StringComparison.Ordinal);
        return separator < 0 ? null : key[(separator + 1)..];
    }

    /// <summary>Base key → sorted distinct placeholder names over all its forms.</summary>
    public static Dictionary<string, string[]> PlaceholdersByKey(string language) =>
        Strings(language)
            .GroupBy(static e => BaseKey(e.Key), StringComparer.Ordinal)
            .ToDictionary(
                static g => g.Key,
                static g =>
                    g.SelectMany(static e => Placeholders(e.Value))
                        .Distinct(StringComparer.Ordinal)
                        .Order(StringComparer.Ordinal)
                        .ToArray(),
                StringComparer.Ordinal
            );

    public static IEnumerable<string> Placeholders(string text) =>
        Placeholder()
            .Matches(text.Replace("{{", string.Empty, StringComparison.Ordinal))
            .Select(static m => m.Groups["name"].Value);

    /// <summary>Base keys listed in <c>allow-unused.txt</c>, with their line numbers.</summary>
    public static List<(string Key, int Line)> AllowUnused() =>
        [
            .. File.ReadAllLines(Path.Combine(Directory, "allow-unused.txt"))
                .Select(static (line, index) => (Key: line.Split('#')[0].Trim(), Line: index + 1))
                .Where(static e => e.Key.Length > 0),
        ];

    [GeneratedRegex(
        @"\{(?<name>[^{}]*)\}",
        RegexOptions.CultureInvariant,
        matchTimeoutMilliseconds: 2000
    )]
    private static partial Regex Placeholder();

    private static List<KeyValuePair<string, string>> ReadFlat(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return
        [
            .. document
                .RootElement.EnumerateObject()
                .Select(static p => KeyValuePair.Create(p.Name, p.Value.GetString()!)),
        ];
    }
}
