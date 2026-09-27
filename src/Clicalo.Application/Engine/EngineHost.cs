using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;

namespace Clicalo.Application.Engine;

/// <summary>
/// The engine actor (blueprint §7.3, ADR-0004): owns <see cref="EngineState"/> on the engine thread, runs
/// <see cref="EngineReducer"/> for each event of its <see cref="EngineMailbox"/> and interprets the effects against the
/// ports, every external one through the injection gate with its <see cref="Generation"/>. Each message runs in a
/// try/catch: an exception releases from the ledger under the gate, resets to <see cref="EngineState.Empty"/> and warns
/// «Something failed; the keys were released» (NFR-005). Writes the ledger heartbeat on every turn and every
/// <c>Timings.Engine.LedgerHeartbeatInterval</c>; timers run on <see cref="TimeProvider"/>.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>One <see cref="TimeProvider"/> timer wakes the loop for the earliest of the engine's timers, the next
/// heartbeat and a coalesced snapshot, so the loop's only wait is the mailbox's.</item>
/// <item>Before a press goes out, the host checks it again against the foreground it knows (INV-6), test mode and
/// pause (INV-7); a press it drops, or one <c>SendInput</c> takes only in part, comes back as
/// <see cref="EngineEvent.InjectFailed"/> in the priority lane, so the reducer releases what it may have left down
/// (INV-5). A release the secure desktop refuses comes back as <see cref="EngineEvent.ReleasesBlocked"/>.</item>
/// <item>A <see cref="InjectionStatus.Fenced"/> result means another engine replaced this one after an emergency
/// (INV-11): this host stops at once and sends nothing more.</item>
/// <item>Text is revealed only to be sent, into a rented buffer wiped afterwards (§6.7).</item>
/// <item>Snapshots reach the observer at most once per <c>Timings.Engine.SnapshotCoalescing</c> (§3.2, rule 5).</item>
/// </list>
/// </remarks>
public sealed partial class EngineHost : IEngineInbox, IDisposable
{
    private readonly EngineHostPorts _ports;
    private readonly TimeProvider _time;
    private readonly ILogger<EngineHost> _logger;
    private readonly Func<EngineState, EngineEvent, EngineConfig, long, EngineTransition> _reducer;
    private readonly EngineMailbox _mailbox = new();
    private readonly Dictionary<TimerKey, long> _timers = [];
    private readonly ITimer _wake;
    private readonly long _heartbeatTicks;
    private readonly long _coalescingTicks;
    private EngineConfig _config;
    private EngineState _state;
    private EngineSnapshot _snapshot = EngineSnapshot.Empty;
    private long _publishedVersion = -1;
    private long? _lastPublishTicks;
    private long? _lastHeartbeatTicks;
    private bool _stopped;
    private bool _fenced;
    private int _disposed;

    /// <summary>Creates a host; <see cref="Run"/> starts it on the engine thread.</summary>
    /// <param name="ports">The ports.</param>
    /// <param name="generation">The ledger's current generation; a host never changes it.</param>
    /// <param name="config">The initial settings.</param>
    /// <param name="time">Clock and timers.</param>
    /// <param name="logger">Logs codes, never keys or text (LOG-001).</param>
    public EngineHost(
        EngineHostPorts ports,
        EngineGeneration generation,
        EngineConfig config,
        TimeProvider time,
        ILogger<EngineHost> logger
    )
        : this(ports, generation, config, time, logger, EngineReducer.Reduce, EngineState.Empty) { }

