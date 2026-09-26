using System.Reflection;
using Clicalo.Application.Ports;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Windowing.IntegrationTests.Windowing;

/// <summary>
/// Smoke tests of the windowing contract (blueprint §3.5): they run headless and pin what other rules depend on.
/// The S1 desktop tests live next to them (docs/testing/spikes/S1.md).
/// </summary>
public sealed class WindowingContractTests
{
    [Fact]
    [Trait("Req", "REG-01")]
    public void NonActivatingWindow_keeps_the_metadata_name_that_CLC0001_binds_to() =>
        typeof(NonActivatingWindow).FullName.ShouldBe(
            "Clicalo.UI.Wpf.Windowing.NonActivatingWindow",
            "CLC0001 finds the base class of the surfaces by this name (docs/guides/analyzers.md)."
        );

    [Fact]
    [Trait("Req", "REG-01")]
    public void Surfaces_cannot_skip_the_non_activation_setup()
    {
        var setup = typeof(NonActivatingWindow).GetMethod(
            "OnSourceInitialized",
            BindingFlags.Instance | BindingFlags.NonPublic,
            [typeof(EventArgs)]
        );

        setup.ShouldNotBeNull();
        setup.DeclaringType.ShouldBe(typeof(NonActivatingWindow));
        setup.IsFinal.ShouldBeTrue("OnSourceInitialized is sealed (blueprint §3.5).");
    }

    [Fact]
    [Trait("Req", "REG-01")]
    public void A_new_guard_has_counted_no_violation()
    {
        var guard = new ActivationGuard(new NoLeaseArbiter(), TimeProvider.System);

        guard.Violations.ShouldBe(0);
        ActivationGuard.MetricName.ShouldBe("reg01.violations");
    }

    private sealed class NoLeaseArbiter : IActivationArbiter
    {
        public bool IsActivationLeased(WindowToken window) => false;

        public void ReportViolation(ActivationViolation violation) { }
    }
}
