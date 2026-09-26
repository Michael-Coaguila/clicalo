namespace Clicalo.Infrastructure.Migration;

/// <summary>A JSON entry read from an untrusted zip.</summary>
/// <param name="Name">Entry name, already checked (no <c>..</c>, no absolute path, no drive).</param>
/// <param name="Content">The uncompressed bytes.</param>
public sealed record SafeZipEntry(string Name, ReadOnlyMemory<byte> Content);
