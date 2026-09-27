using System.IO;
using Clicalo.App.Interop;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Content;
using Clicalo.Infrastructure.Migration;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The document the start hands to the store (blueprint §3.1 step 1, §6.5, §6.6):
/// <list type="number">
/// <item>the document read by the recovery chain;</item>
/// <item>on a new installation (no <c>clicalo.json</c> and nothing to recover), the seed of <c>content\seed.json</c>
/// in the language of Windows, or the v1 file of <c>--migrate-v1</c> converted over it (EC-MIG-01: the original is
/// kept byte for byte first, MIG-004), written at once so the migration never repeats; a migration that fails writes
/// nothing, so it can be tried again;</item>
/// <item>the usage of <c>usage.json</c>, merged and purged (FRE-002).</item>
/// </list>
/// Logs codes and counts only, never names, keys or paths (LOG-001).
/// </summary>
internal sealed partial class StartupDocuments(
    IDocumentRepository documents,
    IUsageRepository usage,
    IBackupService backups,
    V1Importer importer,
    IIdGenerator ids,
    TimeProvider time,
    ILogger<StartupDocuments> logger
)
{
    /// <summary>Loads the document of this start.</summary>
    /// <param name="contentFolder">The folder with <c>seed.json</c>, or <see langword="null"/>.</param>
    /// <param name="language">
    /// The interface language of a new installation (Windows', when Clícalo has it); <see langword="null"/> keeps the
    /// default.
    /// </param>
    /// <param name="migrateV1">The v1 file to migrate on a new installation, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task<DocumentLoad> LoadAsync(
        string? contentFolder,
        LangCode? language,
        string? migrateV1,
        CancellationToken cancellationToken
    )
    {
        var load = await documents.LoadAsync(cancellationToken).ConfigureAwait(false);
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
                var saved = await documents
                    .SaveAsync(document, cancellationToken)
                    .ConfigureAwait(false);
                if (saved.IsFailure)
                {
                    // Still usable: the autosave writes it with the first change and keeps retrying (DAT-002).
                    LogFirstSaveFailed(logger, saved.Failure.Code);
                }
            }

            load = load with { Document = document };
        }

        var history = await usage
            .LoadAsync(load.Document.Frequents.UsageEpoch, cancellationToken)
            .ConfigureAwait(false);
        return load with
        {
            Document = StartupDocument.WithUsage(load.Document, history, time.GetUtcNow()),
        };
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

        var context = new V1ConversionContext(
            ids,
            seed,
            [.. MonitorLayout.Current()],
            time.GetUtcNow()
        );
        var migrated = await importer
            .MigrateAsync(migrateV1, context, backups, cancellationToken)
            .ConfigureAwait(false);
        if (!migrated.TryGetValue(out var preview))
        {
            // MIG-004: the example data, and the original untouched; the report and «Retry» come with the welcome.
            LogMigrationFailed(logger, migrated.Failure.Code);
            return (seed, false);
        }

        var report = preview.Conversion.Report;
        LogMigrated(
            logger,
            report.Input.Profiles,
            report.Input.Buttons,
            report.Output.Profiles,
            report.Output.Buttons,
            report.Notes.Count
        );
        return (preview.Conversion.Document, true);
    }

    private static UserDocument? ReadSeed(
        string? contentFolder,
        Domain.Settings.UserSettings settings
    )
    {
        if (contentFolder is null)
        {
            return null;
        }

        var path = Path.Combine(contentFolder, SeedDocument.FileName);
        try
        {
            return File.Exists(path) ? SeedDocument.Read(File.ReadAllBytes(path), settings) : null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

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
}
