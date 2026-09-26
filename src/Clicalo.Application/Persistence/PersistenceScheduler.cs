using System.Threading.Channels;
using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;

namespace Clicalo.Application.Persistence;

/// <summary>
/// Autosave (blueprint §6.4, REG-07, DAT-002): routes each document change by slice to a single consumer on the
/// Persistence thread. Significant slices save <c>clicalo.json</c> after <c>Timings.Persistence.DocumentSaveDebounce</c>
/// with at most <c>DocumentSaveMaxLatency</c> and schedule the automatic backup; usage saves <c>usage.json</c> after
/// <c>UsageSaveDebounce</c> with at most <c>UsageSaveMaxLatency</c> and never rewrites the document or schedules a
/// backup. Guarantee: 10 changes in 1 s write the document at most once, and 1000 executions never write it.
/// </summary>
/// <remarks>
/// <para>
/// The automatic backup is taken <c>Timings.Backups.AutoBackupDelay</c> after the last significant change (so at most
/// one per delay), and only while the «automatic backup» setting is on (COP-003, §6.8).
/// </para>
/// <para>
/// A failed save keeps the newest document pending. An I/O failure (the writer already retried its backoff) is retried
/// at once, unless it came back at once (a full disk: then it waits for <c>WriteRetryInterval</c> and never spins), and
/// stays <see cref="SaveStatus.Retrying"/>, silent, until <c>Timings.Persistence.UnsavedNoticeAfter</c>
/// after the first failed attempt started; then it is <see cref="SaveStatus.Failing"/>, visible, and retried every
/// <c>WriteRetryInterval</c> (S11: an antivirus or indexer lock never shows an error; a lock longer than 3 s always
/// does). Any other failure (read-only, invalid) is visible at once and retried with the next change.
/// </para>
/// </remarks>
public sealed partial class PersistenceScheduler : IDisposable
{
    private readonly IDocumentRepository _documents;
    private readonly IUsageRepository _usage;
    private readonly IBackupService _backups;
    private readonly TimeProvider _time;
    private readonly ILogger<PersistenceScheduler> _logger;
    private readonly Channel<Signal> _signals = Channel.CreateUnbounded<Signal>(
        new UnboundedChannelOptions { SingleReader = true }
    );
    private readonly SemaphoreSlim _io = new(1, 1);
    private readonly Lock _gate = new();
    private readonly ITimer _documentTimer;
    private readonly ITimer _usageTimer;
    private readonly ITimer _backupTimer;
    private readonly ITimer _noticeTimer;

    private UserDocument? _latest;
    private bool _documentDirty;
    private long _documentDirtySince;
    private bool _usageDirty;
    private long _usageDirtySince;
    private UserDocument? _backupCandidate;
    private long? _failingSince;
    private bool _retryCadence;
    private bool _visible;
    private int _status;
    private int _inFlight;

