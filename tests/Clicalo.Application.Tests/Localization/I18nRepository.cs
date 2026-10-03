using System.Text.Json;
using Clicalo.Application.Localization;
using Clicalo.TestKit;

namespace Clicalo.Application.Tests.Localization;

/// <summary>
/// Loads <c>data/i18n</c> and the design handoff the way Infrastructure will: JSON → <see cref="LocaleInfo"/> and
/// <see cref="LanguagePack"/>. Test-only; the product loader lives in Infrastructure.
/// </summary>
internal static class I18nRepository
{
    public static string DataDirectory => Path.Combine(RepoPaths.Data, "i18n");

    public static IReadOnlyList<LocaleInfo> Locales()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(DataDirectory, "locales.json"))
        );
        return
        [
            .. document
                .RootElement.GetProperty("locales")
                .EnumerateArray()
                .Select(static l => new LocaleInfo(
                    l.GetProperty("code").GetString()!,
                    l.GetProperty("culture").GetString()!,
                    l.GetProperty("nativeName").GetString()!,
                    l.GetProperty("shortName").GetString()!,
                    l.GetProperty("decimalSeparator").GetString()!,
                    PluralRules.Create(
                        l.GetProperty("plural")
                            .EnumerateObject()
                            .Select(static r =>
                                KeyValuePair.Create(
                                    Enum.Parse<PluralCategory>(r.Name, ignoreCase: true),
                                    r.Value.GetString()!
                                )
                            )
                    )
                )),
        ];
    }

    public static string DefaultLanguage()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(DataDirectory, "locales.json"))
        );
        return document.RootElement.GetProperty("default").GetString()!;
    }

    public static LanguagePack Pack(LocaleInfo locale) =>
        LanguagePack.Create(
            locale,
            ReadFlat(Path.Combine(DataDirectory, "strings." + locale.Code + ".json"))
        );

    public static LocalizationContext Context(string? initialLanguage = null) =>
        new([.. Locales().Select(Pack)], DefaultLanguage(), initialLanguage);

    public static Localizer Localizer(string code)
    {
        var locales = Locales();
        var fallback = Pack(
            locales.Single(static l =>
                string.Equals(l.Code, DefaultLanguage(), StringComparison.Ordinal)
            )
        );
        return new Localizer(
            Pack(locales.Single(l => string.Equals(l.Code, code, StringComparison.Ordinal))),
            fallback
        );
    }

    /// <summary>The original strings of the design handoff, in file order.</summary>
    public static List<KeyValuePair<string, string>> Handoff(string language) =>
        ReadFlat(Path.Combine(RepoPaths.Handoff, "data", "strings." + language + ".json"));

    public static JsonDocument Recipe() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(DataDirectory, "handoff-import.json")));

    /// <summary>The handoff keys a user decision removed from the product (<c>retired</c> of the recipe).</summary>
    public static HashSet<string> Retired()
    {
        using var recipe = Recipe();
        return recipe.RootElement.TryGetProperty("retired", out var retired)
            ? retired
                .EnumerateObject()
                .Select(static p => p.Name)
                .Where(static name => !string.Equals(name, "notes", StringComparison.Ordinal))
                .ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
    }

    /// <summary>The original strings of the design handoff that the product shows (without the retired keys).</summary>
    public static List<KeyValuePair<string, string>> HandoffInProduct(string language)
    {
        var retired = Retired();
        return [.. Handoff(language).Where(entry => !retired.Contains(entry.Key))];
    }

    public static List<KeyValuePair<string, string>> ReadFlat(string path)
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
