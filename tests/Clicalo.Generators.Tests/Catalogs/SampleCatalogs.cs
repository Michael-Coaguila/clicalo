namespace Clicalo.Generators.Tests.Catalogs;

/// <summary>Small valid catalogs, shaped like the real ones, for focused generator tests.</summary>
internal static class SampleCatalogs
{
    public const string Keys = """
        {
          "catalogVersion": 1,
          "groups": [
            { "id": "mods", "labelKey": "kgMods" },
            { "id": "sides", "labelKey": "kgSides" },
            { "id": "letters", "labelKey": "kgLetters" }
          ],
          "keys": [
            { "id": "ctrl", "codeName": "Ctrl", "group": "mods", "label": { "es": "Ctrl", "en": "Ctrl" }, "modifier": "ctrl" },
            { "id": "lctrl", "codeName": "LeftCtrl", "group": "sides", "label": { "es": "Ctrl izq.", "en": "Left Ctrl" }, "modifier": "ctrl", "sideOf": "ctrl", "side": "left" },
            { "id": "a", "codeName": "A", "group": "letters", "label": { "es": "A", "en": "A" } },
            { "id": "char:ñ", "codeName": "NTilde", "group": "letters", "label": { "es": "Ñ", "en": "Ñ" } }
          ]
        }
        """;

    public const string Win32 = """
        {
          "catalogVersion": 1,
          "referenceLayout": "00000409",
          "keys": {
            "ctrl": { "vk": "0xA2", "vkName": "VK_LCONTROL", "scan": "0x1D", "extended": false },
            "lctrl": { "vk": "0xA2", "vkName": "VK_LCONTROL", "scan": "0x1D", "extended": false },
            "a": { "vk": "0x41", "vkName": "A", "scan": "0x1E", "extended": false },
            "char:ñ": { "resolve": "character" }
          }
        }
        """;

    public const string Timings = """
        {
          "catalogVersion": 1,
          "groups": {
            "Touch": {
              "description": "Touch thresholds.",
              "entries": {
                "LongPress": { "duration": "600ms", "description": "Long press.", "req": ["NFR-020"] },
                "SwipeMinDistancePx": { "px": 60, "description": "Swipe distance.", "req": ["CUA-005"] },
                "SwipeMaxSlope": { "ratio": 0.6, "description": "Swipe slope.", "req": ["CUA-005"] }
              }
            },
            "App": {
              "description": "Lifecycle.",
              "entries": {
                "CrashLoop": { "countWindow": { "count": 3, "window": "10min" }, "description": "Crash loop.", "source": "blueprint" },
                "RestartBackoff": { "durations": ["1s", "5s", "30s"], "description": "Backoff.", "source": "blueprint" },
                "MessageMaxBytes": { "bytes": "16KiB", "description": "Message size.", "source": "blueprint" },
                "WaitRange": { "durationRange": { "min": "100ms", "max": "10s", "step": "100ms" }, "description": "Wait.", "source": "x" },
                "UndoDepth": { "count": 20, "description": "Undo depth.", "source": "docs/02" },
                "DimDelay": { "duration": "2.5s", "description": "Dim delay.", "source": "x" }
              }
            }
          }
        }
        """;

    public const string TouchPresets = """
        {
          "catalogVersion": 1,
          "default": "mild-tremor",
          "presets": [
            { "id": "standard", "labelKey": "pStd", "descriptionKey": "dStd", "debounce": "150ms", "hitSlopPx": 8, "cancelMovePx": 45, "minContact": "0ms" },
            { "id": "mild-tremor", "labelKey": "pLeve", "descriptionKey": "dLeve", "debounce": "300ms", "hitSlopPx": 14, "cancelMovePx": 35, "minContact": "0ms" }
          ]
        }
        """;

    /// <summary>The four generated catalogs; sizes.json is the real one because its shape is fixed by SizeMetrics.</summary>
    public static IReadOnlyList<Microsoft.CodeAnalysis.AdditionalText> All(
        string keys = Keys,
        string win32 = Win32,
        string timings = Timings,
        string presets = TouchPresets
    ) =>
        [
            CatalogGeneratorHarness.Catalog("keys.json", keys),
            CatalogGeneratorHarness.Catalog("keys.win32.json", win32),
            CatalogGeneratorHarness.Catalog("timings.json", timings),
            CatalogGeneratorHarness.Catalog("touch-presets.json", presets),
            CatalogGeneratorHarness.Catalog("sizes.json", RealSizes()),
        ];

    public static string RealSizes() =>
        File.ReadAllText(Path.Combine(Clicalo.TestKit.RepoPaths.Data, "catalogs", "sizes.json"));
}
