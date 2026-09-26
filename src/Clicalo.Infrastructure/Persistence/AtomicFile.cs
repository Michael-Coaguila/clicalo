using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Ports;
using Clicalo.Domain.Errors;
using Microsoft.Extensions.Logging;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The only file writer of the product besides the log sink (blueprint §4.4): <c>*.tmp</c> with
/// <c>FILE_FLAG_WRITE_THROUGH</c> and <c>FlushFileBuffers</c>, then <c>ReplaceFileW(target, tmp, target.prev)</c>, or
/// <c>MoveFileEx(MOVEFILE_WRITE_THROUGH)</c> the first time. <c>SHARING_VIOLATION</c>, <c>LOCK_VIOLATION</c> and a
/// transient <c>ACCESS_DENIED</c> are retried at <c>Timings.Persistence.WriteRetryBackoff</c> (S11: Defender, the
/// indexer, a locking monitor and sync clients).
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the persistence package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class AtomicFile : IAtomicFileWriter
{
    /// <summary>Creates the writer.</summary>
    /// <param name="time">Clock of the retries.</param>
    /// <param name="logger">Logs codes and attempts, never content or user paths.</param>
    public AtomicFile(TimeProvider time, ILogger<AtomicFile> logger) =>
        throw new NotImplementedException();

    /// <inheritdoc />
    public Task<Result<AtomicWriteReceipt>> WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    ) => throw new NotImplementedException();
}
