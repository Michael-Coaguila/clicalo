using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests;

/// <summary>
/// The one process setting of the product that has no runtime configuration or manifest equivalent: the mouse routed
/// through the pointer messages (<see cref="PointerSetup.EnableMouseInPointer"/>, blueprint §8.3), so a mouse click on
/// a surface becomes the same frames as a finger. WPF's stylus and touch stacks are switched off by the runtime
/// configuration of this project and per-monitor DPI awareness v2 comes from its manifest, as in the application.
/// </summary>
/// <remarks>
/// Pointer messages that no <see cref="PointerInputSource"/> consumes still reach WPF as mouse messages through
/// <c>DefWindowProc</c>, so the surfaces of the Windowing, Automation and Foreground folders see ordinary mouse input.
/// </remarks>
internal static class TestProcessSetup
{
    [ModuleInitializer]
    [SuppressMessage(
        "Usage",
        "CA2255:The 'ModuleInitializer' attribute should not be used in libraries",
        Justification = "A test executable: the mouse must be routed as pointer input before the first window."
    )]
    internal static void Initialize() => _ = PointerSetup.EnableMouseInPointer();
}
