using Clicalo.Domain.Tests.Execution.Support;
using CsCheck;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The reduced counterexamples of <see cref="EngineModelTests"/>, kept as regressions (blueprint §7.10, item 1: «Los
/// contraejemplos reducidos se guardan como regresiones»). When a property fails, CsCheck reduces the scenario and prints
/// its seed («Set seed: "…"»); the seed goes into <see cref="Invariants"/> or <see cref="Releases"/> with what it caught,
/// and replays that exact scenario on every run, before and apart from the random cases. Each seed stands for the
/// scenario the generator of <see cref="EngineScenarios"/> gives it: when the generator changes, the seeds are found
/// again with the same mutations (docs/testing/property-regressions.md).
/// </summary>
[Trait("Req", "SEG-007")]
[Trait("Req", "REG-03")]
public sealed class EngineModelRegressionTests
{
    /// <summary>Seeds of <see cref="EngineModelTests.EveryInvariantHolds"/> and the defect each one caught.</summary>
    public static TheoryData<string, string> Invariants =>
        new()
        {
            {
                "3cnthrQVDtP1",
                "KeyboardLedger.Release skipped the first key of a holder: it stayed down after a switch (INV-1)"
            },
            {
                "4kPlB-gLWxb6",
                "a scan-code key up lost its extended flag: the key pressed stayed down (INV-1, INV-12)"
            },
            {
                "fphVLHOKEBW4",
                "KeyboardLedger.ReleaseAll sent no button up: a drag left the button down (INV-1, INV-3)"
            },
        };

    /// <summary>Seeds of <see cref="EngineModelTests.EveryReleaseMatchesItsPress"/> and the defect each one caught.</summary>
    public static TheoryData<string, string> Releases =>
        new()
        {
            {
                "694-R_qiLQI7",
                "a scan-code key up lost its extended flag: released a key that was not pressed that way (INV-12)"
            },
        };

    [Theory]
    [MemberData(nameof(Invariants))]
    public void A_reduced_counterexample_keeps_every_invariant(string seed, string caught)
    {
        _ = caught;
        EngineScenarios.Scenario.Sample(
            EngineModelTests.EveryInvariantHolds,
            seed: seed,
            iter: 1,
            threads: 1,
            print: EngineModelTests.Print
        );
    }

    [Theory]
    [MemberData(nameof(Releases))]
    [Trait("Req", "ATJ-004")]
    public void A_reduced_counterexample_releases_what_it_pressed(string seed, string caught)
    {
        _ = caught;
        EngineScenarios.Scenario.Sample(
            EngineModelTests.EveryReleaseMatchesItsPress,
            seed: seed,
            iter: 1,
            threads: 1,
            print: EngineModelTests.Print
        );
    }
}
