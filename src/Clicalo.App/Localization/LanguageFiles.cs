using System.Globalization;
using System.IO;
using System.Text.Json;
using Clicalo.Application.Localization;

namespace Clicalo.App.Localization;

/// <summary>
/// Loads <c>data/i18n</c> at run time (blueprint §8.5, ADR-0011): <c>locales.json</c> and one <c>strings.&lt;code&gt;.json</c>
/// per language become the <see cref="LanguagePack"/>s of a <see cref="LocalizationContext"/>. The build already
/// rejects broken Spanish and English data (CLCI errors). At run time a language whose entry or texts cannot be read
/// is left out and named in <c>skipped</c>, so the others still start; only a missing or unreadable
/// <c>locales.json</c>, or no texts for its default language, stops the start.
/// </summary>
/// <remarks>
/// <para>
/// Only the <c>i18n</c> folder next to the executable is read: the project copies <c>data/i18n</c> there on build and
/// on publish, so no build ever reads language files from a folder above it that the user could write (an elevated
/// instance included). The loader moves to Infrastructure/Localization once that module exists. It reads files: the
/// start calls it off the UI thread (§3.2).
/// </para>
/// </remarks>
internal static class LanguageFiles
{
    private const string FolderName = "i18n";
    private const string LocalesFile = "locales.json";

    /// <summary>The folder with the language files, or <see langword="null"/> when there is none.</summary>
    /// <param name="baseDirectory">The folder of the executable.</param>
    public static string? Find(string baseDirectory)
    {
        var shipped = Path.Combine(baseDirectory, FolderName);
        return File.Exists(Path.Combine(shipped, LocalesFile)) ? shipped : null;
    }

    /// <summary>Whether <paramref name="folder"/> has the texts of <paramref name="language"/>.</summary>
    /// <param name="folder">A folder found by <see cref="Find"/>.</param>
    /// <param name="language">A two-letter language code.</param>
    public static bool Has(string folder, string language) =>
        language.All(char.IsAsciiLetterLower)
        && File.Exists(Path.Combine(folder, "strings." + language + ".json"));

    /// <summary>Loads every language of <paramref name="folder"/> that can be read.</summary>
    /// <param name="folder">A folder found by <see cref="Find"/>.</param>
    /// <param name="initialLanguage">The language to start with (the settings, or Windows' language); the default when unavailable.</param>
    /// <param name="skipped">The languages left out because their entry or their texts could not be read.</param>
    /// <exception cref="JsonException"><c>locales.json</c> is not valid.</exception>
    /// <exception cref="ArgumentException">There are no texts for the default language.</exception>
    public static LocalizationContext Load(
        string folder,
        string? initialLanguage,
        out IReadOnlyList<string> skipped
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(folder);
        using var locales = JsonDocument.Parse(
            File.ReadAllBytes(Path.Combine(folder, LocalesFile))
        );
        var root = locales.RootElement;
        var defaultLanguage = root.GetProperty("default").GetString() ?? "es";
        var packs = new List<LanguagePack>();
        var left = new List<string>();
        var index = 0;
        foreach (var locale in root.GetProperty("locales").EnumerateArray())
        {
            index++;
            try
            {
                if (ReadPack(folder, locale) is { } pack)
                {
                    if (
                        packs.Exists(p =>
                            string.Equals(p.Locale.Code, pack.Locale.Code, StringComparison.Ordinal)
                        )
                    )
                    {
                        left.Add(pack.Locale.Code);
                        continue;
                    }

                    packs.Add(pack);
                }
            }
            catch (Exception ex) when (IsBrokenData(ex))
            {
                left.Add(
                    locale.ValueKind == JsonValueKind.Object
                    && locale.TryGetProperty("code", out var code)
                    && code.ValueKind == JsonValueKind.String
                        ? code.GetString()!
                        : "#" + index.ToString(CultureInfo.InvariantCulture)
                );
            }
        }

        skipped = left;
        return new LocalizationContext(packs, defaultLanguage, initialLanguage);
    }

    /// <summary>The two-letter language of Windows' interface (<c>es</c>, <c>en</c>…).</summary>
    public static string WindowsLanguage() => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    /// <summary>The pack of one entry of <c>locales.json</c>, or <see langword="null"/> when it has no texts file.</summary>
    private static LanguagePack? ReadPack(string folder, JsonElement locale)
    {
        var info = new LocaleInfo(
            Text(locale, "code"),
            Text(locale, "culture"),
            Text(locale, "nativeName"),
            Text(locale, "shortName"),
            Text(locale, "decimalSeparator"),
            PluralRules.Create(
                locale
                    .GetProperty("plural")
                    .EnumerateObject()
                    .Select(static rule =>
                        KeyValuePair.Create(
                            Enum.Parse<PluralCategory>(rule.Name, ignoreCase: true),
                            rule.Value.GetString() ?? string.Empty
                        )
                    )
            )
        );
        if (!info.Code.All(char.IsAsciiLetterLower))
        {
            throw new JsonException("'code' must be lower-case letters.");
        }

        var strings = Path.Combine(folder, "strings." + info.Code + ".json");
        return File.Exists(strings) ? LanguagePack.Create(info, ReadFlat(strings)) : null;
    }

    /// <summary>A broken language entry or texts file (skipped), not a defect.</summary>
    private static bool IsBrokenData(Exception exception) =>
        exception
            is JsonException
                or KeyNotFoundException
                or InvalidOperationException
                or ArgumentException
                or FormatException
                or IOException
                or UnauthorizedAccessException;

    private static string Text(JsonElement element, string property) =>
        element.GetProperty(property).GetString()
        ?? throw new JsonException("'" + property + "' must be a string.");

    private static List<KeyValuePair<string, string>> ReadFlat(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(path));
        return
        [
            .. document
                .RootElement.EnumerateObject()
                .Where(static entry => entry.Value.ValueKind == JsonValueKind.String)
                .Select(static entry => KeyValuePair.Create(entry.Name, entry.Value.GetString()!)),
        ];
    }
}
