using System.Collections.Immutable;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;

namespace Clicalo.Domain.Execution;

/// <summary>
/// An input of the engine (blueprint §7.3). Each event declares its <see cref="Lane"/>: releasing, terminal events,
/// the end of a contact and real app switches travel in <see cref="EngineLane.Priority"/> (§3.2, rule 3).
/// </summary>
public abstract record EngineEvent
{
    private EngineEvent() { }

    /// <summary>The mailbox lane of this event.</summary>
    public abstract EngineLane Lane { get; }

    /// <summary>A tile was activated (contact start, contact end or invocation).</summary>
    /// <param name="Request">The activation.</param>
    /// <param name="Shortcut">The shortcut.</param>
    /// <param name="OriginProfile">The profile it was activated from.</param>
    /// <param name="Injection">The mode of that profile (D24).</param>
    /// <param name="LastExternalPointer">Last pointer position outside Clícalo (EJE-009).</param>
    /// <param name="EditMode">Whether the panel is in edit mode.</param>
    /// <param name="Epoch">Foreground epoch when the tile was touched (INV-6).</param>
    /// <param name="RequiredForeground">The window that must still be in front, if any.</param>
    public sealed record Activation(
        ActivationRequest Request,
        Shortcut Shortcut,
        ProfileId? OriginProfile,
        InjectionMode Injection,
        PhysicalPoint? LastExternalPointer,
        bool EditMode,
        long Epoch,
        ForegroundWindowId? RequiredForeground
    ) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>A contact ended or was cancelled: a Hold under it is released (EJE-004, INV-9).</summary>
    /// <param name="ContactId">The contact.</param>
    /// <param name="Summary">Duration, displacement and palm.</param>
    /// <param name="Cancelled">Whether it was cancelled (left the extra area, long press, swipe).</param>
    public sealed record ContactEnded(int ContactId, ContactSummary Summary, bool Cancelled)
        : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>A timer of the engine fired.</summary>
    /// <param name="Key">Which timer.</param>
    public sealed record TimerFired(TimerKey Key) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>The verified external foreground changed (blueprint §7.9).</summary>
    /// <param name="Info">The new foreground.</param>
    /// <param name="IsUserSwitch">Whether it is a real app switch (not Clícalo, the shell, the touch keyboard, Voice Access, nor «Try now»).</param>
    public sealed record ForegroundChanged(ForegroundInfo Info, bool IsUserSwitch) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>The foreground thread switched layout (<c>WM_INPUTLANGCHANGE</c>).</summary>
    /// <param name="Layout">The new layout.</param>
    public sealed record LayoutChanged(KeyboardLayoutSnapshot Layout) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>A terminal event: release everything, cancel the macro, empty the state (SEG-007, INV-3).</summary>
    /// <param name="Reason">Why.</param>
    public sealed record Terminal(TerminalReason Reason) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>Release everything and keep running (SEG-003, SEG-005).</summary>
    /// <param name="Reason">Why.</param>
    public sealed record ReleaseAll(ReleaseReason Reason) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>A batch failed half-way (INV-5: the transaction is closed releasing what it pressed).</summary>
    /// <param name="Effect">The failed effect.</param>
    /// <param name="Win32Error">The error of <c>SendInput</c>.</param>
    public sealed record InjectFailed(EffectId Effect, int Win32Error) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>The clipboard holds the text to paste; send Ctrl+V (EJE-008).</summary>
    /// <param name="Effect">The paste effect.</param>
    public sealed record ClipboardReady(EffectId Effect) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>The settings the engine obeys changed.</summary>
    /// <param name="Config">The new settings.</param>
    public sealed record ConfigChanged(EngineConfig Config) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>Test mode switched on or off (TAC-008).</summary>
    /// <param name="On">Whether it is on.</param>
    public sealed record SetTestMode(bool On) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>Sending paused or resumed; pausing releases everything.</summary>
    /// <param name="On">Whether it is paused.</param>
    public sealed record SetPaused(bool On) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>A launch finished on the Shell thread.</summary>
    /// <param name="Effect">The effect.</param>
    public sealed record LaunchCompleted(EffectId Effect) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>A launch failed on the Shell thread.</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Failure">Why.</param>
    public sealed record LaunchFailed(EffectId Effect, Failure Failure) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>
    /// The secure desktop refused a release (locked session, UAC, Ctrl+Alt+Del: <c>InjectionStatus.Blocked</c>), or
    /// <c>SendInput</c> took it only in part: the engine keeps it and sends it again on <see cref="SessionResumed"/> and
    /// with the next «release everything» (INV-3). The physical ledger marks it pending meanwhile.
    /// </summary>
    /// <param name="Events">The release events that did not go.</param>
    public sealed record ReleasesBlocked(ImmutableArray<InjectedEvent> Events) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>
    /// The input desktop is Clícalo's again: the session was unlocked, the computer resumed, or the secure desktop of
    /// UAC or Ctrl+Alt+Del closed. Blocked releases go again, the logical ones and the ones the physical ledger keeps
    /// pending (§7.6, INV-3).
    /// </summary>
    public sealed record SessionResumed : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>A tap on a key of the sticky modifiers row: 0 → 1 → 2 → 0 (FIJ-005).</summary>
    /// <param name="Modifier">The modifier.</param>
    public sealed record StickyTapped(ModifierKind Modifier) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }

    /// <summary>The sticky modifiers row was switched off: every sticky modifier is released (FIJ-005).</summary>
    public sealed record ClearSticky : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Priority;
    }

    /// <summary>A system command finished on the Shell thread.</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Succeeded">Whether it worked.</param>
    public sealed record SystemCommandCompleted(EffectId Effect, bool Succeeded) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }
}
