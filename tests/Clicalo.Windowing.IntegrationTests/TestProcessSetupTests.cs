using System.Runtime.InteropServices;
using Clicalo.UI.Wpf.Pointer;

namespace Clicalo.Windowing.IntegrationTests;

/// <summary>
/// The test process runs with the process configuration of the product: WPF's stylus and touch stacks off from the
/// runtime configuration, the mouse routed as pointer input, and per-monitor DPI awareness v2 from the manifest.
/// </summary>
public sealed class TestProcessSetupTests
{
    /// <summary><c>DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2</c>.</summary>
    private const nint PerMonitorAwareV2 = -4;

    [Fact]
    public void The_stylus_switch_comes_from_the_runtime_configuration() =>
        // Runtime configuration properties are AppContext data; AppContext.SetSwitch would not appear here.
        AppContext.GetData(PointerSetup.DisableStylusAndTouchSupportSwitch).ShouldBe("true");

    [Fact]
    public void The_process_uses_the_pointer_configuration_of_the_product()
    {
        PointerSetup.IsStylusAndTouchSupportDisabled.ShouldBeTrue();
        PointerSetup.IsMouseInPointerEnabled.ShouldBeTrue();
    }

    [Fact]
    public void The_process_is_per_monitor_dpi_aware_v2() =>
        AreDpiAwarenessContextsEqual(GetDpiAwarenessContextForProcess(0), PerMonitorAwareV2)
            .ShouldBeTrue("the manifest of the test executable declares PerMonitorV2");

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern nint GetDpiAwarenessContextForProcess(nint process);

    [DllImport("user32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AreDpiAwarenessContextsEqual(nint first, nint second);
}
