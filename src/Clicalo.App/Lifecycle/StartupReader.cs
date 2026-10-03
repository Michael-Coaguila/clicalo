using System.Collections.Immutable;
using System.IO;
using Clicalo.App.Composition;
using Clicalo.App.Localization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Platform.Core.Guardian;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// Everything the start reads from disk before the panel exists (blueprint §3.1 steps 0 and 1): the crash Sentinel
/// reported goes to its journal (ADR-0018), the document is read or created (<see cref="StartupDocuments"/>)
/// and the language files are loaded. All of it runs on the thread pool, never on the thread that asks: that is the UI
/// thread, which never does I/O (§3.2), and a slow disk, OneDrive or an antivirus scan must not hold the first frame
/// back (NFR-001).
/// </summary>
internal sealed partial class StartupReader(
    StartupDocuments documents,
    IAtomicFileWriter writer,
    DataLocations locations,
    ILogger<StartupReader> logger
)
{
    /// <summary>Reads the start off the calling thread.</summary>
    /// <param name="request">What the command line and Windows ask for.</param>
    /// <param name="cancellationToken">Cancels the start.</param>
    public Task<StartupRead> ReadAsync(StartupRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.Run(() => ReadCoreAsync(request, cancellationToken), cancellationToken);
    }

    private async Task<StartupRead> ReadCoreAsync(
        StartupRequest request,
        CancellationToken cancellationToken
    )
    {
        if (request.AfterCrash is { } crash)
        {
            await RecordCrashAsync(crash, cancellationToken).ConfigureAwait(false);
        }

        var i18n =
            LanguageFiles.Find(request.BaseDirectory)
            ?? throw new FileNotFoundException("i18n was not found next to Clicalo.exe.");
        var windows = request.WindowsLanguage;
        var load = await documents
            .LoadAsync(
                ContentFiles.Find(request.BaseDirectory),
                LanguageFiles.Has(i18n, windows) ? new LangCode(windows) : null,
                cancellationToken
            )
            .ConfigureAwait(false);
        var localization = LanguageFiles.Load(
            i18n,
            load.Load.Document.Settings.Language.Value,
            out var skipped
        );
        foreach (var language in skipped)
        {
            LogLanguageSkipped(logger, language);
        }

        return new StartupRead(load, localization);
    }

    /// <summary>Appends the crash of <c>--after-crash</c> to the journal Sentinel reads (ADR-0018).</summary>
    private async Task RecordCrashAsync(DateTimeOffset crash, CancellationToken cancellationToken)
    {
        var path = AppDataLocations.CrashJournal(locations);
        ImmutableArray<DateTimeOffset> previous = [];
        try
        {
            if (File.Exists(path))
            {
                previous = CrashJournal.Parse(
                    await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false)
                );
            }
        }
        catch (IOException)
        {
            // An unreadable journal starts again: at worst one crash loop is detected later.
        }

        var written = await writer
            .WriteAsync(path, CrashJournal.Append(previous, crash), cancellationToken)
            .ConfigureAwait(false);
        if (written.IsFailure)
        {
            LogCrashJournalFailed(logger, written.Failure.Code);
        }

        LogAfterCrash(logger, previous.Length + 1);
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "startup.after_crash {Count} crashes in the journal"
    )]
    private static partial void LogAfterCrash(ILogger logger, int count);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "startup.crash_journal_failed ({Code})"
    )]
    private static partial void LogCrashJournalFailed(ILogger logger, string code);

    [LoggerMessage(
        EventId = 3,
        Level = LogLevel.Warning,
        Message = "startup.language_skipped {Language}: its entry or its texts could not be read"
    )]
    private static partial void LogLanguageSkipped(ILogger logger, string language);
}
