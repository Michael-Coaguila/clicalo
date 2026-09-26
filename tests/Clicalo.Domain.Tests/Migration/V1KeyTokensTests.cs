using System.Text.Json;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Migration.V1;
using Clicalo.TestKit;

namespace Clicalo.Domain.Tests.Migration;

/// <summary>The token table of catalog §7.3 against the key catalog (MIG-003, MIG-005).</summary>
[Trait("Req", "MIG-003")]
public sealed class V1KeyTokensTests
{
    [Fact]
    public void Every_token_maps_to_a_key_of_the_catalog()
    {
        var catalog = KeyDefinitions.All.Select(static d => d.Id).ToHashSet();

        foreach (var (token, stroke) in V1KeyTokens.Table)
        {
            catalog.ShouldContain(stroke.Key, token);
        }
    }

    [Fact]
    public void Sided_tokens_use_the_base_modifier_and_a_side()
    {
        foreach (var stroke in V1KeyTokens.Table.Values.Where(static s => s.Side != KeySide.Any))
        {
            KeyDefinitions.TryGet(stroke.Key, out var definition).ShouldBeTrue();
            definition!.IsModifier.ShouldBeTrue(stroke.Key.Value);
            definition.Side.ShouldBe(KeySide.Any);
        }
    }

    [Fact]
    public void Every_row_of_the_catalog_table_is_there()
    {
        string[] tokens =
        [
            "ctrl",
            "ctrlleft",
            "ctrlright",
            "alt",
            "altleft",
            "altright",
            "shift",
            "shiftleft",
            "shiftright",
            "win",
            "winleft",
            "winright",
            "a",
            "z",
            "ñ",
            "0",
            "9",
            "f1",
            "f24",
            "tab",
            "enter",
            "return",
            "esc",
            "escape",
            "space",
            "delete",
            "del",
            "backspace",
            "insert",
            "home",
            "end",
            "pageup",
            "pgup",
            "pagedown",
            "pgdn",
            "left",
            "right",
            "up",
            "down",
            "printscreen",
            "prtsc",
            "pause",
            "capslock",
            "numlock",
            "scrolllock",
            "apps",
            "+",
            "plus",
            "-",
            "minus",
            "=",
            ",",
            ".",
            ";",
            "/",
            "slash",
            "grave",
            "`",
            "backslash",
            "\\",
            "[",
            "]",
            "'",
            "#",
            "num0",
            "num9",
            "num+",
            "add",
            "num-",
            "subtract",
            "num*",
            "multiply",
            "num/",
            "divide",
            "decimal",
            "volumeup",
            "volumedown",
            "volumemute",
            "playpause",
            "nexttrack",
            "prevtrack",
            "stop",
        ];

        tokens.Except(V1KeyTokens.Table.Keys, StringComparer.Ordinal).ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-005")]
    public void The_migration_keys_are_in_the_catalog_with_the_v1_aliases()
    {
        using var keys = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "keys.json"))
        );
        var aliases = keys
            .RootElement.GetProperty("keys")
            .EnumerateArray()
            .ToDictionary(
                static k => k.GetProperty("id").GetString()!,
                static k =>
                    k.TryGetProperty("aliases", out var list)
                        ? list.EnumerateArray()
                            .Select(static a => a.GetString()!)
                            .ToHashSet(StringComparer.Ordinal)
                        : new HashSet<string>(StringComparer.Ordinal),
                StringComparer.Ordinal
            );

        aliases["char:`"].Contains("grave").ShouldBeTrue();
        aliases["char:\\"].Contains("backslash").ShouldBeTrue();
        aliases.ShouldContainKey("char:[");
        aliases.ShouldContainKey("char:]");
        aliases.ShouldContainKey("char:'");
        aliases.ShouldContainKey("char:#");
        aliases["num.add"].Contains("num+").ShouldBeTrue();
        aliases["num.subtract"].Contains("num-").ShouldBeTrue();
    }
}
