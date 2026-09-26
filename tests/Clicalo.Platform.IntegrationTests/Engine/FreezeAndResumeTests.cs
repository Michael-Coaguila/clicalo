using Clicalo.Application.Ports;
using Clicalo.Domain.KeySafety;
using Clicalo.Platform.Core.Injection;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// «Congelar y reanudar» (blueprint §7.10, item 3; INV-11): 10 000 seeded scenarios freeze the engine thread at a
/// random step, outside the gate or inside <c>SendInput</c> before or after it acts. The emergency then either takes
/// the gate (outside): the generation goes up, nothing is left down, and when the old thread resumes with its pending
/// effects every one of them is fenced; or it cannot (inside): it escalates to a restart, and the guardian's release
/// of the ledger leaves nothing down. Real threads, the real gate, a model of the system.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-007")]
public sealed class FreezeAndResumeTests
{
    /// <summary>The M2 criterion: 10 000 cases (blueprint §14, ADR-0004).</summary>
    public const int Cases = 10_000;

    [Fact]
    public void A_frozen_engine_never_presses_anything_after_the_emergency() =>
        Parallel.For(
            0,
            Cases,
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            seed =>
            {
                using var scenario = new LedgerScenario(seed);
                var random = new Random(seed + 1_000_000);
                var prefix = random.Next(60);
                for (var step = 0; step < prefix; step++)
                {
                    scenario.Send(scenario.NextTransition(step), scenario.Generation);
                }

                switch (seed % 3)
                {
                    case 0:
                        FrozenOutsideTheGate(scenario, random);
                        break;
                    default:
                        FrozenInsideSendInput(scenario, afterApply: seed % 3 == 2);
                        break;
                }
            }
        );

    private static void FrozenOutsideTheGate(LedgerScenario scenario, Random random)
    {
        // The old engine planned some effects and stopped before the gate.
        var old = scenario.Generation;
        var pending = new List<LedgerTransition>();
        for (var i = 0; i < 1 + random.Next(4); i++)
        {
            var transition = scenario.NextTransition(1_000 + i);
            scenario.Logical = transition.Ledger;
            pending.Add(transition);
        }

        scenario
            .Gate.TryEmergencyRelease(TimeSpan.FromSeconds(1), out var fresh)
            .ShouldBe(EmergencyOutcome.Released, scenario.Log);
        fresh.ShouldBeGreaterThan(old);
        scenario.System.IsEmpty.ShouldBeTrue(scenario.Log);

        // The zombie resumes: all of it is fenced (INV-11).
        foreach (var transition in pending)
        {
            if (!transition.Events.IsEmpty)
            {
                scenario
                    .Injector.Send(new EngineGeneration(old), transition.Events.AsSpan())
                    .Status.ShouldBe(InjectionStatus.Fenced, scenario.Log);
            }
        }

        scenario.System.IsEmpty.ShouldBeTrue(scenario.Log);
        scenario.Ledger.Snapshot().Slots.ShouldBeEmpty(scenario.Log);

        // The new engine starts empty and works under the new generation.
        scenario.Logical = KeyboardLedger.Empty;
        for (var step = 0; step < 20; step++)
        {
            scenario
                .Send(scenario.NextTransition(2_000 + step), fresh)
                .Status.ShouldNotBe(InjectionStatus.Fenced);
            scenario.ShouldSurviveDeathNow(" [new engine]");
        }
    }

    private static void FrozenInsideSendInput(LedgerScenario scenario, bool afterApply)
    {
        // Find a step that sends something, and freeze the engine thread inside that SendInput.
        LedgerTransition transition;
        var step = 3_000;
        do
        {
            transition = scenario.NextTransition(step++);
        } while (transition.Events.IsEmpty && step < 3_100);

        if (transition.Events.IsEmpty)
        {
            return;
        }

        scenario.System.FreezeOnCall = scenario.System.Batches.Count + 1;
        scenario.System.FreezeAfterApply = afterApply;
        var engine = new Thread(() => scenario.Send(transition, scenario.Generation))
        {
            IsBackground = true,
        };
        engine.Start();
        scenario.System.Frozen.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue(scenario.Log);

        // The hung thread holds the gate: the emergency cannot take it and escalates to a restart.
        scenario
            .Gate.TryEmergencyRelease(TimeSpan.Zero, out _)
            .ShouldBe(EmergencyOutcome.GateBusy, scenario.Log);
        scenario.ShouldSurviveDeathNow(
            afterApply ? " [frozen after SendInput]" : " [frozen before SendInput]"
        );

        scenario.System.Resume.Set();
        engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue(scenario.Log);
    }
}
