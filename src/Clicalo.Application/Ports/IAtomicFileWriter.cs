using Clicalo.Domain.Errors;

namespace Clicalo.Application.Ports;

/// <summary>
/// The only way the product writes a data file (blueprint §4.4, §6.5): <c>*.tmp</c> with write-through and
/// <c>FlushFileBuffers</c>, then <c>ReplaceFileW(target, tmp, target.prev)</c> (or <c>MoveFileEx</c> the first time).
/// After a crash the file is the old version whole or the new one whole (DAT-002). Transient errors are retried at
/// <c>Timings.Persistence.WriteRetryBackoff</c>; a persistent one is a <see cref="Failure"/> the user sees.
/// </summary>
public interface IAtomicFileWriter
{
    /// <summary>Replaces <paramref name="path"/> atomically with <paramref name="content"/>.</summary>
    /// <param name="path">Target file.</param>
    /// <param name="content">The whole new content.</param>
    /// <param name="cancellationToken">Cancels between retries, never half-way through a replace.</param>
    Task<Result<AtomicWriteReceipt>> WriteAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken
    );
}
