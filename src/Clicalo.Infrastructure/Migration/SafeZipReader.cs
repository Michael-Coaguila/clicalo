using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Immutable;
using System.IO.Compression;
using Clicalo.Domain.Errors;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// Reads the JSON entries of an untrusted <c>.zip</c> (blueprint §6.6, MIG-009): streaming with counters that abort as
/// soon as a limit is passed, no <c>..</c>, absolute or drive paths, no nested zips followed. Every imported content is
/// untrusted and never executed (AGENTS.md).
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>The stream is copied once with a counter: an archive larger than <see cref="SafeZipLimits.MaxTotalBytes"/> is
/// refused before anything is parsed.</item>
/// <item>The entry count of the end-of-central-directory record is checked before the directory is loaded, and again
/// on the loaded directory.</item>
/// <item>Every name is checked, even of entries that are never opened; one unsafe name refuses the whole archive.</item>
/// <item>Only <c>.json</c> entries are decompressed. Each one is counted while it is read, against its own size limit,
/// the total limit and the compression ratio measured on the compressed bytes it really consumed; the sizes written
/// in the headers are never trusted, and a checksum mismatch (a header that understated the size) is damage.</item>
/// </list>
/// Nothing is written to disk and nothing is extracted.
/// </remarks>
public sealed class SafeZipReader
{
    private const int BufferSize = 81920;
    private const uint EndOfCentralDirectorySignature = 0x06054B50;
    private const int EndOfCentralDirectorySize = 22;
    private const int EntryCountOffset = 10;
    private const string JsonExtension = ".json";

    private readonly SafeZipLimits _limits;

    /// <summary>Creates a reader.</summary>
    /// <param name="limits">The limits.</param>
    public SafeZipReader(SafeZipLimits limits)
    {
        ArgumentNullException.ThrowIfNull(limits);
        _limits = limits;
    }

    /// <summary>Reads every <c>.json</c> entry, or fails at the first broken limit or unsafe path.</summary>
    /// <param name="zip">The zip stream, read once.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    public Result<ImmutableArray<SafeZipEntry>> ReadJsonEntries(
        Stream zip,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(zip);
        var archive = CopyBounded(zip, cancellationToken);
        if (archive is null)
        {
            return Fail(V1ImportFailures.ZipTooLarge);
        }

        var declared = DeclaredEntryCount(archive);
        if (declared is null)
        {
            return Fail(V1ImportFailures.ZipDamaged);
        }

        if (declared > _limits.MaxEntries)
        {
            return Fail(V1ImportFailures.ZipTooManyEntries);
        }

        try
        {
            using var counting = new CountingReadStream(new MemoryStream(archive, writable: false));
            using var zipArchive = new ZipArchive(counting, ZipArchiveMode.Read, leaveOpen: false);
            return ReadEntries(zipArchive, counting, cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Fail(V1ImportFailures.ZipDamaged);
        }
        catch (NotSupportedException)
        {
            // An unsupported compression method or an encrypted entry.
            return Fail(V1ImportFailures.ZipDamaged);
        }
        catch (IOException)
        {
            return Fail(V1ImportFailures.ZipDamaged);
        }
    }

    /// <summary>
    /// Whether an entry name is safe: not empty, no control characters, no drive or stream (<c>:</c>), not absolute,
    /// no <c>..</c> segment, with either slash.
    /// </summary>
    /// <param name="name">The entry name as stored.</param>
    internal static bool IsSafeName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        foreach (var c in name)
        {
            if (char.IsControl(c) || c == ':')
            {
                return false;
            }
        }

        var normalized = name.Replace('\\', '/');
        if (normalized[0] == '/')
        {
            return false;
        }

        foreach (var segment in normalized.Split('/'))
        {
            if (string.Equals(segment, "..", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static Result<ImmutableArray<SafeZipEntry>> Fail(Failure failure) =>
        Results.Fail<ImmutableArray<SafeZipEntry>>(failure);

    /// <summary>The entry count written in the end-of-central-directory record, or <see langword="null"/>.</summary>
    private static int? DeclaredEntryCount(ReadOnlySpan<byte> archive)
    {
        var lowest = Math.Max(0, archive.Length - EndOfCentralDirectorySize - ushort.MaxValue);
        for (var i = archive.Length - EndOfCentralDirectorySize; i >= lowest; i--)
        {
            if (
                BinaryPrimitives.ReadUInt32LittleEndian(archive[i..])
                == EndOfCentralDirectorySignature
            )
            {
                // 0xFFFF means ZIP64, which a v1 backup never needs: it counts as too many entries.
                return BinaryPrimitives.ReadUInt16LittleEndian(archive[(i + EntryCountOffset)..]);
            }
        }

        return null;
    }

    private byte[]? CopyBounded(Stream zip, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            int read;
            while ((read = zip.Read(buffer, 0, BufferSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (copy.Length + read > _limits.MaxTotalBytes)
                {
                    return null;
                }

                copy.Write(buffer, 0, read);
            }

            return copy.ToArray();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private Result<ImmutableArray<SafeZipEntry>> ReadEntries(
        ZipArchive archive,
        CountingReadStream counting,
        CancellationToken cancellationToken
    )
    {
        var entries = archive.Entries;
        if (entries.Count > _limits.MaxEntries)
        {
            return Fail(V1ImportFailures.ZipTooManyEntries);
        }

        foreach (var entry in entries)
        {
            if (!IsSafeName(entry.FullName))
            {
                return Fail(V1ImportFailures.ZipUnsafePath);
            }
        }

        var result = ImmutableArray.CreateBuilder<SafeZipEntry>();
        long total = 0;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = entry.FullName.Replace('\\', '/');

            // Directories, nested zips and any other file are never opened.
            if (
                name.EndsWith('/')
                || !name.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase)
            )
            {
                continue;
            }

            var content = ReadEntry(entry, counting, ref total, cancellationToken);
            if (!content.TryGetValue(out var bytes))
            {
                return Fail(content.Failure);
            }

            result.Add(new SafeZipEntry(name, bytes));
        }

        return Results.Ok(result.ToImmutable());
    }

    private Result<ReadOnlyMemory<byte>> ReadEntry(
        ZipArchiveEntry entry,
        CountingReadStream counting,
        ref long total,
        CancellationToken cancellationToken
    )
    {
        counting.ResetCount();
        using var stream = entry.Open();
        using var output = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            long count = 0;
            uint crc = 0;
            int read;
            while ((read = stream.Read(buffer, 0, BufferSize)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                count += read;
                total += read;
                if (count > _limits.MaxJsonBytes)
                {
                    return Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.ZipEntryTooLarge);
                }

                if (total > _limits.MaxTotalBytes)
                {
                    return Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.ZipTooLarge);
                }

                // The ratio against the compressed bytes this entry has really consumed so far.
                if (count > _limits.MaxCompressionRatio * Math.Max(1, counting.BytesRead))
                {
                    return Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.ZipRatio);
                }

                crc = Crc32.Append(crc, buffer.AsSpan(0, read));
                output.Write(buffer, 0, read);
            }

            // The decompressor stops at the declared size: a lying header shows up as a wrong checksum.
            return crc == entry.Crc32
                ? Results.Ok<ReadOnlyMemory<byte>>(output.ToArray())
                : Results.Fail<ReadOnlyMemory<byte>>(V1ImportFailures.ZipDamaged);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
