using System.IO;
using System.Text;
using Clicalo.App.Interop;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Document;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Catalogs;
using Clicalo.Infrastructure.Migration;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The document the start hands to the store (blueprint §3.1 step 1, §6.5, §6.6):
/// <list type="number">
/// <item>the document read by the recovery chain;</item>
/// <item>on a new installation (no <c>clicalo.json</c> and nothing to recover), the starter kit of <c>content</c> with
/// the options it marks by default («Basics» only, user decision D2, until the welcome of M4 lets the user choose), in
/// the language of Windows, or the v1 file of <c>--migrate-v1</c> converted over it (EC-MIG-01: the original is
/// kept byte for byte first, MIG-004), written at once so the migration never repeats;</item>
/// <item>a migration that fails writes nothing and leaves the mark <c>migration-v1.pending</c> in the local data
/// folder, because the first change will write the seed: while the mark exists, a start with <c>--migrate-v1</c> tries
/// the migration again over the seed, after a <c>pre-migrate</c> copy of the document it replaces (until the welcome
/// of M3 offers «Retry migration»);</item>
/// <item>the usage of <c>usage.json</c>, merged and purged (FRE-002).</item>
/// </list>
/// A document of the start that could not be written is reported with <see cref="StartupLoad.SavePending"/>, so the
/// autosave writes it at once and retries (DAT-002). It runs before the Persistence consumer starts, so its writes
/// never meet the autosave's. Logs codes and counts only, never names, keys or paths (LOG-001).
/// </summary>
internal sealed partial class StartupDocuments(
    IDocumentRepository documents,
    IUsageRepository usage,
    IBackupService backups,
    V1Importer importer,
    IAtomicFileWriter writer,
    string pendingMigration,
    IIdGenerator ids,
    TimeProvider time,
    ILogger<StartupDocuments> logger
)
{
    /// <summary>Loads the document of this start.</summary>
    /// <param name="contentFolder">The folder with the starter content, or <see langword="null"/>.</param>
    /// <param name="language">
    /// The interface language of a new installation (Windows', when Clícalo has it); <see langword="null"/> keeps the
    /// default.
    /// </param>
    /// <param name="migrateV1">The v1 file to migrate on a new installation, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task<StartupLoad> LoadAsync(
        string? contentFolder,
        LangCode? language,
        string? migrateV1,
        CancellationToken cancellationToken
    )
    {
        var load = await documents.LoadAsync(cancellationToken).ConfigureAwait(false);
        var savePending = false;
        if (load.Outcome == DocumentLoadOutcome.FirstRun)
        {
            var (document, save) = await NewInstallationAsync(
                    load.Document,
                    contentFolder,
                    language,
                    migrateV1,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (save)
            {
                savePending = !await SaveAsync(document, cancellationToken).ConfigureAwait(false);
            }

            load = load with { Document = document };
        }

        var history = await usage
            .LoadAsync(load.Document.Frequents.UsageEpoch, cancellationToken)
            .ConfigureAwait(false);
        load = load with
        {
            Document = StartupDocument.WithUsage(load.Document, history, time.GetUtcNow()),
        };
        if (
            load.Outcome != DocumentLoadOutcome.FirstRun
            && migrateV1 is not null
            && !load.IsReadOnly
            && File.Exists(pendingMigration)
        )
        {
            var retried = await RetryMigrationAsync(
                    load.Document,
                    contentFolder,
                    migrateV1,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (retried is not null)
            {
                savePending = !await SaveAsync(retried, cancellationToken).ConfigureAwait(false);
                load = load with { Document = retried };
            }
        }

        return new StartupLoad(load, savePending);
    }

    private async Task<(UserDocument Document, bool Save)> NewInstallationAsync(
        UserDocument minimal,
        string? contentFolder,
        LangCode? language,
        string? migrateV1,
        CancellationToken cancellationToken
    )
    {
        var settings = language is { } chosen
            ? minimal.Settings with
            {
                Language = chosen,
            }
            : minimal.Settings;
        var seed = ReadSeed(contentFolder, settings);
        if (seed is null)
        {
            LogSeedUnavailable(logger);
            seed = minimal with { Settings = settings };
        }

        if (migrateV1 is null)
        {
            return (seed, true);
        }

        var migrated = await MigrateAsync(seed, migrateV1, cancellationToken).ConfigureAwait(false);

        // MIG-004: on failure the example data, and the original untouched; the report and «Retry» come with the welcome.
        return migrated is null ? (seed, false) : (migrated, true);
    }

    /// <summary>
    /// A start with <c>--migrate-v1</c> after a failed migration: the document of today (the seed, and whatever was
    /// changed since) is kept as <c>pre-migrate</c> first, and is only replaced when that copy exists (REG-08).
    /// </summary>
    private async Task<UserDocument?> RetryMigrationAsync(
        UserDocument current,
        string? contentFolder,
        string migrateV1,
        CancellationToken cancellationToken
    )
    {
        var kept = await backups
            .CreateAsync(current, BackupKind.PreMigrate, cancellationToken)
            .ConfigureAwait(false);
        if (kept.IsFailure)
        {
            LogRetryNotCopied(logger, kept.Failure.Code);
            return null;
        }

        LogRetryingMigration(logger);
        var baseline = ReadSeed(contentFolder, current.Settings) ?? current;
        return await MigrateAsync(baseline, migrateV1, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Converts the v1 file over <paramref name="baseline"/>; a failure leaves the mark that allows a retry, a success
    /// removes it.
    /// </summary>
    private async Task<UserDocument?> MigrateAsync(
        UserDocument baseline,
        string migrateV1,
        CancellationToken cancellationToken
    )
    {
        var context = new V1ConversionContext(
            ids,
            baseline,
            [.. MonitorLayout.Current()],
            time.GetUtcNow()
        );
        var migrated = await importer
            .MigrateAsync(migrateV1, context, backups, cancellationToken)
            .ConfigureAwait(false);
        if (!migrated.TryGetValue(out var preview))
        {
            LogMigrationFailed(logger, migrated.Failure.Code);
            await MarkPendingAsync(migrated.Failure.Code, cancellationToken).ConfigureAwait(false);
            return null;
        }

        ClearPending();
        var report = preview.Conversion.Report;
        LogMigrated(
            logger,
            report.Input.Profiles,
            report.Input.Buttons,
            report.Output.Profiles,
            report.Output.Buttons,
            report.Notes.Count
        );
        return preview.Conversion.Document;
    }

    /// <summary>Writes the document of the start now; <see langword="false"/> when it could not be written.</summary>
    private async Task<bool> SaveAsync(UserDocument document, CancellationToken cancellationToken)
    {
        var saved = await documents.SaveAsync(document, cancellationToken).ConfigureAwait(false);
        if (saved.IsSuccess)
        {
            return true;
        }

        // Still usable: the autosave writes it at once and keeps retrying (DAT-002, StartupLoad.SavePending).
        LogFirstSaveFailed(logger, saved.Failure.Code);
        return false;
    }

    private async Task MarkPendingAsync(string code, CancellationToken cancellationToken)
    {
        var marked = await writer
            .WriteAsync(pendingMigration, Encoding.UTF8.GetBytes(code), cancellationToken)
            .ConfigureAwait(false);
        if (marked.IsFailure)
        {
            LogPendingMarkFailed(logger, marked.Failure.Code);
        }
    }

    private void ClearPending()
    {
        try
        {
            File.Delete(pendingMigration);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // At worst the next start with --migrate-v1 migrates again, after its own pre-migrate copy.
            var failure = ex.GetType().Name;
            LogPendingMarkFailed(logger, failure);
        }
    }

    /// <summary>
    /// The document of the starter kit with the options it marks by default (user decision D2), or
    /// <see langword="null"/> when the content cannot be read (the caller keeps General alone).
    /// </summary>
    private UserDocument? ReadSeed(string? contentFolder, Domain.Settings.UserSettings settings) =>
        contentFolder is not null
        && StarterContentFiles.Load(contentFolder) is { } content
        && FirstDocument.CreateDefault(content, settings, ids).TryGetValue(out var document)
            ? document
            : null;

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "startup.seed_unavailable")]
    private static partial void LogSeedUnavailable(ILogger logger);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "startup.first_save_failed ({Code})"
    )]
    private static partial void LogFirstSaveFailed(ILogger logger, string code);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning, Message = "migration.v1.failed ({Code})")]
    private static partial void LogMigrationFailed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Information,
        Message = "migration.v1.done {InputProfiles} profiles, {InputButtons} buttons → {OutputProfiles} profiles, {OutputButtons} shortcuts, {Notes} report lines"
    )]
    private static partial void LogMigrated(
        ILogger logger,
        int inputProfiles,
        int inputButtons,
        int outputProfiles,
        int outputButtons,
        int notes
    );

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "migration.v1.retry: a previous migration failed; the current document was kept as pre-migrate"
    )]
    private static partial void LogRetryingMigration(ILogger logger);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Warning,
        Message = "migration.v1.retry_skipped: the pre-migrate copy could not be written ({Code})"
    )]
    private static partial void LogRetryNotCopied(ILogger logger, string code);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Warning,
        Message = "migration.v1.pending_mark_failed ({Code})"
    )]
    private static partial void LogPendingMarkFailed(ILogger logger, string code);
}
