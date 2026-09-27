namespace Clicalo.Build;

/// <summary>Which tests a test step runs, by the <c>Requires=Desktop</c> and <c>Category</c> traits.</summary>
internal enum TestSelection
{
    /// <summary>Everything that runs headless: excludes <c>[Trait("Requires", "Desktop")]</c>.</summary>
    WithoutDesktop,

    /// <summary>
    /// Only the tests that need an interactive desktop, with <c>CLICALO_DESKTOP_TESTS=1</c>, except the performance
    /// measurements (<c>Category=Perf</c>, <c>cl perf</c>).
    /// </summary>
    DesktopOnly,

    /// <summary>Only the performance measurements of <c>cl perf</c> (<c>Category=Perf</c>), on the desktop.</summary>
    PerfOnly,
}
