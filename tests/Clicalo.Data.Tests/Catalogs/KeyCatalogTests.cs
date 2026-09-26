using System.Globalization;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>Integrity of keys.json: canonical and unique identities, groups, sides, labels (CAT-001, IDI-003).</summary>
public sealed class KeyCatalogTests
{
    private static KeyCatalog Keys => KeyCatalog.Shared;

    [Fact]
    [Trait("Req", "CAT-001")]
    public void Key_ids_and_code_names_are_unique()
    {
        Keys.Keys.GroupBy(k => k.Id, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ShouldBeEmpty();
        Keys.Keys.GroupBy(k => k.CodeName, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "CAT-001")]
    public void Character_keys_hold_one_lower_case_precomposed_character()
    {
        foreach (var key in Keys.Keys.Where(k => k.IsCharacter))
        {
            var payload = key.Id["char:".Length..];
            payload.Length.ShouldBe(1, key.Id);
            payload.ShouldBe(payload.ToLower(CultureInfo.InvariantCulture), key.Id);
            payload.ShouldBe(payload.Normalize(System.Text.NormalizationForm.FormC), key.Id);
            char.IsAsciiLetterOrDigit(payload[0])
                .ShouldBeFalse(key.Id + " must use its plain name");
        }
    }

    [Fact]
    [Trait("Req", "EDI-008")]
    public void Every_group_is_declared_once_and_holds_keys()
    {
        Keys.Groups.Select(g => g.Id).ShouldBeUnique(StringComparer.Ordinal);
        Keys.Keys.Select(k => k.Group)
            .Distinct(StringComparer.Ordinal)
            .ShouldBe(Keys.Groups.Select(g => g.Id), ignoreOrder: true);
    }

    [Fact]
    [Trait("Req", "EDI-008")]
    public void Keys_of_a_group_are_contiguous_and_groups_follow_the_picker_order()
    {
        var order = Keys.Keys.Select(k => k.Group).Distinct(StringComparer.Ordinal).ToList();

        order.ShouldBe(Keys.Groups.Select(g => g.Id).ToList());
        var runs = Keys
            .Keys.Zip(Keys.Keys.Skip(1))
            .Count(pair =>
                !string.Equals(pair.First.Group, pair.Second.Group, StringComparison.Ordinal)
            );
        runs.ShouldBe(Keys.Groups.Count - 1, "each group must be one contiguous block");
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void Sided_keys_specialize_an_any_side_modifier_of_the_same_family()
    {
        foreach (var key in Keys.Keys.Where(k => k.SideOf is not null))
        {
            var baseKey = Keys.ById[key.SideOf!];
            baseKey.Modifier.ShouldBe(key.Modifier, key.Id);
            baseKey.Side.ShouldBeNull(key.Id + " must specialize an any-side key");
            key.Side.ShouldBeOneOf("left", "right");
            key.Group.ShouldBe("sides");
        }

        Keys.Keys.Where(k => Is(k.Group, "sides")).ShouldAllBe(k => k.SideOf != null);
        Keys.Keys.Where(k => Is(k.Group, "mods"))
            .Select(k => k.Modifier)
            .ShouldBe(["ctrl", "alt", "shift", "win"]);
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    public void Every_modifier_family_offers_its_right_side()
    {
        foreach (var family in new[] { "ctrl", "alt", "shift", "win" })
        {
            Keys.Keys.ShouldContain(k => Is(k.Modifier, family) && Is(k.Side, "right"), family);
        }
    }

    [Fact]
    [Trait("Req", "MIG-003")]
    public void Every_spelling_resolves_to_exactly_one_key()
    {
        var owners = Keys
            .Keys.SelectMany(k => k.Spellings.Select(s => (Spelling: s.ToLowerInvariant(), k.Id)))
            .GroupBy(x => x.Spelling, StringComparer.Ordinal)
            .Where(g => g.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() > 1)
            .Select(g => g.Key + " → " + string.Join(", ", g.Select(x => x.Id)));

        owners.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "MIG-003")]
    public void Aliases_are_lower_case_and_do_not_repeat_the_id_or_a_label()
    {
        foreach (var key in Keys.Keys)
        {
            foreach (var alias in key.Aliases)
            {
                alias.ShouldBe(alias.ToLowerInvariant(), key.Id);
                Is(alias, key.Id).ShouldBeFalse(key.Id);
                string.Equals(alias, key.LabelEs, StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse(key.Id);
                string.Equals(alias, key.LabelEn, StringComparison.OrdinalIgnoreCase)
                    .ShouldBeFalse(key.Id);
            }
        }
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    public void Keys_labelled_with_glyphs_have_a_spoken_name()
    {
        foreach (var key in Keys.Keys)
        {
            // «←», «+» or «Num −»: the last word of the label is a glyph a screen reader may not name (UIA008).
            if (EndsWithGlyph(key.LabelEs) || EndsWithGlyph(key.LabelEn))
            {
                key.Spoken.ShouldNotBeNull(key.Id + " is labelled with a glyph (UIA008)");
                key.Spoken!.Value.Es.Any(char.IsLetter).ShouldBeTrue(key.Id);
                key.Spoken!.Value.En.Any(char.IsLetter).ShouldBeTrue(key.Id);
            }
        }
    }

    [Fact]
    [Trait("Req", "IDI-003")]
    public void Keys_have_translated_names_where_Spanish_keyboards_differ()
    {
        Keys.ById["delete"].LabelEs.ShouldBe("Supr");
        Keys.ById["delete"].LabelEn.ShouldBe("Delete");
        Keys.ById["backspace"].LabelEs.ShouldBe("Retroceso");
        Keys.ById["pagedown"].LabelEn.ShouldBe("PgDn");
        Keys.ById["lctrl"].LabelEs.ShouldBe("Ctrl izq.");
        Keys.ById["lctrl"].LabelEn.ShouldBe("Left Ctrl");
        Keys.ById["space"].LabelEs.ShouldBe("Espacio");
    }

    [Fact]
    [Trait("Req", "EC-EDI-03")]
    public void The_typographic_minus_and_the_hyphen_are_the_same_key()
    {
        Keys.Resolve("−")!.Id.ShouldBe("char:-");
        Keys.Resolve("-")!.Id.ShouldBe("char:-");
        Keys.Resolve("minus")!.Id.ShouldBe("char:-");
    }

    /// <summary>
    /// Every token of the version 1 key table (catalog §7.3) and the catalog key it becomes. «winleft» is the
    /// generic Win, which is already sent as the left key.
    /// </summary>
    public static TheoryData<string, string> Version1Tokens() =>
        new()
        {
            { "ctrl", "ctrl" },
            { "ctrlleft", "lctrl" },
            { "ctrlright", "rctrl" },
            { "alt", "alt" },
            { "altleft", "lalt" },
            { "altright", "altgr" },
            { "shift", "shift" },
            { "shiftleft", "lshift" },
            { "shiftright", "rshift" },
            { "win", "win" },
            { "winleft", "win" },
            { "winright", "rwin" },
            { "ñ", "char:ñ" },
            { "tab", "tab" },
            { "enter", "enter" },
            { "return", "enter" },
            { "esc", "esc" },
            { "escape", "esc" },
            { "space", "space" },
            { "delete", "delete" },
            { "del", "delete" },
            { "backspace", "backspace" },
            { "insert", "insert" },
            { "home", "home" },
            { "end", "end" },
            { "pageup", "pageup" },
            { "pgup", "pageup" },
            { "pagedown", "pagedown" },
            { "pgdn", "pagedown" },
            { "left", "left" },
            { "right", "right" },
            { "up", "up" },
            { "down", "down" },
            { "printscreen", "printscreen" },
            { "prtsc", "printscreen" },
            { "pause", "pause" },
            { "capslock", "capslock" },
            { "numlock", "numlock" },
            { "scrolllock", "scrolllock" },
            { "apps", "menu" },
            { "+", "char:+" },
            { "plus", "char:+" },
            { "-", "char:-" },
            { "minus", "char:-" },
            { "=", "char:=" },
            { ",", "char:," },
            { ".", "char:." },
            { ";", "char:;" },
            { "/", "char:/" },
            { "slash", "char:/" },
            { "grave", "char:`" },
            { "`", "char:`" },
            { "backslash", "char:\\" },
            { "\\", "char:\\" },
            { "[", "char:[" },
            { "]", "char:]" },
            { "'", "char:'" },
            { "#", "char:#" },
            { "num0", "num.0" },
            { "num9", "num.9" },
            { "num+", "num.add" },
            { "add", "num.add" },
            { "num-", "num.subtract" },
            { "subtract", "num.subtract" },
            { "num*", "num.multiply" },
            { "multiply", "num.multiply" },
            { "num/", "num.divide" },
            { "divide", "num.divide" },
            { "decimal", "num.decimal" },
            { "volumeup", "volume.up" },
            { "volumedown", "volume.down" },
            { "volumemute", "volume.mute" },
            { "playpause", "media.playpause" },
            { "nexttrack", "media.next" },
            { "prevtrack", "media.previous" },
            { "stop", "media.stop" },
            { "f13", "f13" },
            { "f24", "f24" },
        };

    [Theory]
    [MemberData(nameof(Version1Tokens))]
    [Trait("Req", "MIG-005")]
    public void Every_version_1_token_resolves_to_its_catalog_key(string token, string keyId)
    {
        Keys.Resolve(token).ShouldNotBeNull(token)!.Id.ShouldBe(keyId, token);
    }

    [Fact]
    [Trait("Req", "CUA-008")]
    public void Size_S_abbreviates_Ctrl_as_Ctl_and_Shift_as_an_arrow_everywhere()
    {
        foreach (var key in Keys.Keys.Where(k => k.Modifier is "ctrl" or "shift"))
        {
            key.Short.ShouldNotBeNull(key.Id);
            var expected = Is(key.Modifier, "ctrl") ? "Ctl" : "⇧";
            key.Short!.Value.Es.ShouldStartWith(expected, Case.Sensitive, key.Id);
            key.Short!.Value.En.ShouldContain(expected, Case.Sensitive, key.Id);
        }

        Keys.Keys.Where(k => k.Modifier is not ("ctrl" or "shift"))
            .ShouldAllBe(k => k.Short == null);
    }

    private static bool EndsWithGlyph(string label) =>
        !label.Split(' ')[^1].Any(char.IsLetterOrDigit);

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.Ordinal);
}
