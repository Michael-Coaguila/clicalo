using System.Collections.Immutable;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Migration.V1;
using Clicalo.Domain.Timing;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// The v1 import stage (blueprint §6.6): reads a <c>profiles.json</c>, a language backup or the zip backup (through
/// <see cref="SafeZipReader"/>), then <see cref="V1Reader"/> and the pure <see cref="V1Converter"/>. Writes nothing.
/// Criterion: 210 → 210 shortcuts, no empty token (MIG-003, MIG-004).
/// </summary>
/// <remarks>
/// <see cref="PreviewAsync"/> is the «Import» of MIG-009 (always a preview first). <see cref="MigrateAsync"/> is the
/// first-run migration of MIG-004: it hands the original bytes to <see cref="IBackupService.KeepV1OriginalAsync"/>
/// <b>before</b> converting, so everything stays recoverable from the copy. Neither writes the document: on success the
/// caller applies <see cref="V1ImportPreview.Conversion"/>; on failure nothing is written, Clícalo starts with the
/// example data and the welcome offers «Retry migration» (<see cref="FailureRecovery.Retry"/>). Both are idempotent:
/// the source is only read, and the same file with the same ids gives the same document.
/// </remarks>
public sealed class V1Importer
{
    private const string ProfilesFileName = "profiles.json";
    private const string LanguageBackupPrefix = "profiles.backup.";
    private const string LanguageBackupNote = "_nota";

    private readonly SafeZipReader _zipReader;
    private readonly ILogger<V1Importer> _logger;

    /// <summary>Creates the importer.</summary>
    /// <param name="zipReader">Reads the zip backup.</param>
    /// <param name="logger">Logs codes and counts, never labels or combinations.</param>
    public V1Importer(SafeZipReader zipReader, ILogger<V1Importer> logger)
    {
        ArgumentNullException.ThrowIfNull(zipReader);
        ArgumentNullException.ThrowIfNull(logger);
        _zipReader = zipReader;
        _logger = logger;
    }

    /// <summary>Reads and converts a v1 file without applying it.</summary>
    /// <param name="path">The file.</param>
    /// <param name="context">Ids, defaults, monitors and clock of the conversion.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<Result<V1ImportPreview>> PreviewAsync(
        string path,
        V1ConversionContext context,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(context);
        var original = await ReadOriginalAsync(path, cancellationToken).ConfigureAwait(false);
        return original.TryGetValue(out var bytes)
            ? Convert(path, bytes, context, cancellationToken)
            : Failed(original.Failure);
    }

    /// <summary>
    /// The first-run migration (MIG-004): keeps the original byte for byte with
    /// <see cref="IBackupService.KeepV1OriginalAsync"/> before converting, then converts without applying. If the copy
    /// cannot be kept, nothing is converted.
    /// </summary>
    /// <param name="path">The v1 <c>profiles.json</c> found or chosen.</param>
    /// <param name="context">Ids, defaults, monitors and clock of the conversion.</param>
    /// <param name="backups">Keeps <c>backups\v1-original-&lt;date&gt;.json</c>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public async Task<Result<V1ImportPreview>> MigrateAsync(
        string path,
        V1ConversionContext context,
        IBackupService backups,
        CancellationToken cancellationToken
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(backups);
        var original = await ReadOriginalAsync(path, cancellationToken).ConfigureAwait(false);
        if (!original.TryGetValue(out var bytes))
        {
            return Failed(original.Failure);
        }

        var kept = await backups
            .KeepV1OriginalAsync(bytes, cancellationToken)
            .ConfigureAwait(false);
        if (kept.IsFailure)
        {
            return Failed(kept.Failure);
        }

        V1ImportLog.OriginalKept(_logger, bytes.Length);
        return Convert(path, bytes, context, cancellationToken);
    }

    /// <summary>The kind of a v1 file: a zip by its signature, a language backup by its name or its <c>_nota</c>.</summary>
    /// <param name="path">The file.</param>
    /// <param name="bytes">Its first bytes at least.</param>
    internal static V1SourceKind DetectKind(string path, ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith("PK\u0003\u0004"u8) || bytes.StartsWith("PK\u0005\u0006"u8))
        {
            return V1SourceKind.Zip;
        }

        return Path.GetFileName(path)
            .StartsWith(LanguageBackupPrefix, StringComparison.OrdinalIgnoreCase)
            ? V1SourceKind.LanguageBackup
            : V1SourceKind.ProfilesJson;
    }

    private static async Task<Result<ReadOnlyMemory<byte>>> ReadOriginalAsync(
        string path,
        CancellationToken cancellationToken
    )
    {
        var limit = Math.Max(Timings.Import.ZipMaxTotalBytes, Timings.Import.V1MaxBytes);
        try
        {
            var stream = File.OpenRead(path);
            await using (stream.ConfigureAwait(false))
            {
                if (stream.Length > limit)
                {
                    return Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.TooLarge);
                }

                var bytes = new byte[stream.Length];
                await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
                return Results.Ok<ReadOnlyMemory<byte>>(bytes);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Missing, locked, shrinking or forbidden: the user can pick the file again.
            return Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.Unreadable);
        }
    }

    private static Result<ReadOnlyMemory<byte>> FindProfiles(ImmutableArray<SafeZipEntry> entries)
    {
        SafeZipEntry? best = null;
        foreach (var entry in entries)
        {
            var name = entry.Name;
            var fileName = name[(name.LastIndexOf('/') + 1)..];
            if (
                string.Equals(fileName, ProfilesFileName, StringComparison.OrdinalIgnoreCase)
                && (best is null || name.Length < best.Name.Length)
            )
            {
                best = entry;
            }
        }

        return best is null
            ? Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.ZipWithoutProfiles)
            : Results.Ok(best.Content);
    }

    private Result<V1ImportPreview> Convert(
        string path,
        ReadOnlyMemory<byte> original,
        V1ConversionContext context,
        CancellationToken cancellationToken
    )
    {
        var kind = DetectKind(path, original.Span);
        var json = original;
        if (kind == V1SourceKind.Zip)
        {
            var array = MemoryMarshal.TryGetArray(original, out var segment)
                ? segment
                : new ArraySegment<byte>(original.ToArray());
            using var stream = new MemoryStream(
                array.Array!,
                array.Offset,
                array.Count,
                writable: false
            );
            var entries = _zipReader.ReadJsonEntries(stream, cancellationToken);
            if (!entries.TryGetValue(out var read))
            {
                return Failed(entries.Failure);
            }

            var profiles = FindProfiles(read);
            if (!profiles.TryGetValue(out json))
            {
                return Failed(profiles.Failure);
            }
        }

        var document = V1Reader.Read(json.Span);
        if (!document.TryGetValue(out var v1))
        {
            return Failed(document.Failure);
        }

        if (
            kind == V1SourceKind.ProfilesJson
            && v1.UnknownKeys.Contains(LanguageBackupNote, StringComparer.Ordinal)
        )
        {
            kind = V1SourceKind.LanguageBackup;
        }

        var conversion = V1Converter.Convert(v1, context);
        if (!conversion.TryGetValue(out var converted))
        {
            return Failed(conversion.Failure);
        }

        V1ImportLog.Converted(
            _logger,
            kind,
            converted.Report.Input.Profiles,
            converted.Report.Input.Buttons,
            converted.Report.Notes.Count
        );
        return Results.Ok(new V1ImportPreview(path, kind, original, converted));
    }

    private Result<V1ImportPreview> Failed(Failure failure)
    {
        V1ImportLog.Failed(_logger, failure.Code);
        return Results.Fail<V1ImportPreview>(failure);
    }
}
