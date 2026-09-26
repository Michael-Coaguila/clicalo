using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// Configures this test process like the product before any window exists: WPF's stylus and touch stacks off (touch
/// arrives as <c>WM_POINTER*</c>, blueprint §8.3) and per-monitor DPI awareness v2, so window rectangles, pointer
/// coordinates and <c>WM_DPICHANGED</c> behave in physical pixels as they will in the application (§3.7, ACC-008).
/// </summary>
/// <remarks>
/// A module initializer, because the test project's runtime configuration and manifest are shared files: the
/// integration moves both settings there (see docs/testing/spikes/S1.md).
/// </remarks>
internal static class TestProcessSetup
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
        _ = NativeSurface.SetProcessDpiAwarenessContext(NativeSurface.PerMonitorAwareV2);
    }
}
