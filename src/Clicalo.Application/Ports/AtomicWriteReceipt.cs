namespace Clicalo.Application.Ports;

/// <summary>What an atomic write did.</summary>
/// <param name="Path">The file written.</param>
/// <param name="Attempts">Attempts, retries of transient errors included.</param>
/// <param name="ReplacedExisting">Whether it replaced a file (<c>ReplaceFileW</c>, keeping <c>.prev</c>) or created it.</param>
public sealed record AtomicWriteReceipt(string Path, int Attempts, bool ReplacedExisting);
