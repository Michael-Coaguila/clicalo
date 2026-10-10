using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Confirmation;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases.Backups;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Clicalo.Presentation.ControlCenter.SystemSection;

/// <summary>
/// «Sistema» of the Control Center (docs/05 §5, SIS-001, SIS-002 as modified by D7, ACT-001 to ACT-005, COP-002 to
/// COP-005): three tabs joined to their content, each with its state. It forwards every intention to the updates, the
/// backups, the startup, the elevation and the uninstaller; the rules live there and in the Application (import plans,
/// the review of imported content, two taps). A backup that is imported or restored is imported content: its Web, App
/// and Macro shortcuts that the document does not have yet are confirmed one by one first (LOG-008,
/// <see cref="ImportReview"/>). «Desinstalar Clícalo» keeps the data unless the person asks to delete it, and then
/// only after a copy saved where they choose (NFR-010, P6). Projections are coalesced to one per dispatcher turn.
/// </summary>
public sealed class SystemSectionViewModel : ObservableObject
{
    private readonly ControlCenterServices _s;
    private readonly SystemServices _sys;
    private SystemTab _tab = SystemTab.Updates;
    private SystemScreen _screen;
    private ImmutableArray<BackupInfo> _backups = [];
    private ImportPick? _pick;
    private RestoreReview? _restore;
    private ValueList<Shortcut> _pending = [];
    private readonly HashSet<ShortcutId> _confirmed = [];
    private bool _deleteData;
    private bool _queued;
    private bool _busy;
    private bool _elevating;
    private bool _startEnabled;

