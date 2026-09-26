using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using Clicalo.TestKit.Snapshots;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using FlaUI.Core.Definitions;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// The S3 lab (nine tiles and the notice bar) checked in process with the UIA rules and a text snapshot of its
/// tree, on every PR and without a desktop (blueprint §10.2). The same rules run through a real UIA client in
/// <see cref="UiaTreeTests"/>.
/// </summary>
public sealed class InProcessTreeTests
{
    private static readonly Size LabSize = new(340, 330);

    [Theory]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "REG-02")]
    [InlineData(false)]
    [InlineData(true)]
    public void The_lab_tree_follows_the_UIA_rules(bool voiceNumbers) =>
        WpfThread.Invoke(() =>
        {
            using var lab = new TileLab();
            lab.SetVoiceNumbers(voiceNumbers);
            lab.LayOut(LabSize);

            UiaVerifier.ShouldPass(Snapshot(lab), LabExpectations.Create(voiceNumbers));
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-003")]
    public void States_set_through_the_patterns_are_what_the_tree_reports() =>
        WpfThread.Invoke(() =>
        {
            using var lab = new TileLab();
            lab.LayOut(LabSize);

            Pattern<IToggleProvider>(lab, "tile.shift", PatternInterface.Toggle).Toggle();
            Pattern<IToggleProvider>(lab, "tile.shift", PatternInterface.Toggle).Toggle();
            Pattern<IToggleProvider>(lab, "tile.holdCtrl", PatternInterface.Toggle).Toggle();
            Pattern<IExpandCollapseProvider>(lab, "tile.profile", PatternInterface.ExpandCollapse)
                .Expand();
            lab.Announcer.Announce("Copiado", AnnouncementUrgency.Assertive);

            UiaVerifier.ShouldPass(
                Snapshot(lab),
                LabExpectations.Create(
                    voiceNumbers: false,
                    shift: ToggleState.Indeterminate,
                    holdCtrl: ToggleState.On,
                    profileExpanded: true,
                    notice: "Copiado",
                    noticeLive: LiveSetting.Assertive
                )
            );
        });

    [Fact]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-009")]
    public void The_tree_text_with_voice_numbers_matches_its_snapshot() =>
        WpfThread.Invoke(() =>
        {
            using var lab = new TileLab();
            lab.SetVoiceNumbers(true);
            lab.LayOut(LabSize);

            TextSnapshot.Match(UiaTreeText.Format(Snapshot(lab)), "voice-numbers-on");
        });

    [Fact]
    [Trait("Req", "REG-06")]
    public void The_tree_text_without_voice_numbers_matches_its_snapshot() =>
        WpfThread.Invoke(() =>
        {
            using var lab = new TileLab();
            lab.SetVoiceNumbers(false);
            lab.LayOut(LabSize);

            TextSnapshot.Match(UiaTreeText.Format(Snapshot(lab)), "voice-numbers-off");
        });

    private static UiaNode Snapshot(TileLab lab) =>
        PeerSnapshot.CaptureElements("lab", [.. lab.Tiles, lab.Notice]);

    private static T Pattern<T>(TileLab lab, string id, PatternInterface pattern)
        where T : class =>
        (T)UIElementAutomationPeer.CreatePeerForElement(lab.Tile(id)).GetPattern(pattern);
}
