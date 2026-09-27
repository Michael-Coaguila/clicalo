using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Tests.Execution.Support;

namespace Clicalo.Domain.Tests.Execution;

/// <summary>
/// The model's receiver forgives the blind releases of a batch <c>SendInput</c> took only in part (INV-5), and only
/// while the engine recovers from it: afterwards a double press or a spurious release of those keys is an anomaly
/// again, so the 10 000-case properties (INV-1, INV-12) keep watching Ctrl and Shift after the first failure.
/// </summary>
[Trait("Req", "SEG-007")]
[Trait("Req", "ATJ-004")]
public sealed class ReceiverToleranceTests
{
    [Fact]
    public void A_failed_batch_blinds_the_receiver_only_until_the_engine_recovered()
    {
        var engine = new EngineHarness(
            EngineHarness.DefaultConfig with
            {
                InterEventDelay = TimeSpan.Zero,
            }
        );
        engine.Foreground();
        engine.FailNextPress = 1;

        engine.Press(Shortcuts.Hold("save", "ctrl", "shift"));

        engine.FailedPresses.ShouldBe(1);
        engine.Receiver.IsEmpty.ShouldBeTrue();
        engine.Receiver.Anomalies.ShouldBeEmpty();
        var ctrl = engine.Receiver.Log.First(static e => e.Kind == InjectedEventKind.KeyDown).Key;

        engine.Receiver.Apply([InjectedEvent.KeyUp(ctrl)]);
        engine.Receiver.Apply([InjectedEvent.KeyDown(ctrl), InjectedEvent.KeyDown(ctrl)]);

        engine.Receiver.Anomalies.Count.ShouldBe(2);
    }
}
