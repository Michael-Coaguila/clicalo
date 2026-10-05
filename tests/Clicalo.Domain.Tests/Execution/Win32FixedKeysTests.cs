using System.Globalization;
using System.Text.Json;
using Clicalo.Domain.Execution.Internal;
using Clicalo.Domain.Keys;
using Clicalo.TestKit;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The engine's copy of keys.win32.json is the JSON, entry by entry, and covers every fixed key of the catalog
/// (blueprint §7.7): fixed keys take vk, scan and extended from that table in both modes.
/// </summary>
[Trait("Req", "EJE-003")]
[Trait("Req", "ATJ-004")]
public sealed class Win32FixedKeysTests
{
    private static readonly Lazy<Dictionary<string, FixedKey>> Json = new(Load);

    [Fact]
    public void Every_entry_of_the_json_is_in_the_engine_table_with_the_same_values()
    {
        foreach (var (id, expected) in Json.Value)
        {
            Win32FixedKeys.ById.TryGetValue(id, out var actual).ShouldBeTrue(id);
            actual.ShouldBe(expected, id);
        }
    }

    [Fact]
    public void The_engine_table_has_nothing_the_json_does_not() =>
        Win32FixedKeys.ById.Keys.Except(Json.Value.Keys, StringComparer.Ordinal).ShouldBeEmpty();

    [Fact]
    public void Every_key_of_the_catalog_is_fixed_or_a_character() =>
        KeyDefinitions
            .All.Where(static d => !d.Id.IsCharacter)
            .Select(static d => d.Id.Value)
            .Except(Win32FixedKeys.ById.Keys, StringComparer.Ordinal)
            .ShouldBeEmpty();

    private static Dictionary<string, FixedKey> Load()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "keys.win32.json"))
        );
        var result = new Dictionary<string, FixedKey>(StringComparer.Ordinal);
        foreach (var entry in document.RootElement.GetProperty("keys").EnumerateObject())
        {
            if (entry.Value.TryGetProperty("resolve", out _))
            {
                continue;
            }

            result[entry.Name] = new FixedKey(
                Hex(entry.Value.GetProperty("vk").GetString()!),
                Hex(entry.Value.GetProperty("scan").GetString()!),
                entry.Value.GetProperty("extended").GetBoolean(),
                entry.Value.TryGetProperty("prefixE1", out var e1) && e1.GetBoolean()
            );
        }

        return result;
    }

    private static ushort Hex(string text) =>
        ushort.Parse(text.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
