using System.Collections.Immutable;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Geometry;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;

namespace Clicalo.Domain.Execution;

/// <summary>
/// An output of the engine that the host interprets (blueprint §7.3). Every effect with external consequences runs
/// through the injection gate with the host's generation (INV-11); releases ignore epoch, target, elevation, test
/// mode and pause (INV-8), and nothing else is sent in test mode or pause (INV-7).
/// </summary>
public abstract record EngineEffect
{
    private EngineEffect() { }

    /// <summary>Send key and button events (<c>SendInput</c> in the mode each <see cref="Keys.InjectedKey"/> carries).</summary>
    /// <param name="Events">The events, in order.</param>
    /// <param name="Epoch">Foreground epoch it was planned for; <see langword="null"/> only for releases and internal keys.</param>
    /// <param name="RequiredForeground">The window that must be in front, if any (INV-6).</param>
    /// <param name="IsRelease">Whether it only releases (never filtered, INV-8).</param>
    /// <param name="IsInternal">Whether it is the internal rights key of the foreground ladder (§3.6).</param>
    public sealed record Inject(
        ImmutableArray<InjectedEvent> Events,
        long? Epoch,
        ForegroundWindowId? RequiredForeground,
        bool IsRelease,
        bool IsInternal
    ) : EngineEffect
    {
        /// <summary>
        /// Identity of a batch that presses something: when it fails, <see cref="EngineEvent.InjectFailed"/> names it
        /// and the engine releases what its holder pressed (INV-5). Default for releases.
        /// </summary>
        public EffectId Effect { get; init; }

        /// <summary>The holder the batch presses or releases for, if a single one.</summary>
        public HolderId? Holder { get; init; }
    }

    /// <summary>Type a text as Unicode (EJE-008).</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Text">The text; the host copies it to a rented buffer and wipes it after sending.</param>
    /// <param name="Epoch">Foreground epoch.</param>
    /// <param name="RequiredForeground">The window that must be in front, if any.</param>
    public sealed record TypeText(
        EffectId Effect,
        SecretText Text,
        long Epoch,
        ForegroundWindowId? RequiredForeground
    ) : EngineEffect;

    /// <summary>Put the text on the clipboard; <see cref="EngineEvent.ClipboardReady"/> sends Ctrl+V (EJE-008).</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Text">The text.</param>
    /// <param name="Epoch">Foreground epoch.</param>
    public sealed record ClipboardPaste(EffectId Effect, SecretText Text, long Epoch)
        : EngineEffect;

    /// <summary>A mouse action at a point (EJE-009, blueprint §7.11): move there, act, stay there.</summary>
    /// <param name="Op">The action (drag press or release is an <see cref="Inject"/> of a button).</param>
    /// <param name="Speed">Scroll speed.</param>
    /// <param name="Target">Where; <see langword="null"/> for the centre of the foreground window's client area.</param>
    /// <param name="Epoch">Foreground epoch.</param>
    public sealed record MouseAction(
        MouseOp Op,
        ScrollSpeed Speed,
        PhysicalPoint? Target,
        long Epoch
    ) : EngineEffect;

    /// <summary>Start something on the Shell thread; the result comes back as an event.</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Request">What to start.</param>
    public sealed record Launch(EffectId Effect, LaunchRequest Request) : EngineEffect;

    /// <summary>Run a system command on the Shell thread; the result comes back as an event.</summary>
    /// <param name="Effect">The effect.</param>
    /// <param name="Command">The command.</param>
    public sealed record SystemCommand(EffectId Effect, SystemCommandId Command) : EngineEffect;

    /// <summary>Start or move a timer (a macro wait is a timer, never <c>Thread.Sleep</c>).</summary>
    /// <param name="Key">The timer.</param>
    /// <param name="DueTicks">When it fires.</param>
    public sealed record Schedule(TimerKey Key, long DueTicks) : EngineEffect;

    /// <summary>Stop a timer.</summary>
    /// <param name="Key">The timer.</param>
    public sealed record CancelTimer(TimerKey Key) : EngineEffect;

    /// <summary>A notice for the panel and screen readers.</summary>
    /// <param name="Text">The text.</param>
    /// <param name="Urgency">How it is announced.</param>
    public sealed record Notice(Message Text, NoticeUrgency Urgency) : EngineEffect;

    /// <summary>
    /// Count an effective execution for Frequents (FRE-002); the host hands it to the observer, which dispatches the
    /// <c>RecordUsage</c> document command.
    /// </summary>
    /// <param name="Shortcut">The shortcut.</param>
    /// <param name="At">When.</param>
    public sealed record CountUsage(ShortcutId Shortcut, DateTimeOffset At) : EngineEffect;

    /// <summary>Remember the last action for Repeat (AVI-004).</summary>
    /// <param name="Shortcut">The shortcut.</param>
    public sealed record SetLastAction(ShortcutId Shortcut) : EngineEffect;
}
