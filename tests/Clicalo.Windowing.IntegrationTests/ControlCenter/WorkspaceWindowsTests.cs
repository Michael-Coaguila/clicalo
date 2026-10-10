using System.Reflection;
using System.Windows;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.UI.Wpf.Workspace;
using Clicalo.UI.Wpf.Workspace.Welcome;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// The touch filter belongs to the panel only (TAC-007): the Control Center and the welcome are normal windows, so
/// their taps never go through the pointer layer where the filter runs (<see cref="PointerInputSource"/>, owned by the
/// surfaces of the panel). Checked on the types, without a window.
/// </summary>
public sealed class WorkspaceWindowsTests
{
    private const BindingFlags Fields =
        BindingFlags.Instance
        | BindingFlags.Public
        | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    [Theory]
    [Trait("Req", "TAC-007")]
    [InlineData(typeof(ControlCenterWindow))]
    [InlineData(typeof(WelcomeWindow))]
    public void The_control_center_and_the_welcome_are_normal_windows_outside_the_touch_filter(
        Type window
    )
    {
        window.BaseType.ShouldBe(typeof(Window), "a normal, activatable window");
        typeof(NonActivatingWindow).IsAssignableFrom(window).ShouldBeFalse();
        OwnsThePointerLayer(window).ShouldBeFalse("its taps are plain WPF input, unfiltered");
    }

    [Theory]
    [Trait("Req", "TAC-007")]
    [InlineData(typeof(PanelWindow))]
    [InlineData(typeof(TouchSurface))]
    public void The_surfaces_of_the_panel_own_the_pointer_layer_where_the_filter_runs(Type surface)
    {
        typeof(NonActivatingWindow).IsAssignableFrom(surface).ShouldBeTrue();
        OwnsThePointerLayer(surface).ShouldBeTrue();
    }

    private static bool OwnsThePointerLayer(Type type) =>
        type.GetFields(Fields).Any(static field => field.FieldType == typeof(PointerInputSource));
}
