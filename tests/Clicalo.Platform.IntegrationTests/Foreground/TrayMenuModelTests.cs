using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;
using Clicalo.Platform.Windows.Hotkeys;
using Clicalo.Platform.Windows.Tray;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// What the tray shows in each state (BUR-003, BUR-004) and how the closed list of global shortcuts becomes a
/// <c>RegisterHotKey</c> registration (BUR-005). Headless: nothing is shown and nothing is registered.
/// </summary>
public sealed class TrayMenuModelTests
{
    [Fact]
    [Trait("Req", "BUR-003")]
    [Trait("Req", "BUR-004")]
    public void The_menu_is_show_or_hide_control_center_release_all_pause_and_exit()
    {
        var entries = TrayMenuModel.Entries(new TrayState(true, AnythingHeld: false, false));

        entries
            .Select(static e => e.Command)
            .ShouldBe([
                TrayCommand.ShowHide,
                TrayCommand.ControlCenter,
                TrayCommand.ReleaseAll,
                TrayCommand.Pause,
                TrayCommand.Exit,
            ]);
        entries
            .Select(static e => e.Text)
            .ShouldBe([L.HidePanel, L.Cc, L.ReleaseAll, L.PauseApp, L.ExitApp]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Req", "BUR-003")]
    public void Release_all_is_enabled_only_while_something_is_held(bool held)
    {
        var entries = TrayMenuModel.Entries(new TrayState(true, held, Paused: false));

        entries.Single(static e => e.Command == TrayCommand.ReleaseAll).IsEnabled.ShouldBe(held);
        entries
            .Where(static e => e.Command != TrayCommand.ReleaseAll)
            .ShouldAllBe(e => e.IsEnabled);
    }

    [Fact]
    [Trait("Req", "BUR-003")]
    public void A_hidden_panel_offers_to_show_it_and_the_icon_says_it_is_hidden()
    {
        var state = new TrayState(PanelVisible: false, AnythingHeld: false, Paused: false);

        TrayMenuModel.Entries(state)[0].Text.ShouldBe(L.Restore);
        TrayMenuModel.Tooltip(state).ShouldBe(L.TrayHidden);
        TrayMenuModel.Tooltip(state with { PanelVisible = true }).ShouldBe(L.AppName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Req", "BUR-004")]
    [Trait("Req", "BUR-005")]
    public void Paused_the_menu_offers_to_resume_and_to_show_the_panel_and_the_icon_says_paused(
        bool panelVisible
    )
    {
        var state = new TrayState(panelVisible, AnythingHeld: false, Paused: true);

        var entries = TrayMenuModel.Entries(state);

        entries.Single(static e => e.Command == TrayCommand.Pause).Text.ShouldBe(L.ResumeApp);
        entries.Single(static e => e.Command == TrayCommand.ShowHide).Text.ShouldBe(L.Restore);
        TrayMenuModel.Tooltip(state).ShouldBe(L.TrayPaused);
    }

    [Fact]
    [Trait("Req", "BUR-005")]
    public void Every_combination_of_the_closed_list_can_be_registered_and_none_uses_the_Windows_key()
    {
        var registrations = GlobalHotkeys
            .All.Select(static hotkey => HotkeyChord.From(hotkey.Keys))
            .ToList();

        registrations.ShouldAllBe(static chord => chord != null);
        registrations.Distinct().Count().ShouldBe(GlobalHotkeys.All.Length);
        registrations.ShouldAllBe(static chord =>
            (chord!.Value.Modifiers & ~(HotkeyChord.Control | HotkeyChord.Alt | HotkeyChord.Shift))
            == 0
        );
    }

    [Fact]
    [Trait("Req", "BUR-005")]
    public void The_preselected_combination_is_ctrl_alt_space()
    {
        HotkeyChord
            .From(GlobalHotkeys.Default.Keys)
            .ShouldBe(new HotkeyChord(HotkeyChord.Control | HotkeyChord.Alt, 0x20));
        HotkeyChord
            .From(GlobalHotkeys.Find("ctrl-alt-f10")!.Keys)
            .ShouldBe(new HotkeyChord(HotkeyChord.Control | HotkeyChord.Alt, 0x79));
    }

    [Theory]
    [InlineData("win", "space")]
    [InlineData("ctrl", "shift", "m")]
    [InlineData("space")]
    [InlineData("ctrl", "alt")]
    [InlineData("ctrl", "f25")]
    [Trait("Req", "BUR-005")]
    public void A_combination_outside_what_the_list_allows_is_refused(params string[] keys)
    {
        var chord = KeyChord.Create(keys.Select(static key => new KeyStroke(new KeyId(key))));

        HotkeyChord.From(chord).ShouldBeNull();
    }
}
