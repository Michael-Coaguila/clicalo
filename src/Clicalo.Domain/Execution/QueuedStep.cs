using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;

namespace Clicalo.Domain.Execution;

/// <summary>
/// One pending key operation of the engine's outbox (blueprint §7.7): presses and planned releases leave one per
/// <c>Timings.Injection.InterEventDelay</c> (all together when it is zero), so a Tap is «press in order, release in
/// reverse order» with a pause between events. Each step changes the logical ledger only when it runs, so what the
/// ledger holds is always what was sent (INV-1). Safety releases never queue: they go out at once, as one batch.
/// </summary>
public abstract record QueuedStep
{
    private QueuedStep() { }

    /// <summary>The holder the step belongs to; cancelling a holder drops all its steps.</summary>
    public abstract HolderId Holder { get; }

    /// <summary>Press a key for a holder (INV-6: only if the foreground epoch still matches).</summary>
    /// <param name="Template">The holder's item, created by the first press.</param>
    /// <param name="Key">The key.</param>
    /// <param name="Origin">The execution it belongs to.</param>
    public sealed record Press(PressedItem Template, InjectedKey Key, ExecutionOrigin Origin)
        : QueuedStep
    {
        /// <inheritdoc />
        public override HolderId Holder => Template.Holder;
    }

    /// <summary>The planned release of a key a holder pressed (no menu mask, so a Win tap opens Start).</summary>
    /// <param name="Owner">The holder.</param>
    /// <param name="Key">The key.</param>
    public sealed record Lift(HolderId Owner, InjectedKey Key) : QueuedStep
    {
        /// <inheritdoc />
        public override HolderId Holder => Owner;
    }

    /// <summary>The end of an execution's steps: notices, usage and, for a macro, its next step.</summary>
    /// <param name="Owner">The holder whose steps end here.</param>
    /// <param name="Completion">What happens.</param>
    public sealed record Finish(HolderId Owner, StepCompletion Completion) : QueuedStep
    {
        /// <inheritdoc />
        public override HolderId Holder => Owner;
    }
}