    /// <summary>Creates the section and projects it.</summary>
    /// <param name="services">The services of the Control Center.</param>
    /// <param name="system">The services of «Sistema».</param>
    public SystemSectionViewModel(ControlCenterServices services, SystemServices system)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(system);
        _s = services;
        _sys = system;
        _startEnabled = system.Startup.IsEnabled;
        system.Updates.StatusChanged += (_, _) => _s.Post(Invalidate);
        _screen = Build();
        _ = Guard(LoadBackupsAsync);
    }

    /// <summary>Raised with a message for the status bar (CCM-003).</summary>
    public event EventHandler<WorkspaceNoticeEventArgs>? Noticed;

    /// <summary>Everything the section shows.</summary>
    public SystemScreen Screen
    {
        get => _screen;
        private set => SetProperty(ref _screen, value);
    }

    /// <summary>The counter of «Sistema» in the side menu: 1 while there is a new version (ACT-001).</summary>
    public int UpdateCount => _sys.Updates.Status.HasNewVersion ? 1 : 0;

    private UserSettings Settings => _s.Store.Current.Settings;

    private CultureInfo Culture => _s.Localization.Current.Locale.Culture;

    /// <summary>Projects again once the current work of the dispatcher is done.</summary>
    public void Invalidate()
    {
        if (_queued)
        {
            return;
        }

        _queued = true;
        _s.Post(Refresh);
    }

    /// <summary>Projects the section now.</summary>
    public void Refresh()
    {
        _queued = false;
        Screen = Build();
        OnPropertyChanged(nameof(UpdateCount));
    }

    /// <summary>The section came into view: the history is read again.</summary>
    public void OnShown()
    {
        _startEnabled = _sys.Startup.IsEnabled;
        _ = Guard(LoadBackupsAsync);
        Invalidate();
    }

    /// <summary>Esc with the question of Importar open (CCM-001): closes it and says whether there was one.</summary>
    public bool CloseMenu()
    {
        if (_pick is null && _restore is null)
        {
            return false;
        }

        ClearReview();
        _s.Confirm.Disarm();
        Invalidate();
        return true;
    }

    /// <summary>A tab (SIS-001).</summary>
    /// <param name="tab">The tab.</param>
    public void SelectTab(SystemTab tab)
    {
        _tab = tab;
        ClearReview();
        _s.Confirm.Disarm();
        if (tab == SystemTab.Backups)
        {
            _ = Guard(LoadBackupsAsync);
        }

        Invalidate();
    }

    /// <summary>
    /// The button of the update card (ACT-001): [Buscar actualizaciones], [Instalar ahora], [Listo] or [Reintentar].
    /// </summary>
    public void UpdateAction()
    {
        var updates = _sys.Updates;
        switch (updates.Status.Phase)
        {
            case UpdatePhase.UpToDate or UpdatePhase.Failed:
                _ = Guard(() => updates.CheckAsync(CancellationToken.None));
                break;
            case UpdatePhase.Found:
                _ = Guard(() => updates.InstallAsync(CancellationToken.None));
                break;
            case UpdatePhase.Updated:
                updates.Acknowledge();
                break;
        }
    }

    /// <summary>A switch of «Actualizaciones» (ACT-002): 0 automatic, 1 ask before, 2 backup before.</summary>
    /// <param name="index">The switch.</param>
    public void ToggleUpdateSwitch(int index)
    {
        var updates = Settings.Updates;
        var (path, value) = index switch
        {
            0 => (SettingPaths.UpdatesAutomatic, updates.Automatic),
            1 => (SettingPaths.UpdatesAskBefore, updates.AskBefore),
            _ => (SettingPaths.UpdatesBackupBefore, updates.BackupBefore),
        };
        _ = _s.Store.Dispatch(new SetSetting(path, !value));
        Invalidate();
    }

    /// <summary>Estable or Beta (ACT-002); the new channel is read at once.</summary>
    /// <param name="channel">The channel.</param>
    public void SetChannel(UpdateChannel channel)
    {
        if (channel == Settings.Updates.Channel)
        {
            return;
        }

        _ = _s.Store.Dispatch(new SetSetting(SettingPaths.UpdatesChannel, channel));
        if (
            _sys.Updates.Status.Phase
            is UpdatePhase.UpToDate
                or UpdatePhase.Found
                or UpdatePhase.Failed
        )
        {
            _ = Guard(() => _sys.Updates.CheckAsync(CancellationToken.None));
        }

        Invalidate();
    }

    /// <summary>[Volver] (ACT-005): the first tap arms, the second goes back to the previous version (REG-04).</summary>
    public void Rollback()
    {
        if (_sys.Updates.Status.RollbackVersion is not { } version)
        {
            return;
        }

        switch (_s.Confirm.Tap(new ConfirmationSubject(UpdateStatus.RollbackOperation, version)))
        {
            case TwoStepResult.Confirmed confirmed:
                _ = Guard(() =>
                    _sys.Updates.RollbackAsync(confirmed.Token, CancellationToken.None)
                );
                break;
            case TwoStepResult.Armed armed:
                Rearm(armed);
                break;
        }

        Invalidate();
    }

    /// <summary>[Crear copia ahora] (COP-002): a manual backup at the top of the history and [backupT].</summary>
    public void BackupNow() =>
        _ = Busy(async () =>
        {
            var created = await _sys
                .Backups.CreateAsync(_s.Store.Current, CancellationToken.None)
                .ConfigureAwait(true);
            Notify(created ? L.BackupT : L.BackupFailed, "backup", warning: !created);
            await LoadBackupsAsync().ConfigureAwait(true);
        });

    /// <summary>[Exportar] (COP-002): the person chooses where; a copy of the document is written there.</summary>
    public void Export() =>
        _ = Busy(async () =>
        {
            var outcome = await _sys
                .Backups.ExportAsync(_s.Store.Current, CancellationToken.None)
                .ConfigureAwait(true);
            switch (outcome)
            {
                case ExportOutcome.Done:
                    Notify(L.ExportDone, "upload", warning: false);
                    break;
                case ExportOutcome.Failed:
                    Notify(L.ExportFailed, "warning", warning: true);
                    break;
            }

            await LoadBackupsAsync().ConfigureAwait(true);
        });

    /// <summary>
    /// [Importar] (COP-002, decision of order): first the file, read and summarized; then the question Combinar or
    /// Reemplazar. A second tap with the question open closes it.
    /// </summary>
    public void Import()
    {
        if (_pick is not null || _restore is not null)
        {
            _ = CloseMenu();
            return;
        }

        _ = Busy(async () =>
        {
            var picked = await _sys
                .Backups.PickImportAsync(CancellationToken.None)
                .ConfigureAwait(true);
            if (picked is not { } result)
            {
                return;
            }

            if (result.TryGetValue(out var pick))
            {
                ClearReview();
                _pick = pick;
                _pending = ImportReview.Pending(_s.Store.Current, pick.Document);
            }
            else
            {
                Notify(result.Failure.Message, "warning", warning: true);
            }
        });
    }

    /// <summary>[impMerge] (COP-002): what is missing is added, with undo and a backup before.</summary>
    public void ImportMerge()
    {
        if (_pick is not { } pick)
        {
            return;
        }

        var current = _s.Store.Current;
        var plan = ImportReview
            .Confirmed(current, pick.Document, _confirmed)
            .Bind(reviewed => ImportPlanner.Merge(current, reviewed, _sys.Ids));
        var applied = plan.Bind(p => _s.Store.Dispatch(new MergeOnImport(p.Next.Library)));
        ClearReview();
        Notify(
            applied.IsSuccess ? L.ImpMerged : applied.Failure.Message,
            applied.IsSuccess ? "merge" : "warning",
            warning: applied.IsFailure,
            undo: applied.IsSuccess
        );
        Invalidate();
    }

    /// <summary>[impReplace] (COP-002, REG-04): two taps, with undo and a backup before.</summary>
    public void ImportReplace()
    {
        if (_restore is { } restore)
        {
            RestoreReviewed(restore);
            return;
        }

        if (_pick is not { } pick)
        {
            return;
        }

        switch (_s.Confirm.Tap(new ConfirmationSubject(nameof(ReplaceOnImport), pick.Version)))
        {
            case TwoStepResult.Confirmed confirmed:
                var current = _s.Store.Current;
                var plan = ImportReview
                    .Confirmed(current, pick.Document, _confirmed)
                    .Bind(reviewed => ImportPlanner.Replace(current, reviewed));
                var applied = plan.Bind(p =>
                    _s.Store.Dispatch(new ReplaceOnImport(p.Next.Library), confirmed.Token)
                );
                ClearReview();
                Notify(
                    applied.IsSuccess ? L.ImpReplaced : applied.Failure.Message,
                    applied.IsSuccess ? "swap_horiz" : "warning",
                    warning: applied.IsFailure,
                    undo: applied.IsSuccess
                );
                break;
            case TwoStepResult.Armed armed:
                Rearm(armed);
                break;
        }

        Invalidate();
    }

    /// <summary>
    /// A row of the review of an import or a restore (LOG-008): ticks or unticks a Web, App or Macro shortcut. Only the
    /// ticked ones are installed.
    /// </summary>
    /// <param name="id">The shortcut.</param>
    public void ToggleReview(ShortcutId id)
    {
        if (_pick is null && _restore is null)
        {
            return;
        }

        if (!_confirmed.Remove(id))
        {
            _ = _confirmed.Add(id);
        }

        _s.Confirm.Disarm();
        Invalidate();
    }

    /// <summary>«Copia automática» (COP-003).</summary>
    public void ToggleAutoBackup()
    {
        _ = _s.Store.Dispatch(
            new SetSetting(SettingPaths.AutoBackup, !Settings.Reliability.AutoBackup)
        );
        Invalidate();
    }

    /// <summary>[Restaurar] of the history (COP-004, REG-04): two taps; a backup of the current state is taken first.</summary>
    /// <param name="id">The backup.</param>
    public void Restore(BackupId id)
    {
        switch (_s.Confirm.Tap(new ConfirmationSubject(nameof(RestoreBackup), id.Value)))
        {
            case TwoStepResult.Confirmed confirmed:
                _ = Busy(async () =>
                {
                    var read = await _sys
                        .Backups.ReadAsync(id, CancellationToken.None)
                        .ConfigureAwait(true);
                    if (
                        read.TryGetValue(out var backup)
                        && ImportReview.Pending(_s.Store.Current, backup)
                            is { IsEmpty: false } pending
                    )
                    {
                        // LOG-008: a backup is imported content. What it would add that opens or runs something
                        // is confirmed one by one first; [Restaurar] of that review asks for its own two taps.
                        ClearReview();
                        _restore = new RestoreReview(id, backup);
                        _pending = pending;
                        return;
                    }

                    var restored = read.Bind(document =>
                        _s.Store.Dispatch(new RestoreBackup(document), confirmed.Token)
                    );
                    Notify(
                        restored.IsSuccess ? RestoredNotice(id) : restored.Failure.Message,
                        restored.IsSuccess ? "restore" : "warning",
                        warning: restored.IsFailure,
                        undo: restored.IsSuccess
                    );
                    await LoadBackupsAsync().ConfigureAwait(true);
                });
                break;
            case TwoStepResult.Armed armed:
                Rearm(armed);
                break;
        }

        Invalidate();
    }

    /// <summary>«Borrar también mis atajos y ajustes» of «Desinstalar Clícalo» (NFR-010, P6): off by default.</summary>
    public void ToggleDeleteData()
    {
        _deleteData = !_deleteData;
        _s.Confirm.Disarm();
        Invalidate();
    }

    /// <summary>
    /// [Desinstalar] (NFR-010, P6, REG-04): two taps. The data is kept unless «Borrar también mis atajos y ajustes» is
    /// on; then the person first saves a copy where they choose, and without that copy nothing is uninstalled nor
    /// deleted (REG-08). When the uninstaller starts, this instance ends cleanly.
    /// </summary>
    public void Uninstall()
    {
        if (!_sys.Uninstall.IsAvailable)
        {
            Notify(L.UninstallNotInstalled, "info", warning: true);
            return;
        }

        var deleteData = _deleteData;
        switch (
            _s.Confirm.Tap(
                new ConfirmationSubject(ISystemUninstall.Operation, UninstallTarget(deleteData))
            )
        )
        {
            case TwoStepResult.Confirmed confirmed:
                _ = Busy(async () =>
                {
                    if (
                        deleteData
                        && await _sys
                            .Backups.ExportAsync(_s.Store.Current, CancellationToken.None)
                            .ConfigureAwait(true) != ExportOutcome.Done
                    )
                    {
                        Notify(L.UninstallNeedsCopy, "warning", warning: true);
                        return;
                    }

                    var started = await _sys
                        .Uninstall.UninstallAsync(
                            deleteData,
                            confirmed.Token,
                            CancellationToken.None
                        )
                        .ConfigureAwait(true);
                    if (!started)
                    {
                        Notify(L.UninstallFailed, "warning", warning: true);
                    }
                });
                break;
            case TwoStepResult.Armed armed:
                Rearm(armed);
                break;
        }

        Invalidate();
    }

    /// <summary>«Iniciar con Windows» (SIS-002): the <c>Run</c> entry of the installed copy, never elevated.</summary>
    public void ToggleStartWithWindows()
    {
        var startup = _sys.Startup;
        if (!startup.IsAvailable)
        {
            Notify(L.StartNotInstalled, "info", warning: true);
            return;
        }

        var wanted = !_startEnabled;
        if (!startup.TrySetEnabled(wanted))
        {
            Notify(L.StartFailed, "warning", warning: true);
            return;
        }

        _startEnabled = startup.IsEnabled;
        if (Settings.Reliability.StartWithWindows != _startEnabled)
        {
            _ = _s.Store.Dispatch(new SetSetting(SettingPaths.StartWithWindows, _startEnabled));
        }

        Invalidate();
    }

    /// <summary>
    /// «Reabrir como administrador» (user decision D7): Windows asks with its UAC; when the elevated instance started,
    /// this one ends cleanly. Cancelling leaves everything as it was, with an explanation (SIS-002).
    /// </summary>
    public void ReopenAsAdmin()
    {
        if (_sys.Elevation.IsElevated || _elevating)
        {
            return;
        }

        _elevating = true;
        Invalidate();
        _ = Guard(async () =>
        {
            try
            {
                var outcome = await _sys
                    .Elevation.RelaunchAsync(CancellationToken.None)
                    .ConfigureAwait(true);
                switch (outcome)
                {
                    case ElevationOutcome.Started:
                        await _sys.EndForHandover().ConfigureAwait(true);
                        return;
                    case ElevationOutcome.Cancelled:
                        Notify(L.AdminCancelled, "admin_panel_settings", warning: true);
                        break;
                    case ElevationOutcome.NotInstalled:
                        Notify(L.AdminNotInstalled, "admin_panel_settings", warning: true);
                        break;
                    default:
                        Notify(L.AdminFailed, "warning", warning: true);
                        break;
                }
            }
            finally
            {
                _elevating = false;
                Invalidate();
            }
        });
    }

    private string T(Message message) => _s.Localization.Current.Format(message);

    /// <summary>«Copia restaurada: {fecha}» (COP-004), with the date of the backup as the history shows it.</summary>
    private Message RestoredNotice(BackupId id)
    {
        foreach (var backup in _backups)
        {
            if (backup.Id == id)
            {
                return L.RestoredAt(date: When(backup.CreatedAt));
            }
        }

        return L.RestoredT;
    }

    private void Notify(Message text, string icon, bool warning, bool undo = false) =>
        Noticed?.Invoke(
            this,
            new WorkspaceNoticeEventArgs(new WorkspaceNotice(text, icon, undo, warning))
        );

    private static string UninstallTarget(bool deleteData) =>
        deleteData ? "delete-data" : "keep-data";

    private static string ReviewTarget(BackupId id) => "review:" + id.Value;

    private void ClearReview()
    {
        _pick = null;
        _restore = null;
        _pending = [];
        _confirmed.Clear();
    }

    /// <summary>[Restaurar] of the review of a backup (LOG-008, REG-04): two taps; only what was ticked comes back.</summary>
    private void RestoreReviewed(RestoreReview restore)
    {
        switch (
            _s.Confirm.Tap(new ConfirmationSubject(nameof(RestoreBackup), ReviewTarget(restore.Id)))
        )
        {
            case TwoStepResult.Confirmed confirmed:
                var restored = ImportReview
                    .Confirmed(_s.Store.Current, restore.Document, _confirmed)
                    .Bind(document =>
                        _s.Store.Dispatch(new RestoreBackup(document), confirmed.Token)
                    );
                ClearReview();
                Notify(
                    restored.IsSuccess ? RestoredNotice(restore.Id) : restored.Failure.Message,
                    restored.IsSuccess ? "restore" : "warning",
                    warning: restored.IsFailure,
                    undo: restored.IsSuccess
                );
                _ = Guard(LoadBackupsAsync);
                break;
            case TwoStepResult.Armed armed:
                Rearm(armed);
                break;
        }

        Invalidate();
    }

    private void Rearm(TwoStepResult.Armed armed) =>
        _ = _s.Time.CreateTimer(
            _ => _s.Post(Invalidate),
            null,
            armed.Until - _s.Time.GetUtcNow(),
            Timeout.InfiniteTimeSpan
        );

    private bool IsArmed(string operation, string target) =>
        _s.Confirm.ArmedSubject is { } armed
        && string.Equals(armed.Operation, operation, StringComparison.Ordinal)
        && string.Equals(armed.Target, target, StringComparison.Ordinal);

    private async Task LoadBackupsAsync()
    {
        _backups = await _sys.Backups.ListAsync(CancellationToken.None).ConfigureAwait(true);
        Invalidate();
    }

    private async Task Busy(Func<Task> work)
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        Invalidate();
        try
        {
            await Guard(work).ConfigureAwait(true);
        }
        finally
        {
            _busy = false;
            Invalidate();
        }
    }

    private static async Task Guard(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // NFR-005: an unexpected failure of a button never closes the Control Center; the service logged it.
            _ = ex;
        }
    }

    private SystemScreen Build()
    {
        var status = _sys.Updates.Status;
        return new SystemScreen(
            T(L.SysTitle),
            T(L.SysSub),
            [
                new SystemTabModel(
                    SystemTab.Updates,
                    status.HasNewVersion ? "new_releases" : "verified",
                    T(L.UpdTitle),
                    status.HasNewVersion
                        ? T(L.UpdAvail)
                        : "v" + status.CurrentVersion + " · " + T(L.UpToDateShort),
                    status.HasNewVersion,
                    _tab == SystemTab.Updates
                ),
                new SystemTabModel(
                    SystemTab.Backups,
                    "backup",
                    T(L.BackupsT),
                    _backups.IsEmpty
                        ? T(L.BackupNone)
                        : T(L.LastBackupAt(date: When(_backups[0].CreatedAt))),
                    false,
                    _tab == SystemTab.Backups
                ),
                new SystemTabModel(
                    SystemTab.Start,
                    "power_settings_new",
                    T(L.StartT),
                    _startEnabled ? T(L.StStart) : T(L.ManualStart),
                    false,
                    _tab == SystemTab.Start
                ),
            ],
            _tab,
            Updates(status),
            Backups(),
            Start()
        );
    }

    private UpdatesModel Updates(UpdateStatus status)
    {
        var settings = Settings.Updates;
        var language = _s.Localization.Current.Locale.Code;
        return new UpdatesModel(
            Card(status),
            [
                new SwitchModel("autorenew", T(L.UpdAuto), T(L.UpdAutoD), settings.Automatic),
                new SwitchModel("notifications", T(L.UpdAsk), T(L.UpdAskD), settings.AskBefore),
                new SwitchModel("backup", T(L.UpdBackup), T(L.UpdBackupD), settings.BackupBefore),
            ],
            T(L.Channel),
            T(L.ChannelD),
            [
                new ChannelOption(
                    UpdateChannel.Stable,
                    T(L.Stable),
                    settings.Channel == UpdateChannel.Stable
                ),
                new ChannelOption(
                    UpdateChannel.Beta,
                    T(L.Beta),
                    settings.Channel == UpdateChannel.Beta
                ),
            ],
            T(L.WhatsNew),
            [
                .. status.Notes.Select(notes => new NotesModel(
                    notes.Version,
                    notes.Date is { } date
                        ? date.ToString("MMM yyyy", Culture)
                            .Replace(".", string.Empty, StringComparison.Ordinal)
                        : string.Empty,
                    notes.IsNew ? T(L.NewBadge) : string.Empty,
                    [.. notes.In(language)]
                )),
            ],
            status.RollbackVersion is { } previous && status.Phase != UpdatePhase.Unavailable
                ? new RollbackModel(
                    T(L.RollbackT(version: previous)),
                    T(L.RollbackD),
                    IsArmed(UpdateStatus.RollbackOperation, previous)
                        ? T(L.DelConfirm)
                        : T(L.RollbackBtn),
                    IsArmed(UpdateStatus.RollbackOperation, previous)
                )
                : null
        );
    }

    private UpdateCardModel Card(UpdateStatus status)
    {
        var current = "v" + status.CurrentVersion;
        var next = "v" + status.NewVersion;
        return status.Phase switch
        {
            UpdatePhase.Unavailable => new UpdateCardModel(
                "info",
                T(L.UpdUnavailT),
                _sys.Elevation.IsElevated ? T(L.AdminActive) : T(L.UpdUnavailD),
                false,
                0,
                T(L.CheckNow),
                false,
                false,
                false
            ),
            UpdatePhase.Checking => new UpdateCardModel(
                "sync",
                T(L.Checking),
                current,
                false,
                0,
                T(L.Checking),
                false,
                false,
                false
            ),
            UpdatePhase.Found => new UpdateCardModel(
                "new_releases",
                T(L.Found) + " · " + next,
                T(L.UpdBackupD),
                false,
                0,
                T(L.InstallNow),
                true,
                true,
                false
            ),
            UpdatePhase.Installing => new UpdateCardModel(
                "downloading",
                status.NewVersion is { } target
                && string.Equals(target, status.RollbackVersion, StringComparison.Ordinal)
                    ? T(L.RollingBack(version: target))
                    : T(L.Installing),
                next,
                true,
                status.Percent,
                T(L.InstallingBtn),
                false,
                false,
                false
            ),
            UpdatePhase.Updated => new UpdateCardModel(
                "verified",
                T(L.Updated) + " · " + current,
                T(L.UpToDate),
                false,
                0,
                T(L.DoneBtn),
                true,
                false,
                false
            ),
            UpdatePhase.Failed => new UpdateCardModel(
                "error",
                T(L.UpdErrT),
                T(
                    status.Error switch
                    {
                        UpdateError.Offline => L.UpdErrOffline,
                        UpdateError.Damaged => L.UpdErrDamaged,
                        UpdateError.NoSpace => L.UpdErrSpace,
                        _ => L.UpdErrStopped,
                    }
                ),
                false,
                0,
                T(L.Retry),
                true,
                false,
                true
            ),
            _ => new UpdateCardModel(
                "verified",
                T(L.UpToDate) + " · " + current,
                status.LastChecked is { } checkedAt
                    ? T(L.LastCheckAt(date: When(checkedAt)))
                    : T(L.NotCheckedYet),
                false,
                0,
                T(L.CheckNow),
                true,
                false,
                false
            ),
        };
    }

    private BackupsModel Backups() =>
        new(
            _sys.Backups.Folder,
            T(L.BakFormat),
            T(L.BackupNow),
            T(L.Export),
            T(L.Import),
            _busy,
            ReviewCard(),
            new SwitchModel(
                "backup",
                T(L.RAuto),
                T(L.AutoBackupD),
                Settings.Reliability.AutoBackup
            ),
            T(L.BackupHist),
            [
                .. _backups.Select(backup =>
                {
                    var armed = IsArmed(nameof(RestoreBackup), backup.Id.Value);
                    return new BackupRowModel(
                        backup.Id,
                        T(When(backup.CreatedAt)),
                        T(
                            L.BakMeta(
                                name: KindName(backup.Kind),
                                count: backup.Profiles,
                                total: backup.Shortcuts
                            )
                        ),
                        armed ? T(L.ConfirmB) : T(L.RestoreB),
                        armed
                    );
                }),
            ],
            T(L.BackupNone)
        );

    /// <summary>The question of Importar, or the review of a backup being restored (LOG-008); null when closed.</summary>
    private ImportCardModel? ReviewCard()
    {
        var note = _pending.IsEmpty ? string.Empty : T(L.RiskyReviewT);
        var language = Settings.Language;
        ValueList<ReviewRowModel> rows =
        [
            .. _pending.Items.Select(shortcut => new ReviewRowModel(
                shortcut.Id,
                shortcut.Icon.Name,
                shortcut.Name.Get(language, LangCode.Es),
                shortcut.Action switch
                {
                    UrlAction url => Targets.Text(url.Target),
                    AppAction app => Targets.Text(app.Target),
                    MacroAction macro => T(L.StepsN(count: macro.Steps.Count)),
                    _ => string.Empty,
                },
                _confirmed.Contains(shortcut.Id)
            )),
        ];
        if (_restore is { } restore)
        {
            var armed = IsArmed(nameof(RestoreBackup), ReviewTarget(restore.Id));
            var info = _backups.FirstOrDefault(b => b.Id == restore.Id);
            return new ImportCardModel(
                info is null ? T(L.RestoreB) : T(When(info.CreatedAt)),
                info is null
                    ? string.Empty
                    : T(
                        L.BakMeta(
                            name: KindName(info.Kind),
                            count: info.Profiles,
                            total: info.Shortcuts
                        )
                    ),
                string.Empty,
                string.Empty,
                string.Empty,
                armed ? T(L.ConfirmB) : T(L.RestoreB),
                T(L.ImpReplaceD),
                armed,
                false,
                note,
                rows
            );
        }

        if (_pick is not { } pick)
        {
            return null;
        }

        var replaceArmed = IsArmed(nameof(ReplaceOnImport), pick.Version);
        return new ImportCardModel(
            T(L.ImpT),
            T(L.ImpSummary(version: pick.Version, count: pick.Profiles, total: pick.Shortcuts)),
            pick.UnavailableTexts > 0
                ? T(L.ImpTextsLost(count: pick.UnavailableTexts))
                : string.Empty,
            T(L.ImpMerge),
            T(L.ImpMergeD),
            replaceArmed ? T(L.ConfirmB) : T(L.ImpReplace),
            T(L.ImpReplaceD),
            replaceArmed,
            true,
            note,
            rows
        );
    }

    private StartModel Start()
    {
        var elevated = _sys.Elevation.IsElevated;
        var canUninstall = _sys.Uninstall.IsAvailable;
        var uninstallArmed = IsArmed(ISystemUninstall.Operation, UninstallTarget(_deleteData));
        return new StartModel(
            new SwitchModel("power_settings_new", T(L.RStart), T(L.RStartD), _startEnabled),
            new AdminRowModel(
                T(L.ReopenAdmin),
                elevated ? T(L.AdminActive) : T(L.ReopenAdminD),
                T(L.ReopenBtn),
                !elevated && !_elevating
            ),
            T(L.RCrash),
            T(L.CrashRecoveryD),
            T(L.AlwaysOn),
            new UninstallModel(
                T(L.UninstallT),
                canUninstall ? T(L.UninstallD) : T(L.UninstallNotInstalled),
                new SwitchModel(
                    "delete_forever",
                    T(L.UninstallWipe),
                    T(L.UninstallWipeD),
                    _deleteData
                ),
                uninstallArmed ? T(L.ConfirmB) : T(L.UninstallBtn),
                uninstallArmed,
                canUninstall && !_busy
            )
        );
    }

    /// <summary>«Hoy, 09:12», «Ayer, 18:40» or «22 sep, 16:27», in the local time of the person.</summary>
    private Message When(DateTimeOffset at)
    {
        var zone = _s.Time.LocalTimeZone;
        var local = TimeZoneInfo.ConvertTime(at, zone);
        var today = TimeZoneInfo.ConvertTime(_s.Time.GetUtcNow(), zone).Date;
        var time = local.ToString("HH:mm", Culture);
        if (local.Date == today)
        {
            return L.WhenToday(time: time);
        }

        if (
            DateOnly.FromDateTime(today).DayNumber - DateOnly.FromDateTime(local.Date).DayNumber
            == 1
        )
        {
            return L.WhenYesterday(time: time);
        }

        return L.WhenDate(
            date: local
                .ToString("d MMM", Culture)
                .Replace(".", string.Empty, StringComparison.Ordinal),
            time: time
        );
    }

    private static Message KindName(BackupKind kind) =>
        kind switch
        {
            BackupKind.Auto => L.BakAuto,
            BackupKind.Manual => L.BakManual,
            BackupKind.PreUpdate => L.BakPreUpdate,
            BackupKind.PreMigrate => L.BakPreMigrate,
            _ => L.BakPreChange,
        };
}
