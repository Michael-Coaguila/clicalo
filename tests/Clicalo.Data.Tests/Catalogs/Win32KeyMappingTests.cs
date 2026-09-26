using System.Runtime.Versioning;

namespace Clicalo.Data.Tests.Catalogs;

/// <summary>
/// keys.win32.json against keys.json and against Windows itself: every key can be sent, the virtual keys are the
/// constants of Microsoft «Virtual-Key Codes» and the scan codes are those of the en-US layout (NFR-004, D24).
/// </summary>
public sealed class Win32KeyMappingTests
{
    /// <summary>
    /// Virtual-key constants of the Microsoft «Virtual-Key Codes» page (Winuser.h), transcribed independently of
    /// the catalog. Letters and digits have no constant: their virtual key is the ASCII code of the upper-case
    /// character.
    /// </summary>
    private static readonly Dictionary<string, int> MicrosoftVirtualKeys = new(
        StringComparer.Ordinal
    )
    {
        ["VK_BACK"] = 0x08,
        ["VK_TAB"] = 0x09,
        ["VK_RETURN"] = 0x0D,
        ["VK_PAUSE"] = 0x13,
        ["VK_CAPITAL"] = 0x14,
        ["VK_ESCAPE"] = 0x1B,
        ["VK_SPACE"] = 0x20,
        ["VK_PRIOR"] = 0x21,
        ["VK_NEXT"] = 0x22,
        ["VK_END"] = 0x23,
        ["VK_HOME"] = 0x24,
        ["VK_LEFT"] = 0x25,
        ["VK_UP"] = 0x26,
        ["VK_RIGHT"] = 0x27,
        ["VK_DOWN"] = 0x28,
        ["VK_SNAPSHOT"] = 0x2C,
        ["VK_INSERT"] = 0x2D,
        ["VK_DELETE"] = 0x2E,
        ["VK_LWIN"] = 0x5B,
        ["VK_RWIN"] = 0x5C,
        ["VK_APPS"] = 0x5D,
        ["VK_NUMPAD0"] = 0x60,
        ["VK_NUMPAD1"] = 0x61,
        ["VK_NUMPAD2"] = 0x62,
        ["VK_NUMPAD3"] = 0x63,
        ["VK_NUMPAD4"] = 0x64,
        ["VK_NUMPAD5"] = 0x65,
        ["VK_NUMPAD6"] = 0x66,
        ["VK_NUMPAD7"] = 0x67,
        ["VK_NUMPAD8"] = 0x68,
        ["VK_NUMPAD9"] = 0x69,
        ["VK_MULTIPLY"] = 0x6A,
        ["VK_ADD"] = 0x6B,
        ["VK_SUBTRACT"] = 0x6D,
        ["VK_DECIMAL"] = 0x6E,
        ["VK_DIVIDE"] = 0x6F,
        ["VK_F1"] = 0x70,
        ["VK_F2"] = 0x71,
        ["VK_F3"] = 0x72,
        ["VK_F4"] = 0x73,
        ["VK_F5"] = 0x74,
        ["VK_F6"] = 0x75,
        ["VK_F7"] = 0x76,
        ["VK_F8"] = 0x77,
        ["VK_F9"] = 0x78,
        ["VK_F10"] = 0x79,
        ["VK_F11"] = 0x7A,
        ["VK_F12"] = 0x7B,
        ["VK_F13"] = 0x7C,
        ["VK_F14"] = 0x7D,
        ["VK_F15"] = 0x7E,
        ["VK_F16"] = 0x7F,
        ["VK_F17"] = 0x80,
        ["VK_F18"] = 0x81,
        ["VK_F19"] = 0x82,
        ["VK_F20"] = 0x83,
        ["VK_F21"] = 0x84,
        ["VK_F22"] = 0x85,
        ["VK_F23"] = 0x86,
        ["VK_F24"] = 0x87,
        ["VK_NUMLOCK"] = 0x90,
        ["VK_SCROLL"] = 0x91,
        ["VK_LSHIFT"] = 0xA0,
        ["VK_RSHIFT"] = 0xA1,
        ["VK_LCONTROL"] = 0xA2,
        ["VK_RCONTROL"] = 0xA3,
        ["VK_LMENU"] = 0xA4,
        ["VK_RMENU"] = 0xA5,
        ["VK_BROWSER_HOME"] = 0xAC,
        ["VK_VOLUME_MUTE"] = 0xAD,
        ["VK_VOLUME_DOWN"] = 0xAE,
        ["VK_VOLUME_UP"] = 0xAF,
        ["VK_MEDIA_NEXT_TRACK"] = 0xB0,
        ["VK_MEDIA_PREV_TRACK"] = 0xB1,
        ["VK_MEDIA_STOP"] = 0xB2,
        ["VK_MEDIA_PLAY_PAUSE"] = 0xB3,
        ["VK_LAUNCH_MAIL"] = 0xB4,
        ["VK_LAUNCH_APP2"] = 0xB7,
    };

