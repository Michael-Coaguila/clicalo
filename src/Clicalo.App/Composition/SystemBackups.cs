using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using Clicalo.Application.Persistence;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Timing;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Presentation.ControlCenter.SystemSection;
using Microsoft.Win32;

namespace Clicalo.App.Composition;

/// <summary>
/// The backups of «Sistema › Copias de seguridad» (COP-002 to COP-005) over what already exists: the backup service,
/// and the Persistence consumer, which stays the only writer of <c>backups\</c> (a manual backup is queued and the
/// consumer writes it on a flush). Exportar writes that backup where the person chooses; Importar reads the chosen file
/// as untrusted content (size first, then <see cref="DocumentImportReader"/>). The pickers are the ones of Windows,
/// on the UI thread of the Control Center.
/// </summary>
internal sealed class SystemBackups(
    DataLocations locations,
    IBackupService backups,
    PersistenceScheduler scheduler,
    IAtomicFileWriter writer,
    TimeProvider time
) : ISystemBackups
{
    private const string Filter = "Clícalo (*.json)|*.json";

    /// <inheritdoc />
    public string Folder
    {
        get
        {
            // The user's folder never shows on screen (screen sharing, LOG-001): %APPDATA% stands for it.
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = locations.Backups;
            return appData.Length > 0 && folder.StartsWith(appData, StringComparison.OrdinalIgnoreCase)
                ? "%APPDATA%" + folder[appData.Length..]
                : folder;
        }
    }

    /// <inheritdoc />
    public Task<ImmutableArray<BackupInfo>> ListAsync(CancellationToken cancellationToken) =>
        Task.Run(() => backups.ListAsync(cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<bool> CreateAsync(UserDocument document, CancellationToken cancellationToken)
    {
        var started = time.GetUtcNow();
        backups.SnapshotNow(document, BackupKind.Manual);
        try
        {
            await Task.Run(() => scheduler.FlushAsync(cancellationToken), cancellationToken)
                .ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        return await NewestManualAsync(started, cancellationToken).ConfigureAwait(true) is not null;
    }

    /// <inheritdoc />
    public Task<Result<UserDocument>> ReadAsync(BackupId id, CancellationToken cancellationToken) =>
        Task.Run(() => backups.ReadAsync(id, cancellationToken), cancellationToken);

    /// <inheritdoc />
    public async Task<ExportOutcome> ExportAsync(
        UserDocument document,
        CancellationToken cancellationToken
    )
    {
        var picker = new SaveFileDialog
        {
            Filter = Filter,
            DefaultExt = ".json",
            AddExtension = true,
            OverwritePrompt = true,
            FileName =
                "clicalo-"
                + TimeZoneInfo
                    .ConvertTime(time.GetUtcNow(), time.LocalTimeZone)
                    .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + ".json",
        };
        if (picker.ShowDialog() != true)
        {
            return ExportOutcome.Cancelled;
        }

        var target = picker.FileName;
        var started = time.GetUtcNow();
        if (!await CreateAsync(document, cancellationToken).ConfigureAwait(true))
        {
            return ExportOutcome.Failed;
        }

        var newest = await NewestManualAsync(started, cancellationToken).ConfigureAwait(true);
        if (newest is null || !TryPathOf(newest.Id, out var source))
        {
            return ExportOutcome.Failed;
        }

        try
        {
            var bytes = await File.ReadAllBytesAsync(source, cancellationToken).ConfigureAwait(true);
            var written = await writer.WriteAsync(target, bytes, cancellationToken).ConfigureAwait(true);
            return written.IsSuccess ? ExportOutcome.Done : ExportOutcome.Failed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ExportOutcome.Failed;
        }
    }

    /// <inheritdoc />
    public async Task<Result<ImportPick>?> PickImportAsync(CancellationToken cancellationToken)
    {
        var picker = new OpenFileDialog
        {
            Filter = Filter,
            CheckFileExists = true,
            Multiselect = false,
        };
        if (picker.ShowDialog() != true)
        {
            return null;
        }

        var path = picker.FileName;
        return await Task.Run(() => Read(path), cancellationToken).ConfigureAwait(true);
    }

    /// <summary>Reads <paramref name="path"/> as untrusted content: its size first, then the reader (LOG-006).</summary>
    /// <param name="path">The chosen file.</param>
    internal static Result<ImportPick> Read(string path)
    {
        try
        {
            if (new FileInfo(path).Length > Timings.Import.ShareMaxBytes)
            {
                return DocumentImportReader
                    .Read(new byte[Timings.Import.ShareMaxBytes + 1])
                    .Map(ToPick);
            }

            return DocumentImportReader.Read(File.ReadAllBytes(path)).Map(ToPick);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return DocumentImportReader.Read([]).Map(ToPick);
        }
    }

    private static ImportPick ToPick(ImportedDocument imported) =>
        new(
            imported.Document,
            imported.Profiles,
            imported.Shortcuts,
            imported.WrittenBy,
            imported.UnavailableTexts
        );

    private async Task<BackupInfo?> NewestManualAsync(
        DateTimeOffset since,
        CancellationToken cancellationToken
    )
    {
        var list = await ListAsync(cancellationToken).ConfigureAwait(true);
        return list.FirstOrDefault(b => b.Kind == BackupKind.Manual && b.CreatedAt >= since.AddTicks(-TimeSpan.TicksPerSecond));
    }

    /// <summary>The file of a backup id this app produced (<c>kind/clicalo.….json</c>), never another path.</summary>
    private bool TryPathOf(BackupId id, out string path)
    {
        path = string.Empty;
        var value = id.Value;
        if (
            value.Contains("..", StringComparison.Ordinal)
            || value.Contains(':', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Split('/').Length != 2
        )
        {
            return false;
        }

        path = Path.Combine(locations.Backups, value.Replace('/', Path.DirectorySeparatorChar));
        return true;
    }
}
