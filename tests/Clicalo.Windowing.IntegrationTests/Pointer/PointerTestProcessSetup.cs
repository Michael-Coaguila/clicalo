using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Pointer;

/// <summary>
/// Configures the pointer input of this test process like the product before any window exists (blueprint §8.3,
/// ADR-0006): WPF's stylus and touch stacks off, so touch reaches the windows as <c>WM_POINTER*</c>, and the mouse
/// routed through the same messages (<see cref="PointerSetup.EnableMouseInPointer"/>), so a mouse click on a surface
/// becomes the same frames as a finger. Pointer messages that no <see cref="PointerInputSource"/> consumes still reach
/// WPF as mouse messages.
/// </summary>
/// <remarks>
/// A module initializer, because the test project's runtime configuration is a shared file: the integration can move
/// the switch there (<c>RuntimeHostConfigurationOption</c>) and keep only the mouse call.
/// </remarks>
internal static class PointerTestProcessSetup
{
    [ModuleInitializer]
    [SuppressMessage(
        "Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "A test executable: the settings must precede the first WPF window of any test."
    )]
    internal static void Initialize()
    {
        PointerSetup.DisableStylusAndTouchSupport();
        _ = PointerSetup.EnableMouseInPointer();
    }
}
