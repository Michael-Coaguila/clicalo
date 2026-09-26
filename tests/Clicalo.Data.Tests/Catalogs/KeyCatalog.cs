using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Test view of keys.json and keys.win32.json.</summary>
internal sealed class KeyCatalog
{
    private static readonly Lazy<KeyCatalog> Instance = new(() => new KeyCatalog());

    private KeyCatalog()
    {
        var keys = CatalogFiles.LoadObject(CatalogFiles.Catalog("keys.json"));
        Groups =
        [
            .. keys["groups"]!
                .AsArray()
                .Select(g => new KeyGroupInfo(Str(g, "id"), Str(g, "labelKey"))),
        ];
        Keys = [.. keys["keys"]!.AsArray().Select(ReadKey)];
        ById = Keys.ToDictionary(k => k.Id, StringComparer.Ordinal);

        var win32 = CatalogFiles.LoadObject(CatalogFiles.Catalog("keys.win32.json"));
        ReferenceLayout = Str(win32, "referenceLayout");
        Win32 = win32["keys"]!
            .AsObject()
            .ToDictionary(p => p.Key, p => ReadMapping(p.Value!), StringComparer.Ordinal);
    }

    public static KeyCatalog Shared => Instance.Value;

    public IReadOnlyList<KeyGroupInfo> Groups { get; }

    public IReadOnlyList<KeyInfo> Keys { get; }

    public IReadOnlyDictionary<string, KeyInfo> ById { get; }

    public string ReferenceLayout { get; }

    public IReadOnlyDictionary<string, Win32Mapping> Win32 { get; }

    /// <summary>
    /// Resolves a spelling (id, Spanish or English label, alias) without case, the way a parser of written
    /// combinations does (catalog §7.3). Returns null when nothing matches.
    /// </summary>
    public KeyInfo? Resolve(string spelling)
    {
        foreach (var key in Keys)
        {
            if (key.Spellings.Contains(spelling, StringComparer.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }

    /// <summary>
    /// Canonical key of a combination (REP-001): modifiers as a set with their side, main keys in order.
    /// With <paramref name="genericIsLeft"/>, a modifier without side counts as its left key (blocked combos).
    /// </summary>
    public string Canonical(IEnumerable<string> keyIds, bool genericIsLeft = false)
    {
        var modifiers = new SortedSet<string>(StringComparer.Ordinal);
        var main = new List<string>();
        foreach (var id in keyIds)
        {
            var key = ById[id];
            if (key.Modifier is null)
            {
                main.Add(id);
                continue;
            }

            var side = key.Side ?? (genericIsLeft ? "left" : "any");
            modifiers.Add(key.Modifier + ":" + side);
        }

        return string.Join("+", modifiers) + "|" + string.Join("+", main);
    }

    private static KeyInfo ReadKey(JsonNode? node)
    {
        var label = node!["label"]!;
        var spellings = new List<string> { Str(node, "id"), Str(label, "es"), Str(label, "en") };
        spellings.AddRange(node["aliases"]?.AsArray().Select(a => a!.GetValue<string>()) ?? []);
        return new KeyInfo(
            Str(node, "id"),
            Str(node, "codeName"),
            Str(node, "group"),
            Str(label, "es"),
            Str(label, "en"),
            node["spoken"] is { } spoken ? (Str(spoken, "es"), Str(spoken, "en")) : null,
            node["short"] is { } shortLabel ? (Str(shortLabel, "es"), Str(shortLabel, "en")) : null,
            node["modifier"]?.GetValue<string>(),
            node["side"]?.GetValue<string>(),
            node["sideOf"]?.GetValue<string>(),
            node["aliases"]?.AsArray().Select(a => a!.GetValue<string>()).ToArray() ?? [],
            [.. spellings.Distinct(StringComparer.OrdinalIgnoreCase)]
        );
    }

    private static Win32Mapping ReadMapping(JsonNode node) =>
        node["resolve"] is not null
            ? new Win32Mapping(IsCharacter: true, 0, null, 0, Extended: false, PrefixE1: false)
            : new Win32Mapping(
                IsCharacter: false,
                Hex(Str(node, "vk")),
                Str(node, "vkName"),
                Hex(Str(node, "scan")),
                node["extended"]!.GetValue<bool>(),
                node["prefixE1"]?.GetValue<bool>() ?? false
            );

    private static int Hex(string value) =>
        int.Parse(
            value.AsSpan(2),
            System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture
        );

    private static string Str(JsonNode? node, string name) =>
        node?[name]?.GetValue<string>() ?? throw new InvalidDataException($"Missing '{name}'.");
}

internal sealed record KeyGroupInfo(string Id, string LabelKey);

internal sealed record KeyInfo(
    string Id,
    string CodeName,
    string Group,
    string LabelEs,
    string LabelEn,
    (string Es, string En)? Spoken,
    (string Es, string En)? Short,
    string? Modifier,
    string? Side,
    string? SideOf,
    IReadOnlyList<string> Aliases,
    IReadOnlyList<string> Spellings
)
{
    public bool IsCharacter => Id.StartsWith("char:", StringComparison.Ordinal);
}

/// <summary>Physical mapping of a key: fixed virtual key and set 1 make code, or a layout character.</summary>
internal sealed record Win32Mapping(
    bool IsCharacter,
    int Vk,
    string? VkName,
    int Scan,
    bool Extended,
    bool PrefixE1
)
{
    /// <summary>Make code in the MAPVK_VK_TO_VSC_EX format: 0xE0 or 0xE1 prefix in the high byte.</summary>
    public int ScanEx =>
        (
            PrefixE1 ? 0xE100
            : Extended ? 0xE000
            : 0
        ) | Scan;
}
