using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.Windowing.IntegrationTests.Desktop;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// S1 (docs/testing/spikes/S1.md, <c>PointerSetupTests</c> row): a surface shown by <c>NonActivatingWindow</c> has
/// every touch feedback of Windows disabled, so touching it draws no circle and no press-and-hold ring (ACC-007).
/// </summary>
[Trait("Requires", "Desktop")]
[Trait("Req", "ACC-007")]
[Collection(DesktopCollectionDefinition.Name)]
public sealed class PointerSetupTests(PointerDesktopFixture fixture)
    : IClassFixture<PointerDesktopFixture>
{
    [DesktopFact]
    public void Surfaces_have_no_touch_feedback() =>
        WpfThread
            .Invoke(() => PointerSetup.IsTouchFeedbackDisabled(fixture.Surface.SurfaceWindow))
            .ShouldBeTrue("NonActivatingWindow disables every FEEDBACK_TYPE before the first show");
}
