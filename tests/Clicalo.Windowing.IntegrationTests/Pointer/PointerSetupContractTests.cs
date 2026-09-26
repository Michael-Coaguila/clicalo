using System.Windows;
using System.Windows.Interop;
using Clicalo.Application.Ports;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// The process and window setup of the pointer layer (ACC-007, blueprint §8.3): no WPF touch stack, the mouse routed
/// as pointer messages, and no touch feedback of Windows on a surface (no circle, no press-and-hold ring).
/// </summary>
[Trait("Req", "ACC-007")]
public sealed class PointerSetupContractTests
{
    [Fact]
    public void The_test_process_uses_the_pointer_configuration_of_the_product()
    {
        PointerSetup.IsStylusAndTouchSupportDisabled.ShouldBeTrue();
        PointerSetup.IsMouseInPointerEnabled.ShouldBeTrue();
        PointerSetup.EnableMouseInPointer().ShouldBeTrue("enabling it again is harmless");
    }

    [Fact]
    public void Disabling_touch_feedback_switches_off_every_feedback_type_of_the_window() =>
        WpfThread.Invoke(() =>
        {
            // A hidden window: its handle exists, nothing is shown or activated.
            var window = new Window { ShowActivated = false };
            try
            {
                var token = new WindowToken(new WindowInteropHelper(window).EnsureHandle());

                PointerSetup.IsTouchFeedbackDisabled(token).ShouldBeFalse();
                PointerSetup.DisableTouchFeedback(token).ShouldBeTrue();
                PointerSetup.IsTouchFeedbackDisabled(token).ShouldBeTrue();
            }
            finally
            {
                window.Close();
            }
        });

    [Fact]
    public void The_feedback_calls_need_a_window()
    {
        Should.Throw<ArgumentException>(() => PointerSetup.DisableTouchFeedback(WindowToken.None));
        Should.Throw<ArgumentException>(() =>
            PointerSetup.IsTouchFeedbackDisabled(WindowToken.None)
        );
    }
}
