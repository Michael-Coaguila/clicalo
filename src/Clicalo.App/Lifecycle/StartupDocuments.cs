using System.IO;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Content;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The document the start hands to the store (blueprint §3.1 step 1, §6.5):
/// <list type="number">
/// <item>the document read by the recovery chain;</item>
/// <item>on a new installation (no <c>clicalo.json</c> and nothing to recover), the seed of <c>content\seed.json</c>
/// in the language of Windows, written at once;</item>
/// <item>the usage of <c>usage.json</c>, merged and purged (FRE-002).</item>
/// </list>
/// Clícalo never reads the files of Macro Quick Access (ADR-0020). A document of the start that could not be written
/// is reported with <see cref="StartupLoad.SavePending"/>, so the autosave writes it at once and retries (DAT-002). It
/// runs before the Persistence consumer starts, so its writes never meet the autosave's. Logs codes and counts only,
/// never names, keys or paths (LOG-001).
/// </summary>
internal sealed partial class StartupDocuments(
    IDocumentRepository documents,
    IUsageRepository usage,
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
    /// <param name="cancellationToken">Cancels the start.</param>
    public async Task<StartupLoad> LoadAsync(
        string? contentFolder,
        LangCode? language,
        CancellationToken cancellationToken
    )
    {
        var load = await documents.LoadAsync(cancellationToken).ConfigureAwait(false);
        var savePending = false;
        if (load.Outcome == DocumentLoadOutcome.FirstRun)
        {
            var document = NewInstallation(load.Document, contentFolder, language);
            savePending = !await SaveAsync(document, cancellationToken).ConfigureAwait(false);
            load = load with { Document = document };
        }

        var history = await usage
            .LoadAsync(load.Document.Frequents.UsageEpoch, cancellationToken)
            .ConfigureAwait(false);
        load = load with
        {
            Document = StartupDocument.WithUsage(load.Document, history, time.GetUtcNow()),
        };
        return new StartupLoad(load, savePending);
    }

    private UserDocument NewInstallation(
        UserDocument minimal,
        string? contentFolder,
        LangCode? language
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

        return seed;
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
}
