using System.Text.Json;
using Clicalo.Domain.Settings;
using Clicalo.TestKit;

namespace Clicalo.Domain.Tests.Settings;

/// <summary>
/// BUR-005 (user decision D10): the closed list of the global shortcut is the one of <c>global-hotkeys.json</c>, in its
/// order, and the setting only ever holds one of its ids.
/// </summary>
[Trait("Req", "BUR-005")]
public sealed class GlobalHotkeysTests
{
    [Fact]
    public void The_domain_list_is_the_catalog_in_its_order()
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepoPaths.Data, "catalogs", "global-hotkeys.json"))
        );
        var catalog = document
            .RootElement.GetProperty("hotkeys")
            .EnumerateArray()
            .Select(static h =>
                h.GetProperty("id").GetString()
                + "="
                + string.Join(
                    '+',
                    h.GetProperty("keys").EnumerateArray().Select(static k => k.GetString())
                )
            )
            .ToList();

        GlobalHotkeys
            .All.Select(static h =>
                h.Id + "=" + string.Join('+', h.Keys.Strokes.Select(static s => s.Key.Value))
            )
            .ShouldBe(catalog);
        GlobalHotkeys.Default.ShouldBeSameAs(GlobalHotkeys.All[0]);
    }

    [Fact]
    public void It_is_off_by_default_with_the_first_combination()
    {
        SettingsSchema.Defaults.GlobalHotkey.ShouldBe(
            new GlobalHotkeySettings(Enabled: false, GlobalHotkeys.Default.Id)
        );
        GlobalHotkeys.Find("ctrl-alt-f10")!.Id.ShouldBe("ctrl-alt-f10");
        GlobalHotkeys.Find("ctrl-shift-m").ShouldBeNull();
        GlobalHotkeys.Find(null).ShouldBeNull();
    }

    [Fact]
    public void Only_a_combination_of_the_list_can_be_written_or_loaded()
    {
        var defaults = SettingsSchema.Defaults;

        SettingsSchema
            .Write(defaults, SettingPaths.GlobalHotkeyCombo, "ctrl-alt-f11", out var chosen)
            .ShouldBe(SettingWriteStatus.Written);
        chosen.GlobalHotkey.Combo.ShouldBe("ctrl-alt-f11");
        SettingsSchema
            .Write(defaults, SettingPaths.GlobalHotkeyCombo, "ctrl-shift-m", out _)
            .ShouldBe(SettingWriteStatus.OutOfRange);

        var unknown = defaults with
        {
            GlobalHotkey = new GlobalHotkeySettings(Enabled: true, "win-x"),
        };
        var repaired = SettingsSchema.Clamp(unknown, out var changed);

        changed.ShouldBe([SettingPaths.GlobalHotkeyCombo]);
        repaired.GlobalHotkey.ShouldBe(new GlobalHotkeySettings(true, GlobalHotkeys.Default.Id));
    }
}
