using System.Globalization;
using System.Text.Json;
using Clicalo.Design.Math;
using Clicalo.TestKit;

namespace Clicalo.Data.Tests.Tokens;

/// <summary>
/// An independent reading of <c>data/tokens</c> with System.Text.Json (the generator uses its own JSON reader):
/// palettes, extra tokens, documented corrections, aliases and category colors, resolved to 8-bit colors.
/// </summary>
internal sealed class TokenDataSet
{
    public const string TintLightness = "tintL";
    public const string WashAlpha = "washA";

    private readonly Dictionary<string, Dictionary<string, string>> _sources;
    private readonly Dictionary<string, Dictionary<string, double>> _numbers;
    private readonly Dictionary<string, bool> _highContrast;

    private TokenDataSet(
        List<string> themes,
        List<string> tokens,
        Dictionary<string, Dictionary<string, string>> sources,
        Dictionary<string, Dictionary<string, double>> numbers,
        Dictionary<string, bool> highContrast,
        CategorySpec categories
    )
    {
        Themes = themes;
        Tokens = tokens;
        _sources = sources;
        _numbers = numbers;
        _highContrast = highContrast;
        Categories = categories;
    }

    public static string Directory => Path.Combine(RepoPaths.Data, "tokens");

    public IReadOnlyList<string> Themes { get; }

    /// <summary>Color tokens in declaration order (palette first, then extra tokens).</summary>
    public IReadOnlyList<string> Tokens { get; }

    public CategorySpec Categories { get; }

