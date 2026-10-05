using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Application.Coordinators;

/// <summary>
/// The order in which Clícalo ends (blueprint §3.1, §6.4, §7.6; SEG-006, SEG-007, REG-08), behind
/// <c>IAppLifetime.ExitAsync</c>:
/// <list type="number">
/// <item>the engine gets <see cref="EngineEvent.Terminal"/> in its priority lane: it releases everything, cancels the
/// macro and stops (INV-3); the sequence waits at most <c>Timings.App.ExitReleaseWait</c> for it;</item>
/// <item>the document and the usage are flushed, at most <c>Timings.App.ExitFlushTimeout</c>.</item>
/// </list>
/// Nothing here can keep the process alive: a step that does not finish in time is reported and skipped. What the
/// engine could not release stays down until Sentinel, seeing the process exit with code 0, releases it and does not
/// relaunch (ADR-0022).
/// </summary>
/// <remarks>
/// Continuations never return to the caller's thread, so the synchronous session-end path (App/Shutdown) may block the
/// UI thread on <see cref="RunAsync"/> without a deadlock.
/// </remarks>
public sealed partial class ExitSequence
{
    private readonly IEngineInbox _engine;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    /// <summary>Creates the sequence.</summary>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="time">Clock of the time limits.</param>
    /// <param name="logger">Logs the outcome (codes only).</param>
    public ExitSequence(
        IEngineInbox engine,
        TimeProvider time,
        ILogger<ExitSequence>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(time);
        _engine = engine;
        _time = time;
        _logger = logger ?? NullLogger<ExitSequence>.Instance;
    }

    /// <summary>Runs the sequence.</summary>
    /// <param name="reason"><see cref="TerminalReason.Exit"/> or <see cref="TerminalReason.SessionEnd"/>.</param>
    /// <param name="engineReleased">
    /// Completes when the engine has released everything after the terminal event: the end of its loop after
    /// <see cref="TerminalReason.Exit"/>, or its first snapshot with nothing held after
    /// <see cref="TerminalReason.SessionEnd"/>, whose loop goes on in case another app cancels the end of the session.
    /// </param>
    /// <param name="flush">Writes everything pending (<c>PersistenceScheduler.FlushAsync</c>).</param>
    /// <param name="cancellationToken">Abandons the waits (the process is ending anyway).</param>
    public async Task<ExitReport> RunAsync(
        TerminalReason reason,
        Func<CancellationToken, Task> engineReleased,
        Func<CancellationToken, Task> flush,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(engineReleased);
        ArgumentNullException.ThrowIfNull(flush);
        if (reason is not (TerminalReason.Exit or TerminalReason.SessionEnd))
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                reason,
                "Only an exit or the end of the session ends the process."
            );
        }

        var started = _time.GetTimestamp();
        var posted = _engine.Post(new EngineEvent.Terminal(reason));
        var released =
            await WithinAsync(engineReleased, Timings.App.ExitReleaseWait, cancellationToken)
                .ConfigureAwait(false) && posted;
        var flushed = await WithinAsync(flush, Timings.App.ExitFlushTimeout, cancellationToken)
            .ConfigureAwait(false);
        var report = new ExitReport(released, flushed, _time.GetElapsedTime(started));
        LogExit(_logger, reason, report.EngineReleased, report.Flushed, report.Elapsed);
        return report;
    }

    private async Task<bool> WithinAsync(
        Func<CancellationToken, Task> step,
        TimeSpan limit,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await step(cancellationToken)
                .WaitAsync(limit, _time, cancellationToken)
                .ConfigureAwait(false);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Ending the process matters more than the failed step: Sentinel covers the keys and the last saved
            // document is still valid (REG-08). The failure is logged by code only.
            LogStepFailed(_logger, ex.GetType().Name);
            return false;
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "app.exit {Reason}: engine released {Released}, flushed {Flushed}, in {Elapsed}"
    )]
    private static partial void LogExit(
        ILogger logger,
        TerminalReason reason,
        bool released,
        bool flushed,
        TimeSpan elapsed
    );

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "app.exit step failed with {Exception}"
    )]
    private static partial void LogStepFailed(ILogger logger, string exception);
}
