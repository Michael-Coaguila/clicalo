using System.Collections.Immutable;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Updates;

/// <summary>
/// The updates of the installed copy (ACT-001 to ACT-005, NFR-010, ADR-0027):
/// <list type="bullet">
/// <item>with «Actualizar automáticamente» it checks the channel at start and every
/// <c>Timings.Updates.UpdateCheckInterval</c>; a version found is offered (the counter of Sistema);</item>
/// <item>it installs only when asked ([Instalar ahora]) or, automatically and without «Avisar antes», once the panel has
/// gone <c>Timings.Updates.UpdateIdleRequired</c> without use; it downloads with the checksum verification of
/// Velopack, takes the <c>pre-update</c> backup and ends the instance cleanly so the updater can install;</item>
/// <item>it never goes down by itself: a channel that offers an older version (Beta → Estable) is ignored until it
/// catches up; the only downgrade is [Volver], confirmed with two taps, to the version before the last update and
/// during <c>Timings.Backups.PreviousVersionRetention</c>.</item>
/// </list>
/// One operation at a time; the status is published on any thread.
/// </summary>
public sealed partial class UpdateService : IUpdateService, IDisposable
{
    private readonly IUpdateClient _client;
    private readonly IUpdateStateStore _stateStore;
    private readonly TimeProvider _time;
    private readonly UpdateHooks _hooks;
    private readonly ILogger<UpdateService> _logger;
    private readonly SemaphoreSlim _busy = new(1, 1);
    private readonly Lock _gate = new();
    private readonly ImmutableArray<ReleaseNotes> _currentNotes;
    private UpdateStatus _status;
    private UpdateState? _state;
    private UpdateOffer? _offer;
    private ITimer? _checkTimer;
    private ITimer? _idleTimer;