    public static JsonDocument Read(string fileName) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Directory, fileName)));

    public static TokenDataSet Load()
    {
        using var palettes = Read("theme-palettes.json");
        using var extra = Read("extra-tokens.json");
        var themes = new List<string>();
        var tokens = new List<string>();
        var sources = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var numbers = new Dictionary<string, Dictionary<string, double>>(StringComparer.Ordinal);
        var highContrast = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var theme in palettes.RootElement.EnumerateObject())
        {
            themes.Add(theme.Name);
            var colors = new Dictionary<string, string>(StringComparer.Ordinal);
            var values = new Dictionary<string, double>(StringComparer.Ordinal);
            highContrast[theme.Name] =
                theme.Value.TryGetProperty("hc", out var hc) && hc.GetBoolean();
            foreach (var member in theme.Value.EnumerateObject())
            {
                switch (member.Value.ValueKind)
                {
                    case JsonValueKind.String:
                        colors[member.Name] = member.Value.GetString()!;
                        AddOnce(tokens, member.Name);
                        break;
                    case JsonValueKind.Number:
                        values[member.Name] = member.Value.GetDouble();
                        break;
                }
            }

            sources[theme.Name] = colors;
            numbers[theme.Name] = values;
        }

        foreach (var theme in DataMembers(extra.RootElement.GetProperty("colors")))
        {
            foreach (var token in DataMembers(theme.Value))
            {
                sources[theme.Name][token.Name] = token.Value.GetString()!;
                AddOnce(tokens, token.Name);
            }
        }

        foreach (var theme in DataMembers(extra.RootElement.GetProperty("corrections")))
        {
            foreach (var correction in DataMembers(theme.Value))
            {
                var to = correction.Value.GetProperty("to");
                if (to.ValueKind == JsonValueKind.Number)
                {
                    numbers[theme.Name][correction.Name] = to.GetDouble();
                }
                else
                {
                    sources[theme.Name][correction.Name] = to.GetString()!;
                }
            }
        }

        var categories = CategorySpec.Read(extra.RootElement.GetProperty("categories"));
        return new TokenDataSet(themes, tokens, sources, numbers, highContrast, categories);
    }

    /// <summary>Documented corrections, as written in <c>extra-tokens.json</c>.</summary>
    public static IReadOnlyList<Correction> ReadCorrections()
    {
        using var extra = Read("extra-tokens.json");
        var result = new List<Correction>();
        foreach (var theme in DataMembers(extra.RootElement.GetProperty("corrections")))
        {
            foreach (var correction in DataMembers(theme.Value))
            {
                var from = correction.Value.GetProperty("from");
                var to = correction.Value.GetProperty("to");
                result.Add(
                    new Correction(
                        theme.Name,
                        correction.Name,
                        from.ValueKind == JsonValueKind.Number
                            ? from.GetRawText()
                            : from.GetString()!,
                        to.ValueKind == JsonValueKind.Number ? to.GetRawText() : to.GetString()!,
                        correction.Value.GetProperty("requirement").GetString()!,
                        correction.Value.GetProperty("reason").GetString()!
                    )
                );
            }
        }

        return result;
    }

    public static IEnumerable<JsonProperty> DataMembers(JsonElement element) =>
        element.EnumerateObject().Where(member => !member.Name.StartsWith('$'));

    public bool IsHighContrast(string theme) => _highContrast[theme];

    /// <summary>The effective CSS text of a token (aliases followed).</summary>
    public string Source(string theme, string token)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var text = _sources[theme][token];
        while (IsAlias(text))
        {
            seen.Add(text).ShouldBeTrue($"alias cycle through '{text}' in theme '{theme}'");
            text = _sources[theme][text];
        }

        return
            text.StartsWith("1px ", StringComparison.Ordinal)
            || text.StartsWith("2px ", StringComparison.Ordinal)
            ? text[(text.IndexOf("solid ", StringComparison.Ordinal) + "solid ".Length)..]
            : text;
    }

    public bool Defines(string theme, string token) => _sources[theme].ContainsKey(token);

    public double Number(string theme, string name) => _numbers[theme][name];

    public Rgba8 Color(string theme, string token) => Parse(Source(theme, token)).ToRgba8();

    public Rgba8 Tint(string theme, string category) =>
        IsHighContrast(theme)
            ? Parse(Categories.HighContrastTint).ToRgba8()
            : GamutMapping
                .ToSrgb(
                    new Oklch(
                        Number(theme, TintLightness),
                        Categories.TintChroma,
                        Categories.Hues[category]
                    )
                )
                .Color.ToRgba8();

    public Rgba8 Wash(string theme, string category) =>
        IsHighContrast(theme)
            ? Parse(Categories.HighContrastWash).ToRgba8()
            : GamutMapping
                .ToSrgb(
                    new Oklch(
                        Categories.WashLightness,
                        Categories.WashChroma,
                        Categories.Hues[category],
                        Number(theme, WashAlpha)
                    )
                )
                .Color.ToRgba8();

    /// <summary>A copy where one token (or <c>tintL</c>/<c>washA</c>) of one theme has another value.</summary>
    public TokenDataSet With(string theme, string name, string value)
    {
        var sources = _sources.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, string>(pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        var numbers = _numbers.ToDictionary(
            pair => pair.Key,
            pair => new Dictionary<string, double>(pair.Value, StringComparer.Ordinal),
            StringComparer.Ordinal
        );
        if (name is TintLightness or WashAlpha)
        {
            numbers[theme][name] = double.Parse(value, CultureInfo.InvariantCulture);
        }
        else
        {
            sources[theme][name] = value;
        }

        return new TokenDataSet(
            [.. Themes],
            [.. Tokens],
            sources,
            numbers,
            _highContrast,
            Categories
        );
    }

    public static CssColor Parse(string text)
    {
        CssParser
            .TryParseColor(text, out var color, out var error)
            .ShouldBeTrue($"'{text}': {error.Message}");
        return color;
    }

    private static bool IsAlias(string text) =>
        text.Length > 0 && char.IsAsciiLetterLower(text[0]) && text.All(char.IsAsciiLetterOrDigit);

    private static void AddOnce(List<string> list, string item)
    {
        if (!list.Contains(item, StringComparer.Ordinal))
        {
            list.Add(item);
        }
    }
}
