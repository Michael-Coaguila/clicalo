using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// Smoke tests of the pointer contract (blueprint §8.3, ADR-0006). The S1 and S2 pointer tests live next to them.
/// </summary>
public sealed class PointerContractTests
{
    [Fact]
    [Trait("Req", "ACC-007")]
    public void The_WPF_stylus_and_touch_stack_is_switched_off_by_its_documented_switch()
    {
        PointerSetup.DisableStylusAndTouchSupportSwitch.ShouldBe(
            "Switch.System.Windows.Input.Stylus.DisableStylusAndTouchSupport"
        );

        PointerSetup.DisableStylusAndTouchSupport();

        PointerSetup.IsStylusAndTouchSupportDisabled.ShouldBeTrue();
    }
}
