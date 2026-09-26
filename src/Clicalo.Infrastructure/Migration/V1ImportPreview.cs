using Clicalo.Domain.Migration.V1;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// A v1 file read and converted, not applied yet (MIG-009: always with preview, backup and undo). The use case keeps
/// <see cref="Original"/> byte for byte with <c>IBackupService.KeepV1OriginalAsync</c> before applying (MIG-004).
/// </summary>
/// <param name="SourcePath">The file chosen or found.</param>
/// <param name="Source">Which kind of file.</param>
/// <param name="Original">The bytes exactly as read (for a zip, the zip itself).</param>
/// <param name="Conversion">The converted document and its report.</param>
public sealed record V1ImportPreview(
    string SourcePath,
    V1SourceKind Source,
    ReadOnlyMemory<byte> Original,
    V1Conversion Conversion
);
