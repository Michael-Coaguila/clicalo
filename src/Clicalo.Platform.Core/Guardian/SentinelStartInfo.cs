using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The start-up contract of <c>Clicalo.Sentinel.exe</c> (blueprint §3.1, ADR-0004, ADR-0018): the three handles it
/// inherits through <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> (and only those) and the thresholds it needs, since it
/// cannot read <c>timings.json</c>. Written by the main process on the command line and parsed by Sentinel; a public
/// contract between two executables of the same version.
/// </summary>
/// <param name="ParentProcess">The main process, with <c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
/// <param name="Ledger">The ledger section, read-only.</param>
/// <param name="HeartbeatPipe">Sentinel's end of an anonymous pipe; the main process writes to it every <paramref name="HeartbeatInterval"/>.</param>
/// <param name="HeartbeatInterval"><c>Timings.Guardian.PipeHeartbeatInterval</c>.</param>
/// <param name="CrashLoopCount"><c>Timings.App.CrashLoop.Count</c>.</param>
/// <param name="CrashLoopWindow"><c>Timings.App.CrashLoop.Window</c>.</param>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed record SentinelStartInfo(
    nint ParentProcess,
    nint Ledger,
    nint HeartbeatPipe,
    TimeSpan HeartbeatInterval,
    int CrashLoopCount,
    TimeSpan CrashLoopWindow
)
{
    /// <summary>Version of this contract; Sentinel refuses any other.</summary>
    public const int ProtocolVersion = 1;

    /// <summary>Exactly this many handles are inherited: parent, ledger and pipe.</summary>
    public const int InheritedHandleCount = 3;

    /// <summary>
    /// Parses Sentinel's arguments; <see langword="false"/> for anything malformed, another protocol version or a
    /// missing handle (Sentinel then exits with <see cref="SentinelExitCode.InvalidArguments"/>).
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="info">The parsed contract.</param>
    public static bool TryParse(
        ReadOnlySpan<string> arguments,
        [NotNullWhen(true)] out SentinelStartInfo? info
    ) => throw new NotImplementedException();

    /// <summary>The command line arguments, culture-invariant, starting with the protocol version.</summary>
    public ImmutableArray<string> ToArguments() => throw new NotImplementedException();
}
