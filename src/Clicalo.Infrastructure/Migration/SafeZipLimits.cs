using Clicalo.Domain.Timing;

namespace Clicalo.Infrastructure.Migration;

/// <summary>The limits of <see cref="SafeZipReader"/> (blueprint §6.6, MIG-009, LOG-006).</summary>
/// <param name="MaxTotalBytes">Total uncompressed size.</param>
/// <param name="MaxEntries">Number of entries.</param>
/// <param name="MaxCompressionRatio">Compression ratio per entry, counted while reading, never trusting the header.</param>
/// <param name="MaxJsonBytes">Size of each JSON file.</param>
public sealed record SafeZipLimits(
    long MaxTotalBytes,
    int MaxEntries,
    double MaxCompressionRatio,
    long MaxJsonBytes
)
{
    /// <summary>The limits of <c>data/catalogs/timings.json</c> (<c>Timings.Import.Zip*</c>).</summary>
    public static SafeZipLimits Default { get; } =
        new(
            Timings.Import.ZipMaxTotalBytes,
            Timings.Import.ZipMaxEntries,
            Timings.Import.ZipMaxCompressionRatio,
            Timings.Import.ZipMaxJsonBytes
        );
}