    /// <summary>Creates the service over a client and a state store (tests use fakes).</summary>
    internal UpdateService(
        IUpdateClient client,
        IUpdateStateStore stateStore,
        TimeProvider time,
        UpdateHooks hooks,
        ILogger<UpdateService> logger
    )
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(logger);
        _client = client;
        _stateStore = stateStore;
        _time = time;
        _hooks = hooks;
        _logger = logger;
        var current = client.CurrentVersion;
        var notes = ReleaseNotesParser.Parse(current, client.CurrentNotes, isNew: false);
        _currentNotes = notes.Items.IsEmpty ? [] : [notes];
        _status = client.IsInstalled
            ? new UpdateStatus(
                UpdatePhase.UpToDate,
                current,
                null,
                0,
                UpdateError.None,
                null,
                null,
                _currentNotes
            )
            : UpdateStatus.Unavailable(current);
    }

    /// <inheritdoc />
    public event EventHandler? StatusChanged;

    /// <inheritdoc />
    public UpdateStatus Status
    {
        get
        {
            lock (_gate)
            {
                return _status;
            }
        }
    }

    /// <summary>
    /// The updates of this installation: Velopack over the GitHub Releases of the public repository (HTTPS), the state in
    /// <c>update.json</c> of the local data folder.
    /// </summary>
    /// <param name="locations">Where the data lives.</param>
    /// <param name="writer">The atomic writer of <c>update.json</c>.</param>
    /// <param name="time">The clock of the checks and the idle wait.</param>
    /// <param name="hooks">Settings, idle time, backup and exit.</param>
    /// <param name="logger">Logs codes, never content.</param>
    /// <param name="enabled">False for a run with isolated data (<c>cl run</c>): no updates at all.</param>
    public static UpdateService Create(
        DataLocations locations,
        IAtomicFileWriter writer,
        TimeProvider time,
        UpdateHooks hooks,
        ILogger<UpdateService> logger,
        bool enabled
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        return new UpdateService(
            new VelopackUpdateClient(UpdateChannels.Repository, enabled),
            new UpdateStateFile(
                Path.Combine(locations.LocalRoot ?? locations.Root, "update.json"),
                writer
            ),
            time,
            hooks,
            logger
        );
    }

    /// <summary>
    /// Notices the first run of a new version («Actualizado», ACT-001) and the rollback window (ACT-005), then, with
    /// «Actualizar automáticamente», checks now and every <c>Timings.Updates.UpdateCheckInterval</c>.
    /// </summary>
    /// <param name="cancellationToken">Cancels the first check.</param>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsInstalled)
        {
            return;
        }

        var now = _time.GetUtcNow();
        var current = _client.CurrentVersion;
        var saved = _stateStore.Load();
        var phase = UpdatePhase.UpToDate;
        UpdateState next;
        if (saved is null)
        {
            next = new UpdateState(current, null, null);
        }
        else if (VersionOrder.IsNewer(current, saved.LastRunVersion))
        {
            next = new UpdateState(current, saved.LastRunVersion, now);
            phase = UpdatePhase.Updated;
            LogUpdated(_logger, current);
        }
        else if (VersionOrder.Compare(current, saved.LastRunVersion) < 0)
        {
            // Back from [Volver]: there is nothing older to go back to.
            next = new UpdateState(current, null, null);
        }
        else
        {
            next = saved;
        }

        if (next != saved)
        {
            await SaveStateAsync(next, cancellationToken).ConfigureAwait(false);
        }

        lock (_gate)
        {
            _state = next;
        }

        Publish(s => s with { Phase = phase, RollbackVersion = RollbackOf(next) });
        _checkTimer = _time.CreateTimer(
            static state => _ = ((UpdateService)state!).AutoCheckAsync(),
            this,
            Timings.Updates.UpdateCheckInterval,
            Timings.Updates.UpdateCheckInterval
        );
        await AutoCheckAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        if (!_client.IsInstalled || !await _busy.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await CheckCoreAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _busy.Release();
        }

        ScheduleAutoInstall();
    }

    /// <inheritdoc />
    public async Task InstallAsync(CancellationToken cancellationToken)
    {
        UpdateOffer? offer;
        lock (_gate)
        {
            offer = _offer;
        }

        if (offer is null || !await _busy.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        try
        {
            await InstallCoreAsync(offer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _busy.Release();
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(ConfirmationToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        var previous = Status.RollbackVersion;
        if (
            previous is null
            || !string.Equals(
                token.Subject.Operation,
                UpdateStatus.RollbackOperation,
                StringComparison.Ordinal
            )
            || !string.Equals(token.Subject.Target, previous, StringComparison.Ordinal)
            || !await _busy.WaitAsync(0, cancellationToken).ConfigureAwait(false)
        )
        {
            return;
        }

        try
        {
            Publish(s => s with
            {
                Phase = UpdatePhase.Installing,
                NewVersion = previous,
                Percent = 0,
                Error = UpdateError.None,
            });
            UpdateOffer? offer;
            try
            {
                offer = await _client
                    .FindAsync(_hooks.Settings().Channel, previous, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                Fail(Classify(ex));
                return;
            }

            if (offer is null)
            {
                // The previous version is no longer published: nothing to go back to.
                LogRollbackMissing(_logger, previous);
                Fail(UpdateError.Offline);
                return;
            }

            LogRollback(_logger, previous);
            await InstallCoreAsync(offer, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _ = _busy.Release();
        }
    }

    /// <inheritdoc />
    public void Acknowledge() =>
        Publish(s =>
            s.Phase == UpdatePhase.Updated ? s with { Phase = UpdatePhase.UpToDate } : s
        );

    /// <inheritdoc />
    public void Dispose()
    {
        _checkTimer?.Dispose();
        _idleTimer?.Dispose();
        _busy.Dispose();
    }

    /// <summary>The version [Volver] goes back to, while the window of ACT-005 lasts.</summary>
    private string? RollbackOf(UpdateState? state) =>
        state is { PreviousVersion: { } previous, UpdatedAt: { } at }
        && _time.GetUtcNow() - at < Timings.Backups.PreviousVersionRetention
            ? previous
            : null;

    private async Task AutoCheckAsync()
    {
        try
        {
            if (_hooks.Settings().Automatic && Status.Phase is not UpdatePhase.Installing)
            {
                await CheckAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(_logger, ex.GetType().Name);
        }
    }

    private async Task CheckCoreAsync(CancellationToken cancellationToken)
    {
        var previous = Status.Phase;
        Publish(s => s with { Phase = UpdatePhase.Checking, Error = UpdateError.None });
        UpdateOffer? offer;
        try
        {
            offer = await _client
                .FindAsync(_hooks.Settings().Channel, null, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(Classify(ex));
            return;
        }

        var now = _time.GetUtcNow();
        var current = _client.CurrentVersion;
        if (offer is null || offer.IsDowngrade || !VersionOrder.IsNewer(offer.Version, current))
        {
            // Never down by itself (blueprint §9.3): Beta → Estable waits for Estable to catch up.
            lock (_gate)
            {
                _offer = null;
            }

            Publish(s => s with
            {
                Phase = previous == UpdatePhase.Updated ? UpdatePhase.Updated : UpdatePhase.UpToDate,
                NewVersion = null,
                LastChecked = now,
                RollbackVersion = RollbackOf(_state),
                Notes = _currentNotes,
            });
            return;
        }

        lock (_gate)
        {
            _offer = offer;
        }

        LogFound(_logger, offer.Version);
        var notes = ReleaseNotesParser.Parse(offer.Version, offer.Notes, isNew: true);
        Publish(s => s with
        {
            Phase = UpdatePhase.Found,
            NewVersion = offer.Version,
            Percent = 0,
            LastChecked = now,
            Notes = notes.Items.IsEmpty ? _currentNotes : [notes, .. _currentNotes],
        });
    }

    private async Task InstallCoreAsync(UpdateOffer offer, CancellationToken cancellationToken)
    {
        Publish(s => s with
        {
            Phase = UpdatePhase.Installing,
            NewVersion = offer.Version,
            Percent = 0,
            Error = UpdateError.None,
        });
        try
        {
            await _client
                .DownloadAsync(
                    offer,
                    percent => Publish(s => s with { Percent = Math.Clamp(percent, 0, 100) }),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (
                _hooks.Settings().BackupBefore
                && !await _hooks.BackupBeforeInstall(cancellationToken).ConfigureAwait(false)
            )
            {
                // Never install without the copy the person asked for (ACT-003, REG-08).
                LogBackupFailed(_logger);
                Fail(UpdateError.Interrupted);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            _client.ApplyAfterExit(offer);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Fail(Classify(ex));
            return;
        }

        LogInstalling(_logger, offer.Version);
        await _hooks.ExitForInstall().ConfigureAwait(false);
    }

    /// <summary>
    /// Automatically and without «Avisar antes»: installs once the panel has gone
    /// <c>Timings.Updates.UpdateIdleRequired</c> without use, checking again when that time is due.
    /// </summary>
    private void ScheduleAutoInstall()
    {
        var settings = _hooks.Settings();
        if (!settings.Automatic || settings.AskBefore || Status.Phase != UpdatePhase.Found)
        {
            return;
        }

        var left = Timings.Updates.UpdateIdleRequired - _hooks.IdleFor();
        if (left <= TimeSpan.Zero)
        {
            _ = InstallWhenIdleAsync();
            return;
        }

        _idleTimer?.Dispose();
        _idleTimer = _time.CreateTimer(
            static state => ((UpdateService)state!).ScheduleAutoInstall(),
            this,
            left,
            Timeout.InfiniteTimeSpan
        );
    }

    private async Task InstallWhenIdleAsync()
    {
        try
        {
            await InstallAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(_logger, ex.GetType().Name);
        }
    }

    private void Fail(UpdateError error)
    {
        LogError(_logger, error);
        Publish(s => s with { Phase = UpdatePhase.Failed, Error = error });
    }

    private static UpdateError Classify(Exception exception) =>
        exception switch
        {
            UpdateFailedException failed => failed.Error,
            HttpRequestException or TimeoutException => UpdateError.Offline,
            IOException io when (io.HResult & 0xFFFF) is 0x70 or 0x27 => UpdateError.NoSpace,
            _ => UpdateError.Interrupted,
        };

    private async Task SaveStateAsync(UpdateState state, CancellationToken cancellationToken)
    {
        try
        {
            await _stateStore.SaveAsync(state, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogFailed(_logger, ex.GetType().Name);
        }
    }

    private void Publish(Func<UpdateStatus, UpdateStatus> change)
    {
        bool changed;
        lock (_gate)
        {
            var next = change(_status);
            changed = next != _status;
            _status = next;
        }

        if (changed)
        {
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "update.found {Version}")]
    private static partial void LogFound(ILogger logger, string version);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "update.installing {Version}"
    )]
    private static partial void LogInstalling(ILogger logger, string version);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "update.error {Error}")]
    private static partial void LogError(ILogger logger, UpdateError error);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "update.failed {Failure}")]
    private static partial void LogFailed(ILogger logger, string failure);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "update.updated {Version}")]
    private static partial void LogUpdated(ILogger logger, string version);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "update.rollback {Version}")]
    private static partial void LogRollback(ILogger logger, string version);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "update.rollback_missing {Version}"
    )]
    private static partial void LogRollbackMissing(ILogger logger, string version);

    [LoggerMessage(EventId = 8, Level = LogLevel.Warning, Message = "update.backup_failed")]
    private static partial void LogBackupFailed(ILogger logger);
}
