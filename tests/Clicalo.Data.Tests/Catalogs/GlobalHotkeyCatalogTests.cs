using System.Text.Json.Nodes;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// The closed list of the global shortcut (BUR-005, user decision D10, ADR-0028) follows the criteria written in
/// <c>global-hotkeys.json</c> that the data can check: 4 to 6 unique combinations of existing keys, no Windows key, no
/// Ctrl+Alt with a character key (AltGr), not Ctrl+Shift+M, and no combination that Windows blocks or treats specially
/// or that the shipped content sends.
/// </summary>
[Trait("Req", "BUR-005")]
public sealed class GlobalHotkeyCatalogTests
{
    private static KeyCatalog Keys => KeyCatalog.Shared;

    [Fact]
    public void There_are_four_to_six_unique_combinations_of_existing_keys()
    {
        var hotkeys = Hotkeys();

        hotkeys.Count.ShouldBeInRange(4, 6);
        hotkeys.Select(h => h.Id).ShouldBeUnique(StringComparer.Ordinal);
        hotkeys
            .Select(h => Keys.Canonical(h.Keys, ignoreSides: true))
            .ShouldBeUnique(StringComparer.Ordinal);
        hotkeys.SelectMany(h => h.Keys).Where(k => !Keys.ById.ContainsKey(k)).ShouldBeEmpty();
    }

    [Fact]
    public void No_combination_uses_the_windows_key_or_ctrl_alt_with_a_character()
    {
        foreach (var (id, keys) in Hotkeys())
        {
            keys.Contains("win", StringComparer.Ordinal).ShouldBeFalse(id);
            var infos = keys.Select(k => Keys.ById[k]).ToList();
            infos.Count(k => k.Modifier is not null).ShouldBeGreaterThanOrEqualTo(2, id);
            if (
                keys.Contains("ctrl", StringComparer.Ordinal)
                && keys.Contains("alt", StringComparer.Ordinal)
            )
            {
                infos
                    .Where(k => k.Modifier is null)
                    .ShouldAllBe(
                        k =>
                            !k.IsCharacter
                            && !Ordinal.Is(k.Group, "letters")
                            && !Ordinal.Is(k.Group, "nums"),
                        id
                    );
            }
        }
    }

    [Fact]
    public void No_combination_is_blocked_special_teams_mute_or_sent_by_the_content()
    {
        var taken = CatalogFiles.LoadObject(CatalogFiles.Catalog("blocked-combos.json"))["combos"]!
            .AsArray()
            .Select(c => Keys.Canonical(Strings(c!["keys"]!), ignoreSides: true))
            .Concat(
                ContentCatalog
                    .Shortcuts.Where(s => s.Keys is not null)
                    .Select(s => Keys.Canonical(s.Keys!, ignoreSides: true))
            )
            .Append(Keys.Canonical(["ctrl", "shift", "m"], ignoreSides: true))
            .ToHashSet(StringComparer.Ordinal);

        Hotkeys()
            .Where(h => taken.Contains(Keys.Canonical(h.Keys, ignoreSides: true)))
            .Select(h => h.Id)
            .ShouldBeEmpty();
    }

    internal static IReadOnlyList<(string Id, IReadOnlyList<string> Keys)> Hotkeys() =>
        [
            .. CatalogFiles.LoadObject(CatalogFiles.Catalog("global-hotkeys.json"))["hotkeys"]!
                .AsArray()
                .Select(h => (h!["id"]!.GetValue<string>(), Strings(h["keys"]!))),
        ];

    private static IReadOnlyList<string> Strings(JsonNode node) =>
        ContentShortcut.Strings(node.AsArray());
}