    /// <summary>
    /// Keys whose virtual key MapVirtualKeyEx(MAPVK_VK_TO_VSC_EX) cannot turn into the make code of
    /// «About Keyboard Input», verified on Windows 11 with en-US. The scan code is still correct: Windows maps the
    /// catalog make code back to the same virtual key. This is why scan-code injection must use keys.win32.json
    /// for fixed keys instead of MapVirtualKeyEx (blueprint §7.7).
    /// </summary>
    private static readonly Dictionary<string, string> MapVirtualKeyQuirks = new(
        StringComparer.Ordinal
    )
    {
        ["insert"] =
            "shares its virtual key with Num 0 without Num Lock; returns the scan code without 0xE0",
        ["delete"] =
            "shares its virtual key with Num . without Num Lock; returns the scan code without 0xE0",
        ["home"] =
            "shares its virtual key with Num 7 without Num Lock; returns the scan code without 0xE0",
        ["end"] =
            "shares its virtual key with Num 1 without Num Lock; returns the scan code without 0xE0",
        ["pageup"] =
            "shares its virtual key with Num 9 without Num Lock; returns the scan code without 0xE0",
        ["pagedown"] =
            "shares its virtual key with Num 3 without Num Lock; returns the scan code without 0xE0",
        ["left"] =
            "shares its virtual key with Num 4 without Num Lock; returns the scan code without 0xE0",
        ["right"] =
            "shares its virtual key with Num 6 without Num Lock; returns the scan code without 0xE0",
        ["up"] =
            "shares its virtual key with Num 8 without Num Lock; returns the scan code without 0xE0",
        ["down"] =
            "shares its virtual key with Num 2 without Num Lock; returns the scan code without 0xE0",
        ["num.enter"] = "shares VK_RETURN with Enter; returns the scan code of Enter",
        ["printscreen"] = "returns 0x54, the SysRq code sent with Alt+Print Screen",
    };

    private static KeyCatalog Keys => KeyCatalog.Shared;

