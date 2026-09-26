using System.IO;
using System.Text.Json;
using Clicalo.Application.Localization;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.TestKit;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The data of the panel tests: a profile with one tile of each M2 kind (Tap, Hold, Toggle, and a Web tile that the
/// surface treats as Tap), and the real language files of <c>data/i18n</c>, loaded the way the app loads them.
/// Combinations are empty: the panel never reads them (the engine plans the keys) and <c>KeyChord.Create</c> belongs
/// to the domain package.
/// </summary>
internal static class PanelTestData
{
    public static readonly ProfileId Word = new("p-word");
    public static readonly ShortcutId Copy = new("copy");
    public static readonly ShortcutId HoldCtrl = new("hold-ctrl");
    public static readonly ShortcutId ShiftLock = new("shift");
    public static readonly ShortcutId Site = new("site");

    /// <summary>The profile of the panel: Copiar (Tap), Mantener Ctrl (Hold), Mayús fija (Toggle), Web (Tap).</summary>
    public static Profile Profile(InjectionMode mode = InjectionMode.VirtualKey) =>
        new(
            Word,
            Text("Word", "Word"),
            new IconRef("description"),
            AutoIcon: false,
            new AppBinding.Manual(),
            mode,
            new ValueList<Shortcut>([
                Shortcut(Copy, "Copiar", "Copy", new TapAction(KeyChord.Empty, [])),
                Shortcut(HoldCtrl, "Mantener Ctrl", "Hold Ctrl", new HoldAction(KeyChord.Empty)),
                Shortcut(ShiftLock, "Mayús fija", "Shift lock", new ToggleAction(KeyChord.Empty)),
                Shortcut(
                    Site,
                    "Web",
                    "Site",
                    new UrlAction(new UrlTarget.Valid(new Uri("https://example.org")))
                ),
            ]),
            Origin: null
        );

    public static Shortcut Shortcut(ShortcutId id, string es, string en, ShortcutAction action) =>
        new(
            id,
            Text(es, en),
            new IconRef("keyboard"),
            AutoIcon: false,
            new CategoryId("edit"),
            action,
            new ShortcutOptions(Confirm: false, new HoldLimit.InheritGlobal(), IsPrivate: false),
            Origin: null,
            PinnedFrom: null
        );

    /// <summary>The real language files, as the app loads them.</summary>
    public static LocalizationContext Localization(string language = "es")
    {
        var folder = Path.Combine(RepoPaths.Data, "i18n");
        using var locales = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(folder, "locales.json"))
        );
        var packs = new List<LanguagePack>();
        foreach (var locale in locales.RootElement.GetProperty("locales").EnumerateArray())
        {
            var info = new LocaleInfo(
                locale.GetProperty("code").GetString()!,
                locale.GetProperty("culture").GetString()!,
                locale.GetProperty("nativeName").GetString()!,
                locale.GetProperty("shortName").GetString()!,
                locale.GetProperty("decimalSeparator").GetString()!,
                PluralRules.Create(
                    locale
                        .GetProperty("plural")
                        .EnumerateObject()
                        .Select(static rule =>
                            KeyValuePair.Create(
                                Enum.Parse<PluralCategory>(rule.Name, ignoreCase: true),
                                rule.Value.GetString()!
                            )
                        )
                )
            );
            using var strings = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(folder, "strings." + info.Code + ".json"))
            );
            packs.Add(
                LanguagePack.Create(
                    info,
                    [
                        .. strings
                            .RootElement.EnumerateObject()
                            .Select(static entry =>
                                KeyValuePair.Create(entry.Name, entry.Value.GetString()!)
                            ),
                    ]
                )
            );
        }

        return new LocalizationContext(
            packs,
            locales.RootElement.GetProperty("default").GetString()!,
            language
        );
    }

    private static LocalizedText Text(string es, string en) =>
        new([KeyValuePair.Create(LangCode.Es, es), KeyValuePair.Create(LangCode.En, en)]);
}
