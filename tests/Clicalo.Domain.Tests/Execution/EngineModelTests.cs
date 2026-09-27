using Clicalo.Domain.Execution;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Tests.Execution.Support;
using CsCheck;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// Model-based tests of the engine (blueprint §7.10, item 1; ADR-0004): 10 000 generated scenarios of up to 200 steps
/// with several contacts, timers, app switches, locks, «Release all», failed batches, test mode, pause, settings and
/// both injection modes. INV-1, INV-3, INV-4, INV-6 to INV-9 and INV-12 hold after every step, and a final
/// <c>Terminal(Exit)</c> leaves nothing down (SEG-007: «estado físico final vacío en todos los casos»). INV-2, INV-5
/// and INV-11 are checked with the real gate in Platform.IntegrationTests (death at every step, freeze and resume,
/// the zombie engine); INV-10 is a property of the layout, not of the engine (deviations.md, D-22).
/// </summary>
[Trait("Req", "SEG-007")]
[Trait("Req", "SEG-001")]
[Trait("Req", "REG-03")]
public sealed class EngineModelTests
{
    /// <summary>The M2 criterion: properties green with 10 000 cases (blueprint §14, ADR-0004).</summary>
    public const int Cases = 10_000;

    /// <summary>How CsCheck prints a reduced counterexample: its operations, in order.</summary>
    internal static string Print(EngineOp[] scenario) =>
        string.Join(' ', scenario.Select(static o => o.ToString()));

    private static EngineHarness Play(EngineOp[] scenario, bool check, bool failures = true)
    {
        var engine = new EngineHarness { SimulateFailures = failures };
        engine.Foreground();
        foreach (var op in scenario)
        {
            var before = engine.State;
            var from = engine.Effects.Count;
            var failedBefore = engine.FailedPresses;
            op.Play(engine, EngineScenarios.Pool);
            if (check)
            {
                EngineInvariants.Check(
                    engine,
                    op,
                    before,
                    engine.Effects.Skip(from).ToList(),
                    engine.FailedPresses != failedBefore
                );
            }
        }

        return engine;
    }

    [Fact]
    public void Every_invariant_holds_after_every_step_and_the_exit_leaves_nothing_down() =>
        EngineScenarios.Scenario.Sample(EveryInvariantHolds, iter: Cases, print: Print);

    /// <summary>
    /// The property of <see cref="Every_invariant_holds_after_every_step_and_the_exit_leaves_nothing_down"/> for one
    /// scenario; <see cref="EngineModelRegressionTests"/> replays the reduced counterexamples through it.
    /// </summary>
    internal static void EveryInvariantHolds(EngineOp[] scenario)
    {
        var engine = Play(scenario, check: true);

        engine.Apply(new EngineEvent.Terminal(TerminalReason.Exit));

        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
        engine.State.IsQuiet.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Every_release_uses_the_mode_vk_and_scan_of_its_press() =>
        EngineScenarios.Scenario.Sample(EveryReleaseMatchesItsPress, iter: Cases, print: Print);

    /// <summary>
    /// The property of <see cref="Every_release_uses_the_mode_vk_and_scan_of_its_press"/> for one scenario (INV-12):
    /// replaying what was sent, every key up names a key that is down with the same identity.
    /// </summary>
    internal static void EveryReleaseMatchesItsPress(EngineOp[] scenario)
    {
        var engine = Play(scenario, check: false, failures: false);
        engine.Apply(new EngineEvent.Terminal(TerminalReason.Exit));

        var down = new HashSet<InjectedKey>();
        foreach (var e in engine.Receiver.Log)
        {
            if (e.Kind == InjectedEventKind.KeyDown)
            {
                down.Add(e.Key);
            }
            else if (e.Kind == InjectedEventKind.KeyUp)
            {
                down.Remove(e.Key)
                    .ShouldBeTrue("released a key that was not pressed that way: " + e.Key);
            }
        }

        down.ShouldBeEmpty();
    }

    [Fact]
    public void The_scenarios_reach_the_interesting_states()
    {
        var reached = new HashSet<string>(StringComparer.Ordinal);
        EngineScenarios.Scenario.Sample(
            scenario =>
            {
                var engine = Play(scenario, check: false);
                lock (reached)
                {
                    foreach (var effect in engine.Effects)
                    {
                        reached.Add(effect.GetType().Name);
                    }

                    if (
                        engine
                            .Effects.OfType<EngineEffect.Inject>()
                            .Any(static i =>
                                i.Events.Any(static e => e.Key.Mode == InjectionMode.ScanCode)
                            )
                    )
                    {
                        reached.Add("scan code");
                    }

                    if (engine.Receiver.Log.Any(static e => e.Kind == InjectedEventKind.MenuMask))
                    {
                        reached.Add("menu mask");
                    }
                }
            },
            iter: 500,
            threads: 1
        );

        string[] expected =
        [
            "Inject",
            "TypeText",
            "ClipboardPaste",
            "MouseAction",
            "Launch",
            "SystemCommand",
            "Schedule",
            "CancelTimer",
            "Notice",
            "CountUsage",
            "scan code",
            "menu mask",
        ];
        expected.Except(reached, StringComparer.Ordinal).ShouldBeEmpty();
    }
}