    /// <summary>Creates the scheduler.</summary>
    /// <param name="documents">Writes <c>clicalo.json</c>.</param>
    /// <param name="usage">Writes <c>usage.json</c>.</param>
    /// <param name="backups">Takes the automatic backups.</param>
    /// <param name="time">Debounce and latency timers.</param>
    /// <param name="logger">Logs codes, never content.</param>
    public PersistenceScheduler(
        IDocumentRepository documents,
        IUsageRepository usage,
        IBackupService backups,
        TimeProvider time,
        ILogger<PersistenceScheduler> logger
    )
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        _documents = documents;
        _usage = usage;
        _backups = backups;
        _time = time;
        _logger = logger;
        _documentTimer = CreateTimer(Signal.Document);
        _usageTimer = CreateTimer(Signal.Usage);
        _backupTimer = CreateTimer(Signal.Backup);
        _noticeTimer = _time.CreateTimer(
            _ => Notice(),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );
    }

    /// <summary>
    /// Raised when <see cref="Status"/> changes, on the Persistence thread (or its timer when a failure becomes visible
    /// while a retry is running).
    /// </summary>
    public event EventHandler? StatusChanged;

    /// <summary>Whether everything is saved.</summary>
    public SaveStatus Status => (SaveStatus)Volatile.Read(ref _status);

    /// <summary>Whether no signal is queued or being processed (tests drive a fake clock with it).</summary>
    internal bool IsIdle => Volatile.Read(ref _inFlight) == 0;

    /// <summary>Queues a change (subscribe it to <see cref="DocumentStore.Changed"/>); never blocks.</summary>
    /// <param name="sender">The store.</param>
    /// <param name="change">The change.</param>
    public void OnDocumentChanged(object? sender, DocumentChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        var significant = (change.Slices & DocumentSlices.Significant) != DocumentSlices.None;
        var usage =
            (change.Slices & DocumentSlices.FrequentsUsage) != DocumentSlices.None
            || change.After.Frequents.UsageEpoch != change.Before.Frequents.UsageEpoch;
        if (!significant && !usage)
        {
            return;
        }

        var now = _time.GetTimestamp();
        lock (_gate)
        {
            _latest = change.After;
            if (significant)
            {
                if (!_documentDirty)
                {
                    _documentDirty = true;
                    _documentDirtySince = now;
                }

                if (!_retryCadence)
                {
                    Arm(
                        _documentTimer,
                        DueIn(
                            now,
                            _documentDirtySince,
                            Timings.Persistence.DocumentSaveDebounce,
                            Timings.Persistence.DocumentSaveMaxLatency
                        )
                    );
                }

                _backupCandidate = change.After;
                Arm(_backupTimer, Timings.Backups.AutoBackupDelay);
            }

            if (usage)
            {
                if (!_usageDirty)
                {
                    _usageDirty = true;
                    _usageDirtySince = now;
                }

                Arm(
                    _usageTimer,
                    DueIn(
                        now,
                        _usageDirtySince,
                        Timings.Persistence.UsageSaveDebounce,
                        Timings.Persistence.UsageSaveMaxLatency
                    )
                );
            }
        }

        Raise(Signal.Evaluate);
    }

    /// <summary>The single consumer loop of the Persistence thread.</summary>
    /// <param name="cancellationToken">Stops the loop after a final flush.</param>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            await foreach (
                var signal in _signals.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false)
            )
            {
                try
                {
                    await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
                    try
                    {
                        await ProcessAsync(signal, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        _io.Release();
                    }
                }
                finally
                {
                    _ = Interlocked.Decrement(ref _inFlight);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stopping: the final flush below writes what is pending.
        }

        using var bounded = new CancellationTokenSource(Timings.App.HandoverFlushTimeout, _time);
        try
        {
            await FlushAsync(bounded.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            LogFlushTimedOut(_logger);
        }
    }

    /// <summary>
    /// Writes everything pending now, both files: on suspend, sign-out, exit, elevated relaunch and before installing
    /// an update (§6.4).
    /// </summary>
    /// <param name="cancellationToken">Bounded by the caller (for example <c>Timings.App.HandoverFlushTimeout</c>).</param>
    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            Disarm(_documentTimer);
            Disarm(_usageTimer);
        }

        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveDocumentAsync(retryAtOnce: false, cancellationToken).ConfigureAwait(false);
            await SaveUsageAsync(cancellationToken).ConfigureAwait(false);
            PublishStatus();
        }
        finally
        {
            _io.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _ = _signals.Writer.TryComplete();
        _documentTimer.Dispose();
        _usageTimer.Dispose();
        _backupTimer.Dispose();
        _noticeTimer.Dispose();
        _io.Dispose();
    }

    private TimeSpan DueIn(long now, long since, TimeSpan debounce, TimeSpan maxLatency)
    {
        var left = maxLatency - _time.GetElapsedTime(since, now);
        return left < debounce ? (left < TimeSpan.Zero ? TimeSpan.Zero : left) : debounce;
    }

    private static void Arm(ITimer timer, TimeSpan due) =>
        timer.Change(due, Timeout.InfiniteTimeSpan);

    private static void Disarm(ITimer timer) =>
        timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

    private static Failure Unexpected() =>
        new(
            "persist.fault",
            L.TBug,
            FailureSeverity.Critical,
            FailureRecovery.Retry,
            FailureAnnouncement.Assertive
        );

    private ITimer CreateTimer(Signal signal) =>
        _time.CreateTimer(
            _ => Raise(signal),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan
        );

    private void Raise(Signal signal)
    {
        _ = Interlocked.Increment(ref _inFlight);
        if (!_signals.Writer.TryWrite(signal))
        {
            _ = Interlocked.Decrement(ref _inFlight);
        }
    }

    private async Task ProcessAsync(Signal signal, CancellationToken cancellationToken)
    {
        switch (signal)
        {
            case Signal.Document:
                await SaveDocumentAsync(retryAtOnce: true, cancellationToken).ConfigureAwait(false);
                break;
            case Signal.Usage:
                await SaveUsageAsync(cancellationToken).ConfigureAwait(false);
                break;
            case Signal.Backup:
                await BackupAsync(cancellationToken).ConfigureAwait(false);
                break;
        }

        PublishStatus();
    }

    private async Task SaveDocumentAsync(bool retryAtOnce, CancellationToken cancellationToken)
    {
        UserDocument document;
        var started = _time.GetTimestamp();
        lock (_gate)
        {
            if (!_documentDirty || _latest is null)
            {
                return;
            }

            document = _latest;
            _documentDirty = false;
            Disarm(_documentTimer);
        }

        Result<SaveReceipt> result;
        try
        {
            result = await _documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFault(_logger, ex);
            result = Results.Fail<SaveReceipt>(Unexpected());
        }

        lock (_gate)
        {
            if (result.IsSuccess)
            {
                if (_failingSince is not null)
                {
                    LogSavedAgain(_logger, result.Value.Seq);
                }

                _failingSince = null;
                _retryCadence = false;
                _visible = false;
                Disarm(_noticeTimer);
                if (_documentDirty)
                {
                    Arm(_documentTimer, Timings.Persistence.DocumentSaveDebounce);
                }

                return;
            }

            if (!_documentDirty)
            {
                _documentDirty = true;
                _documentDirtySince = started;
            }

            _failingSince ??= started;
            var failing = _time.GetElapsedTime(_failingSince.Value);
            var failure = result.Failure;
            LogSaveFailed(_logger, failure.Code, failing);
            if (failure.Recovery != FailureRecovery.Retry)
            {
                _retryCadence = false;
                _visible = true;
                return;
            }

            _retryCadence = true;
            if (failing >= Timings.Persistence.UnsavedNoticeAfter)
            {
                _visible = true;
                Arm(_documentTimer, Timings.Persistence.WriteRetryInterval);
                return;
            }

            Arm(_noticeTimer, Timings.Persistence.UnsavedNoticeAfter - failing);

            // Retrying at once only makes sense after the writer spent its own backoff on a lock; a failure that came
            // back at once (a full disk, any other persistent error) would otherwise spin the Persistence thread and
            // flood the log until the notice. It waits for the regular cadence instead.
            var spentBackoff =
                _time.GetElapsedTime(started) >= Timings.Persistence.WriteRetryBackoff[0];
            if (retryAtOnce && spentBackoff)
            {
                Raise(Signal.Document);
            }
            else
            {
                Arm(_documentTimer, Timings.Persistence.WriteRetryInterval);
            }
        }
    }

    private async Task SaveUsageAsync(CancellationToken cancellationToken)
    {
        UserDocument document;
        lock (_gate)
        {
            if (!_usageDirty || _latest is null)
            {
                return;
            }

            document = _latest;
            _usageDirty = false;
            Disarm(_usageTimer);
        }

        Result<SaveReceipt> result;
        try
        {
            result = await _usage
                .SaveAsync(
                    document.Frequents.UsageEpoch,
                    document.Frequents.Usage,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFault(_logger, ex);
            result = Results.Fail<SaveReceipt>(Unexpected());
        }

        if (result.IsFailure)
        {
            LogUsageFailed(_logger, result.Failure.Code);
            lock (_gate)
            {
                if (!_usageDirty)
                {
                    _usageDirty = true;
                    _usageDirtySince = _time.GetTimestamp();
                }

                Arm(_usageTimer, Timings.Persistence.WriteRetryInterval);
            }
        }
    }

    private async Task BackupAsync(CancellationToken cancellationToken)
    {
        UserDocument? document;
        lock (_gate)
        {
            document = _backupCandidate;
            _backupCandidate = null;
        }

        if (document is null || !document.Settings.Reliability.AutoBackup)
        {
            return;
        }

        try
        {
            var result = await _backups
                .CreateAsync(document, BackupKind.Auto, cancellationToken)
                .ConfigureAwait(false);
            if (result.IsFailure)
            {
                LogBackupFailed(_logger, result.Failure.Code);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogSaveFault(_logger, ex);
        }
    }

    /// <summary>
    /// The failure has lasted <c>UnsavedNoticeAfter</c>: it becomes visible now, even while a retry is still running.
    /// </summary>
    private void Notice()
    {
        lock (_gate)
        {
            _visible |= _failingSince is not null;
        }

        PublishStatus();
    }

    private void PublishStatus()
    {
        SaveStatus status;
        lock (_gate)
        {
            status =
                _failingSince is not null ? (_visible ? SaveStatus.Failing : SaveStatus.Retrying)
                : _documentDirty ? SaveStatus.Pending
                : SaveStatus.Saved;
        }

        if (Interlocked.Exchange(ref _status, (int)status) != (int)status)
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [LoggerMessage(
        EventId = 5151,
        Level = LogLevel.Warning,
        Message = "persist.save_failed: {Code}, failing for {Elapsed}"
    )]
    private static partial void LogSaveFailed(ILogger logger, string code, TimeSpan elapsed);

    [LoggerMessage(
        EventId = 5152,
        Level = LogLevel.Information,
        Message = "persist.saved_again: document seq {Seq} after a failure"
    )]
    private static partial void LogSavedAgain(ILogger logger, long seq);

    [LoggerMessage(
        EventId = 5153,
        Level = LogLevel.Information,
        Message = "usage.save_failed: {Code}"
    )]
    private static partial void LogUsageFailed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 5154,
        Level = LogLevel.Warning,
        Message = "backup.auto_failed: {Code}"
    )]
    private static partial void LogBackupFailed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 5155,
        Level = LogLevel.Error,
        Message = "persist.fault: a save failed unexpectedly"
    )]
    private static partial void LogSaveFault(ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = 5156,
        Level = LogLevel.Error,
        Message = "persist.flush_timeout: the final flush did not finish in time"
    )]
    private static partial void LogFlushTimedOut(ILogger logger);

    /// <summary>What the single consumer does next.</summary>
    private enum Signal
    {
        Evaluate,
        Document,
        Usage,
        Backup,
    }
}
