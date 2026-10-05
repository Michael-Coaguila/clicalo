namespace Clicalo.Build;

/// <summary>
/// Which tests a test step runs, by the <c>Requires=Desktop</c> and <c>Category</c> traits. Only
/// <see cref="Deterministic"/> gates a pull request; the other selections run every night (nightly.yml).
/// </summary>
internal enum TestSelection
{
    /// <summary>
    /// The pull request tier (<c>cl check</c>, <c>cl test</c>, <c>cl fast</c>): everything headless and deterministic.
    /// Excludes <c>[Trait("Requires", "Desktop")]</c>, the chaos tests (<c>Category=Chaos</c>), the measurements
    /// (<c>Category=Perf</c>) and the quarantined tests (<c>Category=Quarantine</c>).
    /// </summary>
    Deterministic,

    /// <summary>
    /// Only the tests that need an interactive desktop, with <c>CLICALO_DESKTOP_TESTS=1</c>, except the performance
    /// measurements (<c>Category=Perf</c>, <c>cl perf</c>) and the quarantined tests (<c>cl quarantine</c>).
    /// </summary>
    DesktopOnly,

    /// <summary>Only the performance measurements of <c>cl perf</c> (<c>Category=Perf</c>), on the desktop.</summary>
    PerfOnly,

    /// <summary>
    /// Only the quarantined tests of <c>cl quarantine</c> (<c>Category=Quarantine</c>), headless or on the desktop,
    /// with <c>CLICALO_DESKTOP_TESTS=1</c>; never the measurements.
    /// </summary>
    QuarantineOnly,
}
