namespace Clicalo.Build;

/// <summary>Which tests a test step runs, by the <c>Requires=Desktop</c> trait.</summary>
internal enum TestSelection
{
    /// <summary>Everything that runs headless: excludes <c>[Trait("Requires", "Desktop")]</c>.</summary>
    WithoutDesktop,

    /// <summary>Only the tests that need an interactive desktop, with <c>CLICALO_DESKTOP_TESTS=1</c>.</summary>
    DesktopOnly,
}
