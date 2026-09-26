using Clicalo.Domain.Library;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Tests.Library;

/// <summary>Macro waits stay inside Timings.Macro.MacroWaitRange (invariant I6 of blueprint §6.2).</summary>
[Trait("Req", "EJE-010")]
public sealed class WaitStepTests
{
    [Fact]
    public void A_wait_inside_the_range_is_kept()
    {
        var range = Timings.Macro.MacroWaitRange;

        new WaitStep(range.Min).Duration.ShouldBe(range.Min);
        new WaitStep(range.Max).Duration.ShouldBe(range.Max);
    }

    [Fact]
    public void A_wait_outside_the_range_is_a_defect_of_the_caller()
    {
        var range = Timings.Macro.MacroWaitRange;

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new WaitStep(range.Min - TimeSpan.FromTicks(1))
        );
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new WaitStep(range.Max + TimeSpan.FromTicks(1))
        );
    }
}