    /// <summary>Creates a host with another reducer or a starting state (tests).</summary>
    internal EngineHost(
        EngineHostPorts ports,
        EngineGeneration generation,
        EngineConfig config,
        TimeProvider time,
        ILogger<EngineHost> logger,
        Func<EngineState, EngineEvent, EngineConfig, long, EngineTransition> reducer,
        EngineState initial
    )
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(reducer);
        ArgumentNullException.ThrowIfNull(initial);
        _ports = ports;
        Generation = generation;
        _time = time;
        _logger = logger;
        _reducer = reducer;
        _state = initial;
        _config = config with { TimestampFrequency = time.TimestampFrequency };
        _heartbeatTicks = ToTicks(Timings.Engine.LedgerHeartbeatInterval);
        _coalescingTicks = ToTicks(Timings.Engine.SnapshotCoalescing);
        _wake = time.CreateTimer(
            static host => ((EngineHost)host!)._mailbox.Wake(),
            this,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>The generation every effect of this host carries.</summary>
    public EngineGeneration Generation { get; }

    /// <summary>The latest published snapshot; safe to read from any thread.</summary>
    public EngineSnapshot Snapshot => Volatile.Read(ref _snapshot);

    /// <summary>Whether this host stopped (exit, cancellation, or fenced by a newer engine).</summary>
    public bool IsStopped => _stopped || _fenced;

    /// <summary>The state, for the tests of the engine thread.</summary>
    internal EngineState State => _state;

    /// <summary>The timers the engine asked for, by due tick (tests).</summary>
    internal IReadOnlyDictionary<TimerKey, long> Timers => _timers;

    /// <inheritdoc />
    public bool Post(EngineEvent engineEvent) => _mailbox.Post(engineEvent);

    /// <summary>
    /// The engine loop: runs on the dedicated engine thread (AboveNormal) until <paramref name="cancellationToken"/>
    /// is cancelled or a <see cref="EngineEvent.Terminal"/> with <see cref="TerminalReason.Exit"/> is processed.
    /// Never does disk or network I/O, never calls the UI and never waits on anything but its mailbox.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop after releasing everything.</param>
    public void Run(CancellationToken cancellationToken)
    {
        _ports.Ledger.SetMarks(KeyLedgerMarks.EngineAlive);
        try
        {
            while (!IsStopped && !cancellationToken.IsCancellationRequested)
            {
                Pump();
                if (IsStopped)
                {
                    break;
                }

                _mailbox.WaitForEvent(Timeout.InfiniteTimeSpan, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancelled: release below.
        }
        finally
        {
            if (!IsStopped)
            {
                Handle(new EngineEvent.Terminal(TerminalReason.Exit));
            }

            // A fenced host is a zombie: the ledger's EngineAlive mark belongs to the engine that replaced it, and
            // clearing it would switch off the emergency releaser's watch over that engine (§3.2, rule 6).
            if (!_fenced)
            {
                _ports.Ledger.ClearMarks(KeyLedgerMarks.EngineAlive);
            }

            _mailbox.Complete();
        }
    }

    /// <summary>
    /// Starts <see cref="Run"/> on its own background thread, «Clicalo.Engine», with
    /// <see cref="ThreadPriority.AboveNormal"/> (blueprint §3.2). The thread ends when the loop ends.
    /// </summary>
    /// <param name="cancellationToken">Stops the loop after releasing everything.</param>
    public Thread StartOnDedicatedThread(CancellationToken cancellationToken)
    {
        var thread = new Thread(() => Run(cancellationToken))
        {
            Name = "Clicalo.Engine",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
        return thread;
    }

    /// <summary>
    /// One turn of the loop: the heartbeat, the timers that fell due, every queued event (priority lane first) and a
    /// coalesced snapshot; then arms the wake-up timer. The tests call it directly on their thread.
    /// </summary>
    internal void Pump()
    {
        var now = _time.GetTimestamp();
        Heartbeat(now);
        foreach (
            var (key, _) in _timers.Where(t => t.Value <= now).OrderBy(static t => t.Value).ToList()
        )
        {
            if (IsStopped)
            {
                return;
            }

            _timers.Remove(key);
            Handle(new EngineEvent.TimerFired(key));
        }

        while (!IsStopped && _mailbox.TryTake(out var engineEvent))
        {
            Handle(engineEvent);
            Heartbeat(_time.GetTimestamp());
        }

        now = _time.GetTimestamp();
        Publish(now);
        ArmWake(now);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _wake.Dispose();
            _mailbox.Dispose();
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "NFR-005: whatever fails inside one message, the engine releases everything and goes on."
    )]
    private void Handle(EngineEvent engineEvent)
    {
        if (engineEvent is EngineEvent.ConfigChanged changed)
        {
            _config = changed.Config with { TimestampFrequency = _time.TimestampFrequency };
        }

        var before = _state;
        try
        {
            var transition = _reducer(_state, engineEvent, _config, _time.GetTimestamp());
            _state = transition.Next;
            foreach (var effect in transition.Effects)
            {
                if (IsStopped)
                {
                    return;
                }

                Interpret(effect);
            }

            if (engineEvent is EngineEvent.Terminal terminal)
            {
                AfterTerminal(terminal.Reason);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fault(before, ex);
        }
    }

    private void AfterTerminal(TerminalReason reason)
    {
        switch (reason)
        {
            case TerminalReason.Exit:
            case TerminalReason.SessionEnd:
                _ports.Ledger.SetMarks(KeyLedgerMarks.CleanShutdown);
                break;
            case TerminalReason.Relaunch:
            case TerminalReason.Update:
                _ports.Ledger.SetMarks(KeyLedgerMarks.CleanShutdown | KeyLedgerMarks.NoRelaunch);
                break;
        }

        if (reason == TerminalReason.Exit)
        {
            _stopped = true;
        }
    }

    private void Interpret(EngineEffect effect)
    {
        switch (effect)
        {
            case EngineEffect.Inject inject:
                Inject(inject);
                break;
            case EngineEffect.TypeText typed:
                if (PressAllowed(typed.Epoch, typed.RequiredForeground))
                {
                    Settle(
                        Reveal(
                            typed.Text,
                            static (host, text) =>
                                host._ports.Injector.TypeText(host.Generation, text)
                        )
                    );
                }

                break;
            case EngineEffect.ClipboardPaste paste:
                if (PressAllowed(paste.Epoch, requiredForeground: null))
                {
                    Reveal(
                        paste.Text,
                        (host, text) =>
                        {
                            host._ports.Clipboard.Prepare(
                                host.Generation,
                                paste.Effect,
                                text,
                                host
                            );
                            return default;
                        }
                    );
                }

                break;
            case EngineEffect.MouseAction mouse:
                if (PressAllowed(mouse.Epoch, requiredForeground: null))
                {
                    Settle(_ports.Injector.Mouse(Generation, mouse.Op, mouse.Target));
                }

                break;
            case EngineEffect.Launch launch:
                _ports.Shell.Launch(Generation, launch.Effect, launch.Request, this);
                break;
            case EngineEffect.SystemCommand command:
                _ports.Shell.Run(Generation, command.Effect, command.Command, this);
                break;
            case EngineEffect.Schedule schedule:
                _timers[schedule.Key] = schedule.DueTicks;
                break;
            case EngineEffect.CancelTimer cancel:
                _timers.Remove(cancel.Key);
                break;
            case EngineEffect.Notice notice:
                _ports.Observer.OnNotice(notice.Text, notice.Urgency);
                break;
            case EngineEffect.CountUsage usage:
                _ports.Observer.OnUsage(usage.Shortcut, usage.At);
                break;
            case EngineEffect.SetLastAction last:
                _ports.Observer.OnLastAction(last.Shortcut);
                break;
        }
    }

    private void Inject(EngineEffect.Inject inject)
    {
        if (
            !inject.IsRelease
            && !inject.IsInternal
            && !PressAllowed(inject.Epoch, inject.RequiredForeground)
        )
        {
            // INV-6, INV-7: the reducer should never ask for this; if it does, the press does not go and the reducer
            // releases what the holder may already hold.
            LogPressDropped(_logger);
            _mailbox.Post(new EngineEvent.InjectFailed(inject.Effect, 0));
            return;
        }

        var result = _ports.Injector.Send(Generation, inject.Events.AsSpan());
        switch (result.Status)
        {
            case InjectionStatus.Fenced:
                Fence();
                break;
            case InjectionStatus.Blocked when inject.IsRelease:
                _mailbox.Post(new EngineEvent.ReleasesBlocked(inject.Events));
                break;
            case InjectionStatus.Blocked:
            case InjectionStatus.Failed when !inject.IsRelease:
                LogSendFailed(_logger, result.Status, result.EventsSent, result.Win32Error);
                _mailbox.Post(new EngineEvent.InjectFailed(inject.Effect, result.Win32Error));
                break;
            case InjectionStatus.Failed:
                // A release SendInput took only in part (the desktop switched in the middle of the batch): the logical
                // ledger already forgot those keys, so keep the batch to send again on SessionResumed, as a refused
                // one (INV-3). The events that did go are sent twice, and an extra release is harmless.
                LogSendFailed(_logger, result.Status, result.EventsSent, result.Win32Error);
                _mailbox.Post(new EngineEvent.ReleasesBlocked(inject.Events));
                break;
        }
    }

    private bool PressAllowed(long? epoch, ForegroundWindowId? requiredForeground) =>
        !_state.TestMode
        && !_state.Paused
        && _state.Foreground is { } foreground
        && epoch == foreground.Epoch
        && (requiredForeground is null || requiredForeground == foreground.Window)
        && foreground.Elevation != ElevationState.TargetElevated;

    private void Settle(InjectionResult result)
    {
        if (result.Status == InjectionStatus.Fenced)
        {
            Fence();
        }
        else if (result.Status is InjectionStatus.Failed or InjectionStatus.Blocked)
        {
            LogSendFailed(_logger, result.Status, result.EventsSent, result.Win32Error);
        }
    }

    private void Fence()
    {
        // INV-11: an emergency raised the generation and a new engine owns the keyboard; this one is a zombie.
        _fenced = true;
        LogFenced(_logger, Generation.Value);
    }

    private InjectionResult Reveal(
        SecretText text,
        Func<EngineHost, ReadOnlySpan<char>, InjectionResult> send
    )
    {
        if (!text.IsAvailable || text.Length == 0)
        {
            return default;
        }

        var buffer = ArrayPool<char>.Shared.Rent(text.Length);
        try
        {
            text.WithRevealed(buffer, static (revealed, copy) => revealed.CopyTo(copy));
            return send(this, buffer.AsSpan(0, text.Length));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(buffer.AsSpan()));
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The emergency path of NFR-005 must never throw out of the engine loop."
    )]
    private void Fault(EngineState before, Exception exception)
    {
        LogFault(_logger, exception.GetType().Name);
        try
        {
            if (_ports.ReleaseRecorded is { } releaseRecorded)
            {
                releaseRecorded(Generation);
            }
            else
            {
                var release = before.Keys.ReleaseAll().Events.AddRange(before.BlockedReleases);
                if (!release.IsEmpty)
                {
                    _ports.Injector.Send(Generation, release.AsSpan());
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFault(_logger, ex.GetType().Name);
        }

        _state = EngineState.Empty with
        {
            Foreground = before.Foreground,
            Sequence = before.Sequence,
            Version = before.Version + 1,
        };
        _timers.Clear();
        _ports.Observer.OnNotice(L.EngineFault, NoticeUrgency.Assertive);
    }

    private void Heartbeat(long now)
    {
        if (_lastHeartbeatTicks is null || now != _lastHeartbeatTicks)
        {
            _ports.Ledger.WriteHeartbeat(now);
            _lastHeartbeatTicks = now;
        }
    }

    private void Publish(long now)
    {
        if (_state.Version == _publishedVersion)
        {
            return;
        }

        if (_lastPublishTicks is { } last && now - last < _coalescingTicks)
        {
            return;
        }

        var snapshot = new EngineSnapshot(
            [
                .. _state
                    .Keys.Items.Values.Where(static i => i.Origin != HoldOrigin.Tap)
                    .OrderBy(static i => i.SinceTicks)
                    .ThenBy(static i => i.Holder.Value, StringComparer.Ordinal),
            ],
            _state.Macro,
            _state.Armed,
            _state.TestMode,
            _state.Paused,
            _state.Version
        )
        {
            Sticky = _state.Sticky,
        };
        Volatile.Write(ref _snapshot, snapshot);
        _publishedVersion = _state.Version;
        _lastPublishTicks = now;
        _ports.Observer.OnSnapshot(snapshot);
    }

    private void ArmWake(long now)
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        var next = (_lastHeartbeatTicks ?? now) + _heartbeatTicks;
        foreach (var due in _timers.Values)
        {
            next = Math.Min(next, due);
        }

        if (_state.Version != _publishedVersion && _lastPublishTicks is { } last)
        {
            next = Math.Min(next, last + _coalescingTicks);
        }

        var delay = next <= now ? TimeSpan.Zero : FromTicks(next - now);
        _wake.Change(delay, Timeout.InfiniteTimeSpan);
    }

    private long ToTicks(TimeSpan duration) =>
        (long)((Int128)duration.Ticks * _time.TimestampFrequency / TimeSpan.TicksPerSecond);

    private TimeSpan FromTicks(long ticks) =>
        TimeSpan.FromTicks(
            (long)((Int128)ticks * TimeSpan.TicksPerSecond / _time.TimestampFrequency)
        );

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "engine.fault: {ExceptionType}; everything was released and the engine state reset"
    )]
    private static partial void LogFault(ILogger logger, string exceptionType);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "engine.zombie: generation {Generation} was fenced; this engine stops"
    )]
    private static partial void LogFenced(ILogger logger, ulong generation);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "engine.send_failed: {Status}, {Sent} events sent, Win32 error {Error}"
    )]
    private static partial void LogSendFailed(
        ILogger logger,
        InjectionStatus status,
        int sent,
        int error
    );

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Warning,
        Message = "engine.press_dropped: a press for another foreground, test mode or pause was not sent"
    )]
    private static partial void LogPressDropped(ILogger logger);
}
