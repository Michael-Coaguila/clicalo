using Clicalo.Domain.Settings;
using Clicalo.Platform.Windows.Hotkeys;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.IntegrationTests.Foreground;

/// <summary>
/// The global shortcut against the real <c>RegisterHotKey</c> (BUR-005, user decision D10). It registers a
/// combination of the closed list for a moment and injects nothing, but another program on this desktop could own that
/// combination, so it only runs with the desktop tests.
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Req", "BUR-005")]
public sealed class GlobalPanelHotkeyDesktopTests : IDisposable
{
    private readonly SysEventsThread _thread = SysEventsThread.Start();

    public void Dispose() => _thread.Dispose();

    [Fact]
    public async Task It_is_off_until_a_combination_is_applied_and_off_again_with_none()
    {
        using var hotkey = new GlobalPanelHotkey(_thread);
        var chord = HotkeyChord.From(GlobalHotkeys.Find("ctrl-alt-f11")!.Keys);
        hotkey.Registered.ShouldBeNull();

        (await hotkey.ApplyAsync(chord)).ShouldBeTrue();
        hotkey.Registered.ShouldBe(chord);

        (await hotkey.ApplyAsync(null)).ShouldBeTrue();
        hotkey.Registered.ShouldBeNull();
    }

    [Fact]
    public async Task A_combination_another_owner_holds_is_refused_and_the_shortcut_stays_off()
    {
        using var owner = new GlobalPanelHotkey(_thread);
        using var other = SysEventsThread.Start();
        using var late = new GlobalPanelHotkey(other);
        var chord = HotkeyChord.From(GlobalHotkeys.Find("ctrl-alt-f11")!.Keys);
        (await owner.ApplyAsync(chord)).ShouldBeTrue();

        (await late.ApplyAsync(chord)).ShouldBeFalse();

        late.Registered.ShouldBeNull();
        (await owner.ApplyAsync(null)).ShouldBeTrue();
    }
}
