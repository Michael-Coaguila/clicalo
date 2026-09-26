using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// Smoke tests of the accessible tile (blueprint §8.6): the peer, created in process on the WPF thread, exposes the
/// voice-numbered name and exactly the pattern of the tile. The detailed peer tests are in
/// <see cref="ShortcutTilePeerTests"/>; the S3 tests with FlaUI and a real window live next to them
/// (docs/testing/spikes/S3.md).
/// </summary>
public sealed class ShortcutTileContractTests
{
    [Fact]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "REG-06")]
    public void The_automation_name_starts_with_the_voice_number() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { AccessibleName = "Negrita", VoiceNumber = 4 };
            var peer = UIElementAutomationPeer.CreatePeerForElement(tile);

            peer.ShouldBeOfType<ShortcutTileAutomationPeer>();
            peer.GetName().ShouldBe("4 Negrita");
            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);

            tile.VoiceNumber = null;
            peer.GetName().ShouldBe("Negrita");
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void An_invoke_tile_exposes_only_Invoke_and_raises_its_event() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Negrita",
                Pattern = ShortcutTilePattern.Invoke,
            };
            var invoked = 0;
            tile.Invoked += (_, _) => invoked++;
            var peer = UIElementAutomationPeer.CreatePeerForElement(tile);

            peer.GetPattern(PatternInterface.Toggle).ShouldBeNull();
            peer.GetPattern(PatternInterface.ExpandCollapse).ShouldBeNull();
            var invoke = peer.GetPattern(PatternInterface.Invoke)
                .ShouldBeAssignableTo<IInvokeProvider>();
            invoke!.Invoke();
            WpfThread.DrainPendingWork();

            invoked.ShouldBe(1);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_toggle_tile_exposes_Toggle_with_the_state_of_the_view_model() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Shift",
                Pattern = ShortcutTilePattern.Toggle,
                ToggleState = ToggleState.Indeterminate,
            };
            var peer = UIElementAutomationPeer.CreatePeerForElement(tile);

            peer.GetPattern(PatternInterface.Invoke).ShouldBeNull();
            peer.GetPattern(PatternInterface.Toggle)
                .ShouldBeAssignableTo<IToggleProvider>()!
                .ToggleState.ShouldBe(ToggleState.Indeterminate);
        });
}