    [Fact]
    [Trait("Req", "CAT-001")]
    [Trait("Req", "NFR-004")]
    public void Every_key_has_exactly_one_Win32_mapping()
    {
        Keys.Keys.Select(k => k.Id)
            .Where(id => !Keys.Win32.ContainsKey(id))
            .ShouldBeEmpty("keys without mapping (CLCC006)");
        Keys.Win32.Keys.Where(id => !Keys.ById.ContainsKey(id))
            .ShouldBeEmpty("mappings of unknown keys (CLCC007)");
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void Only_character_keys_are_resolved_with_the_foreground_layout()
    {
        foreach (var key in Keys.Keys)
        {
            Keys.Win32[key.Id].IsCharacter.ShouldBe(key.IsCharacter, key.Id);
        }

        Keys.ReferenceLayout.ShouldBe("00000409");
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void Virtual_keys_are_the_Microsoft_constants_they_name()
    {
        foreach (var key in Keys.Keys.Where(k => !k.IsCharacter))
        {
            var mapping = Keys.Win32[key.Id];
            var expected =
                mapping.VkName!.Length == 1
                    ? mapping.VkName[0]
                    : MicrosoftVirtualKeys[mapping.VkName];
            mapping.Vk.ShouldBe(expected, key.Id + " (" + mapping.VkName + ")");
        }
    }

    [Fact]
    [Trait("Req", "EDI-009")]
    [Trait("Req", "NFR-004")]
    public void Generic_modifiers_are_sent_as_their_left_key_and_AltGr_as_right_Alt()
    {
        foreach (
            var (generic, left) in new[] { ("ctrl", "lctrl"), ("shift", "lshift"), ("alt", "lalt") }
        )
        {
            Keys.Win32[generic].ShouldBe(Keys.Win32[left], generic);
        }

        Keys.Win32["win"].VkName.ShouldBe("VK_LWIN");
        Keys.Win32["altgr"].VkName.ShouldBe("VK_RMENU");
        Keys.Win32["rctrl"].VkName.ShouldBe("VK_RCONTROL");
        Keys.Win32["rshift"].VkName.ShouldBe("VK_RSHIFT");
        Keys.Win32["rwin"].VkName.ShouldBe("VK_RWIN");
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void Physical_keys_are_unique_except_a_generic_modifier_and_its_left_side()
    {
        var shared = Keys
            .Keys.Where(k => !k.IsCharacter)
            .GroupBy(k => Keys.Win32[k.Id])
            .Where(g => g.Count() > 1)
            .Select(g => g.Select(k => k.Id).Order(StringComparer.Ordinal).ToArray())
            .ToList();

        shared.ShouldAllBe(ids =>
            ids.Length == 2
            && (
                string.Equals(Keys.ById[ids[0]].SideOf, ids[1], StringComparison.Ordinal)
                || string.Equals(Keys.ById[ids[1]].SideOf, ids[0], StringComparison.Ordinal)
            )
        );
        shared.Count.ShouldBe(3, "ctrl, shift and alt share their mapping with their left key");
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    public void Only_Pause_needs_the_E1_prefix()
    {
        Keys.Win32.Where(m => m.Value.PrefixE1).Select(m => m.Key).ShouldBe(["pause"]);
        Keys.Win32["pause"].Extended.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    [Trait("Req", "EJE-003")]
    [Trait("Req", "ATJ-004")]
    [SupportedOSPlatform("windows")]
    public void Scan_codes_match_MapVirtualKeyEx_for_the_en_US_layout()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Keyboard layouts are a Windows feature.");
        using var layout = KeyboardLayoutScope.Acquire(Keys.ReferenceLayout);
        var failures = new List<string>();

        foreach (var key in Keys.Keys.Where(k => !k.IsCharacter))
        {
            var mapping = Keys.Win32[key.Id];
            var forward = (int)
                NativeKeyboard.MapVirtualKeyExW(
                    (uint)mapping.Vk,
                    NativeKeyboard.MapVkToVscEx,
                    layout.Handle
                );
            var quirk = MapVirtualKeyQuirks.ContainsKey(key.Id);
            if (forward == mapping.ScanEx)
            {
                if (quirk)
                {
                    failures.Add($"{key.Id}: Windows now agrees; remove it from the quirk list.");
                }

                continue;
            }

            if (!quirk)
            {
                failures.Add(
                    $"{key.Id}: MapVirtualKeyEx gives 0x{forward:X4}, the catalog 0x{mapping.ScanEx:X4}."
                );
                continue;
            }

            // A quirk is acceptable only if Windows maps the catalog make code back to the same virtual key.
            var back = (int)
                NativeKeyboard.MapVirtualKeyExW(
                    (uint)mapping.ScanEx,
                    NativeKeyboard.MapVscToVkEx,
                    layout.Handle
                );
            if (back != mapping.Vk)
            {
                failures.Add(
                    $"{key.Id}: the make code 0x{mapping.ScanEx:X4} maps back to 0x{back:X2}, not 0x{mapping.Vk:X2}."
                );
            }
        }

        failures.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "NFR-004")]
    [Trait("Req", "EC-EJE-10")]
    [SupportedOSPlatform("windows")]
    public void Character_keys_exist_in_the_en_US_layout_except_the_Spanish_ones()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Keyboard layouts are a Windows feature.");
        using var layout = KeyboardLayoutScope.Acquire(Keys.ReferenceLayout);

        var missing = Keys
            .Keys.Where(k => k.IsCharacter)
            .Where(k => NativeKeyboard.VkKeyScanExW(k.Id["char:".Length], layout.Handle) == -1)
            .Select(k => k.Id);

        // «Ñ» has no key on en-US: Clícalo sends nothing and says so (EC-EJE-10).
        missing.ShouldBe(["char:ñ"]);
    }
}
