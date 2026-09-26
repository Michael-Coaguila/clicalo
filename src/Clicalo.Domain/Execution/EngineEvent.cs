using Clicalo.Domain.Errors;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.Keys;
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

    /// <summary>A system command finished on the Shell thread.</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Succeeded">Whether it worked.</param>
    public sealed record SystemCommandCompleted(EffectId Effect, bool Succeeded) : EngineEvent
    {
        /// <inheritdoc />
        public override EngineLane Lane => EngineLane.Normal;
    }
}
