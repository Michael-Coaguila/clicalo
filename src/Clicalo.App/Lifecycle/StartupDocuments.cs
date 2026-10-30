using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Application.UseCases;
using Clicalo.Domain.Document;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Templates;
using Clicalo.Infrastructure.Catalogs;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The document the start hands to the store (blueprint §3.1 step 1, §6.5):
/// <list type="number">
/// <item>the document read by the recovery chain;</item>
/// <item>on a new installation (no <c>clicalo.json</c> and nothing to recover), an empty General and Siempre visible in
/// the language of Windows, written at once: the welcome, which opens while the document has not finished it, installs
/// the starter kit the person marks (user decision D2; [Omitir] installs «Basics» only, BIE-003);</item>
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

    /// <summary>
    /// The document of the starter kit with nothing marked, which the welcome fills (user decision D2, BIE-003), or
    /// <see langword="null"/> when the content cannot be read (the caller keeps General alone).
    /// </summary>
    private UserDocument? ReadSeed(string? contentFolder, Domain.Settings.UserSettings settings) =>
        contentFolder is not null
        && StarterContentFiles.Load(contentFolder) is { } content
        && FirstDocument
            .Create(content, StarterSelection.Empty, settings, ids)
            .TryGetValue(out var document)
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
}
