using Clicalo.Domain.Execution;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// The key safety invariants of blueprint §7.5 that the pure engine must keep after every step (INV-1, INV-3, INV-4,
/// INV-6 to INV-9, INV-12); INV-2, INV-5 and INV-11 are checked with the real gate in Platform.IntegrationTests.
/// </summary>
internal static class EngineInvariants
{
    public static void Check(
        EngineHarness engine,
        EngineOp op,
        EngineState before,
        IReadOnlyList<EngineEffect> effects,
        bool pressFailed
    )
    {
        var state = engine.State;
        var ledger = state.Keys;
        var receiver = engine.Receiver;

        // INV-1 (soundness): what is down is exactly what the holders hold, with matching reference counts.
        receiver.Anomalies.ShouldBeEmpty("INV-1/INV-12: " + op);
        receiver.Keys.ToHashSet().SetEquals(ledger.Holders.Keys).ShouldBeTrue("INV-1 keys: " + op);
        receiver.Buttons.ShouldBe(ledger.HeldButtons, "INV-1 buttons: " + op);
        foreach (var (key, holders) in ledger.Holders)
        {
            holders.IsEmpty.ShouldBeFalse("INV-1: " + op);
            foreach (var holder in holders)
            {
                ledger.Items[holder].Keys.Items.ShouldContain(key, "INV-1: " + op);
            }
        }

        foreach (var item in ledger.Items.Values)
        {
            foreach (var key in item.Keys)
            {
                ledger.Holders[key].ShouldContain(item.Holder, "INV-1: " + op);
            }

            // INV-4 (bounded life): every holder has a deadline no later than its press plus the limit it follows.
            if (item.InheritsGlobalLimit)
            {
                item.DeadlineTicks.ShouldBe(
                    engine.Config.MaxHold is { } max ? item.SinceTicks + max.Ticks : null,
                    "INV-4: " + op
                );
            }
            else if (item.DeadlineTicks is null)
            {
                item.Shortcut?.Value.ShouldBe(
                    "holdnever",
                    "INV-4 (only «Never» has no deadline): " + op
                );
            }

            if (op.Kind == EngineOpKind.Wait && item.DeadlineTicks is { } deadline)
            {
                deadline.ShouldBeGreaterThan(engine.Now, "INV-4 (expired and still held): " + op);
            }
        }

        var epoch = state.Foreground?.Epoch;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case EngineEffect.Inject { IsRelease: true } release:
                    // INV-8: releases carry no epoch or target: nothing may filter them.
                    release.Epoch.ShouldBeNull("INV-8: " + op);
                    release.RequiredForeground.ShouldBeNull("INV-8: " + op);
                    release.Events.ShouldAllBe(e => e.IsRelease, "INV-8: " + op);
                    break;
                case EngineEffect.Inject press:
                    // INV-6: a press carries the current epoch and never reaches an elevated target.
                    press.Epoch.ShouldBe(epoch, "INV-6: " + op);
                    state.Foreground!.Elevation.ShouldNotBe(
                        ElevationState.TargetElevated,
                        "INV-6: " + op
                    );
                    AssertNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.TypeText typed:
                    typed.Epoch.ShouldBe(epoch!.Value, "INV-6: " + op);
                    AssertNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.MouseAction mouse:
                    mouse.Epoch.ShouldBe(epoch!.Value, "INV-6: " + op);
                    AssertNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.ClipboardPaste
                or EngineEffect.Launch
                or EngineEffect.SystemCommand:
                    AssertNothingSentWhileSilenced(state, op);
                    break;
            }
        }

        // INV-9: a contact's holder goes away only by its own end, a deadline, a terminal event or «Release all».
        foreach (
            var gone in before.Keys.Items.Values.Where(i =>
                i.ContactId is not null && !ledger.Items.ContainsKey(i.Holder)
            )
        )
        {
            var allowed =
                pressFailed
                || op.Kind switch
                {
                    EngineOpKind.Lift => gone.ContactId == 1 + (op.A % 3),
                    EngineOpKind.Wait
                    or EngineOpKind.Terminal
                    or EngineOpKind.ReleaseAll
                    or EngineOpKind.Switch
                    or EngineOpKind.TestMode
                    or EngineOpKind.Pause
                    or EngineOpKind.FailPress => true,
                    _ => false,
                };
            allowed.ShouldBeTrue("INV-9: " + gone.Holder + " released by " + op);
        }

        // INV-3: after a terminal event or «Release all», nothing is held, queued, repeating or running.
        if (op.Kind is EngineOpKind.Terminal or EngineOpKind.ReleaseAll)
        {
            state.IsQuiet.ShouldBeTrue("INV-3: " + op);
            receiver.IsEmpty.ShouldBeTrue("INV-3: " + op);
        }

        // Liveness: queued steps always have their timer, so nothing waits for ever.
        if (!state.Outbox.IsEmpty)
        {
            engine.Timers.ShouldContainKey(new TimerKey("outbox"), "outbox without timer: " + op);
        }
    }

    // INV-7: with test mode or pause, nothing but releases goes out.
    private static void AssertNothingSentWhileSilenced(EngineState state, EngineOp op)
    {
        state.TestMode.ShouldBeFalse("INV-7 (test mode): " + op);
        state.Paused.ShouldBeFalse("INV-7 (pause): " + op);
    }
}
