using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// «Muerte en cada paso» (blueprint §7.10, item 2; ADR-0004): a property over 10 000 seeded scenarios of up to 200 key
/// operations through the real gate and ledger v2, whose failures are reduced to the fewest steps and kept as
/// regressions (<see cref="Counterexamples"/>). At every step, and inside every <c>SendInput</c> before and after it acts, the
/// guardian's release of what the ledger records (the same <see cref="LedgerRelease"/> Sentinel uses) leaves nothing
/// down (INV-2), whatever <c>SendInput</c> took. Nothing is injected: the system is a model.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "SEG-007")]
[Trait("Req", "REG-03")]
public sealed class DeathAtEveryStepTests
{
    /// <summary>The M2 criterion: 10 000 cases (blueprint §14, ADR-0004).</summary>
    public const int Cases = 10_000;

    [Fact]
    public void Whenever_the_process_dies_the_guardian_leaves_nothing_down() =>
        Counterexamples.ForAll(
            Cases,
            static seed => (Seed: seed, Steps: 1 + (seed % 200)),
            GuardianLeavesNothingDown,
            static c => Enumerable.Range(1, c.Steps - 1).Select(steps => (c.Seed, steps)),
            static c =>
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"(seed {c.Seed}, {c.Steps} steps)"
                )
        );

    /// <summary>
    /// The reduced counterexamples of <see cref="Whenever_the_process_dies_the_guardian_leaves_nothing_down"/>, kept
    /// as regressions (blueprint §7.10, item 1): the seed and the steps the failure printed, and what each one caught.
    /// </summary>
    [Theory]
    [InlineData(
        1,
        2,
        "InjectionGate did not record a mouse button down before SendInput: a death inside it left the button down (INV-2)"
    )]
    public void A_reduced_counterexample_leaves_nothing_down(int seed, int steps, string caught)
    {
        _ = caught;
        GuardianLeavesNothingDown((seed, steps));
    }

    private static void GuardianLeavesNothingDown((int Seed, int Steps) scenarioCase)
    {
        using var scenario = new LedgerScenario(scenarioCase.Seed);
        scenario.System.BeforeApply = (_, _) => scenario.ShouldSurviveDeathNow(" [inside, before]");
        scenario.System.AfterApply = (_, _) => scenario.ShouldSurviveDeathNow(" [inside, after]");
        for (var step = 0; step < scenarioCase.Steps; step++)
        {
            var transition = scenario.NextTransition(step);
            scenario.MaybeFailNextSend(transition.Events);
            scenario
                .Send(transition, scenario.Generation)
                .Status.ShouldNotBe(Application.Ports.InjectionStatus.Fenced);
            scenario.ShouldCoverWhatIsDown();
            scenario.ShouldSurviveDeathNow(" [between steps]");
        }

        // A clean exit releases what the logical ledger holds; what a failed send left behind is the guardian's.
        scenario.Send(scenario.Logical.ReleaseAll(), scenario.Generation);
        scenario.ShouldSurviveDeathNow(" [after exit]");
    }

    [Fact]
    public void A_scenario_is_reproducible_from_its_seed()
    {
        using var first = new LedgerScenario(42);
        using var second = new LedgerScenario(42);
        for (var step = 0; step < 50; step++)
        {
            first.Send(first.NextTransition(step), 1);
            second.Send(second.NextTransition(step), 1);
        }

        first.Log.ShouldBe(second.Log);
        first.System.Keys.ShouldBe(second.System.Keys, ignoreOrder: true);
    }

    [Fact]
    public void The_scenarios_reach_shared_keys_both_modes_buttons_and_failed_sends()
    {
        var sawPartial = false;
        var sawBlocked = false;
        var sawShared = false;
        var sawScan = false;
        var sawButton = false;
        for (var seed = 0; seed < 300; seed++)
        {
            using var scenario = new LedgerScenario(seed);
            for (var step = 0; step < 200; step++)
            {
                var transition = scenario.NextTransition(step);
                scenario.MaybeFailNextSend(transition.Events);
                scenario.Send(transition, 1);
                sawShared |= scenario.Logical.Holders.Values.Any(static h => h.Count > 1);
                sawScan |= scenario
                    .Ledger.Snapshot()
                    .Slots.Any(static s =>
                        (s.Key.Attributes & LedgerKeyAttributes.ScanCodeMode)
                        != LedgerKeyAttributes.None
                    );
                sawButton |= scenario.Ledger.MouseButtons != LedgerMouseButtons.None;
            }

            sawPartial |= scenario.Log.Contains("(partial)", StringComparison.Ordinal);
            sawBlocked |= scenario.Log.Contains("(blocked)", StringComparison.Ordinal);
        }

        sawPartial.ShouldBeTrue();
        sawBlocked.ShouldBeTrue();
        sawShared.ShouldBeTrue();
        sawScan.ShouldBeTrue();
        sawButton.ShouldBeTrue();
    }
}
