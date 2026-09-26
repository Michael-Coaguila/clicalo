using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.StickyModifiers;

namespace Clicalo.Domain.Tests.Execution.Support;

/// <summary>
/// The key safety invariants of blueprint §7.5 that the pure engine must keep after every step (INV-1, INV-3, INV-4,
/// INV-6 to INV-9, INV-12, and FIJ-006 for the sticky keys); INV-2, INV-5 and INV-11 are checked with the real gate in
/// Platform.IntegrationTests. Checked after every step of 10 000 scenarios, so the checks build their message only when
/// they fail.
/// </summary>
internal static class EngineInvariants
{
    private static readonly TimerKey OutboxTimer = new("outbox");

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
        if (receiver.Anomalies.Count != 0)
        {
            Assert.Fail(
                "INV-1/INV-12 anomalies " + string.Join("; ", receiver.Anomalies) + " after " + op
            );
        }

        Require(
            receiver.Keys.Count == ledger.Holders.Count
                && ledger.Holders.Keys.All(receiver.Keys.Contains),
            "INV-1 keys",
            op
        );
        Require(receiver.Buttons == ledger.HeldButtons, "INV-1 buttons", op);
        foreach (var (key, holders) in ledger.Holders)
        {
            Require(!holders.IsEmpty, "INV-1 empty holder set", op);
            foreach (var holder in holders)
            {
                Require(
                    ledger.Items[holder].Keys.Items.Contains(key),
                    "INV-1 holder without its key",
                    op
                );
            }
        }

        var maxHold = engine.Config.MaxHold;
        foreach (var item in ledger.Items.Values)
        {
            foreach (var key in item.Keys)
            {
                Require(
                    ledger.Holders[key].Contains(item.Holder),
                    "INV-1 key without its holder",
                    op
                );
            }

            // INV-4 (bounded life): every holder has a deadline no later than its press plus the limit it follows.
            if (item.InheritsGlobalLimit)
            {
                Require(
                    item.DeadlineTicks == (maxHold is { } max ? item.SinceTicks + max.Ticks : null),
                    "INV-4 deadline",
                    op
                );
            }
            else if (item.DeadlineTicks is null)
            {
                Require(
                    string.Equals(item.Shortcut?.Value, "holdnever", StringComparison.Ordinal),
                    "INV-4 (only «Never» has no deadline)",
                    op
                );
            }

            if (op.Kind == EngineOpKind.Wait && item.DeadlineTicks is { } deadline)
            {
                Require(deadline > engine.Now, "INV-4 (expired and still held)", op);
            }
        }

        var epoch = state.Foreground?.Epoch;
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case EngineEffect.Inject { IsRelease: true } release:
                    // INV-8: releases carry no epoch or target: nothing may filter them.
                    Require(
                        release.Epoch is null && release.RequiredForeground is null,
                        "INV-8 filtered release",
                        op
                    );
                    Require(
                        release.Events.All(static e => e.IsRelease),
                        "INV-8 release that presses",
                        op
                    );
                    break;
                case EngineEffect.Inject press:
                    // INV-6: a press carries the current epoch and never reaches an elevated target.
                    Require(press.Epoch == epoch, "INV-6 epoch", op);
                    Require(
                        state.Foreground!.Elevation != ElevationState.TargetElevated,
                        "INV-6 elevated",
                        op
                    );
                    RequireNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.TypeText typed:
                    Require(typed.Epoch == epoch, "INV-6 text epoch", op);
                    RequireNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.MouseAction mouse:
                    Require(mouse.Epoch == epoch, "INV-6 mouse epoch", op);
                    RequireNothingSentWhileSilenced(state, op);
                    break;
                case EngineEffect.ClipboardPaste
                or EngineEffect.Launch
                or EngineEffect.SystemCommand:
                    RequireNothingSentWhileSilenced(state, op);
                    break;
            }
        }

        // INV-9: a contact's holder goes away only by its own end, a deadline, a terminal event or «Release all».
        foreach (var gone in before.Keys.Items.Values)
        {
            if (gone.ContactId is null || ledger.Items.ContainsKey(gone.Holder))
            {
                continue;
            }

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
            Require(allowed, "INV-9 " + gone.Holder + " released", op);
        }

        // INV-3: after a terminal event or «Release all», nothing is held, queued, repeating or running.
        if (op.Kind is EngineOpKind.Terminal or EngineOpKind.ReleaseAll)
        {
            Require(state.IsQuiet && receiver.IsEmpty, "INV-3", op);
        }

        // FIJ-006: an active sticky modifier is exactly a sticky holder of the ledger (SEG-001).
        foreach (var modifier in StickyState.Order)
        {
            Require(
                ledger.Items.ContainsKey(HolderId.ForSticky(modifier))
                    == (state.Sticky.LevelOf(modifier) != StickyLevel.Off),
                "FIJ-006 sticky " + modifier,
                op
            );
        }

        // Liveness: queued steps always have their timer, so nothing waits for ever.
        Require(
            state.Outbox.IsEmpty || engine.Timers.ContainsKey(OutboxTimer),
            "outbox without timer",
            op
        );
    }

    private static void Require(bool holds, string invariant, EngineOp op)
    {
        if (!holds)
        {
            Assert.Fail(invariant + " violated by " + op);
        }
    }

    // INV-7: with test mode or pause, nothing but releases goes out.
    private static void RequireNothingSentWhileSilenced(EngineState state, EngineOp op) =>
        Require(!state.TestMode && !state.Paused, "INV-7", op);
}
