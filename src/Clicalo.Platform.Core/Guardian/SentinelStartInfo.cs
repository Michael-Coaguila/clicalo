using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The start-up contract of <c>Clicalo.Sentinel.exe</c> (blueprint §3.1, ADR-0004, ADR-0018): the three handles it
/// inherits through <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> (and only those) and the thresholds it needs, since it
/// cannot read <c>timings.json</c>. Written by the main process on the command line and parsed by Sentinel; a public
/// contract between two executables of the same version.
/// </summary>
/// <remarks>
/// <para>
/// The arguments, in this order and culture-invariant: <c>--protocol=2</c>, <c>--parent=0x…</c>,
/// <c>--ledger=0x…</c>, <c>--pipe=0x…</c>, <c>--heartbeat-ms=&lt;n&gt;</c>, <c>--crash-loop=&lt;n&gt;/&lt;ms&gt;</c> and
/// <c>--refused-release-wait-ms=&lt;n&gt;</c>.
/// </para>
/// <para>
/// Protocol 2 (ADR-0018, user decision D3 of 2026-10-03) added the seventh argument: Sentinel retries a release the
/// desktop refuses every <see cref="HeartbeatInterval"/> and relaunches only once it went. A refusal by the secure
/// desktop (a locked session) is retried without limit; any other refusal, for at most
/// <see cref="RefusedReleaseWait"/>. Protocol 1 is refused like any other version.
/// </para>
/// </remarks>
/// <param name="ParentProcess">The main process, with <c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
/// <param name="Ledger">The ledger section, read-only.</param>
/// <param name="HeartbeatPipe">Sentinel's end of an anonymous pipe; the main process writes to it every <paramref name="HeartbeatInterval"/>.</param>
/// <param name="HeartbeatInterval"><c>Timings.Guardian.PipeHeartbeatInterval</c>.</param>
/// <param name="CrashLoopCount"><c>Timings.App.CrashLoop.Count</c>.</param>
/// <param name="CrashLoopWindow"><c>Timings.App.CrashLoop.Window</c>.</param>
/// <param name="RefusedReleaseWait">
/// <c>Timings.Guardian.RefusedReleaseWait</c>: how long Sentinel keeps retrying a release refused for a reason other
/// than the secure desktop before it gives up and relaunches.
/// </param>
public sealed record SentinelStartInfo(
    nint ParentProcess,
    nint Ledger,
    nint HeartbeatPipe,
    TimeSpan HeartbeatInterval,
    int CrashLoopCount,
    TimeSpan CrashLoopWindow,
    TimeSpan RefusedReleaseWait
)
{
    /// <summary>Version of this contract; Sentinel refuses any other.</summary>
    public const int ProtocolVersion = 2;

    /// <summary>Exactly this many handles are inherited: parent, ledger and pipe.</summary>
    public const int InheritedHandleCount = 3;

    private const string Protocol = "--protocol=";
    private const string Parent = "--parent=";
    private const string LedgerPrefix = "--ledger=";
    private const string Pipe = "--pipe=";
    private const string Heartbeat = "--heartbeat-ms=";
    private const string CrashLoop = "--crash-loop=";
    private const string RefusedRelease = "--refused-release-wait-ms=";
    private const int ArgumentCount = 7;

    /// <summary>
    /// Parses Sentinel's arguments; <see langword="false"/> for anything malformed, another protocol version or a
    /// missing handle (Sentinel then exits with <see cref="SentinelExitCode.InvalidArguments"/>).
    /// </summary>
    /// <param name="arguments">The command line arguments.</param>
    /// <param name="info">The parsed contract.</param>
    public static bool TryParse(
        ReadOnlySpan<string> arguments,
        [NotNullWhen(true)] out SentinelStartInfo? info
    )
    {
        info = null;
        if (arguments.Length != ArgumentCount)
        {
            return false;
        }

        if (
            !TryValue(arguments[0], Protocol, out var protocolText)
            || !int.TryParse(
                protocolText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var protocol
            )
            || protocol != ProtocolVersion
        )
        {
            return false;
        }

        if (
            !TryHandle(arguments[1], Parent, out var parent)
            || !TryHandle(arguments[2], LedgerPrefix, out var ledger)
            || !TryHandle(arguments[3], Pipe, out var pipe)
        )
        {
            return false;
        }

        if (
            !TryValue(arguments[4], Heartbeat, out var heartbeatText)
            || !int.TryParse(
                heartbeatText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var heartbeatMs
            )
            || heartbeatMs <= 0
        )
        {
            return false;
        }

        if (!TryValue(arguments[5], CrashLoop, out var loopText))
        {
            return false;
        }

        var slash = loopText.IndexOf('/', StringComparison.Ordinal);
        if (
            slash <= 0
            || !int.TryParse(
                loopText[..slash],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var count
            )
            || !long.TryParse(
                loopText[(slash + 1)..],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var windowMs
            )
            || count <= 0
            || windowMs <= 0
        )
        {
            return false;
        }

        if (
            !TryValue(arguments[6], RefusedRelease, out var refusedText)
            || !long.TryParse(
                refusedText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var refusedMs
            )
            || refusedMs <= 0
        )
        {
            return false;
        }

        info = new SentinelStartInfo(
            parent,
            ledger,
            pipe,
            TimeSpan.FromMilliseconds(heartbeatMs),
            count,
            TimeSpan.FromMilliseconds(windowMs),
            TimeSpan.FromMilliseconds(refusedMs)
        );
        return true;
    }

    /// <summary>The command line arguments, culture-invariant, starting with the protocol version.</summary>
    public ImmutableArray<string> ToArguments() =>
        [
            Protocol + ProtocolVersion.ToString(CultureInfo.InvariantCulture),
            Parent + Hex(ParentProcess),
            LedgerPrefix + Hex(Ledger),
            Pipe + Hex(HeartbeatPipe),
            Heartbeat
                + ((long)HeartbeatInterval.TotalMilliseconds).ToString(
                    CultureInfo.InvariantCulture
                ),
            CrashLoop
                + CrashLoopCount.ToString(CultureInfo.InvariantCulture)
                + "/"
                + ((long)CrashLoopWindow.TotalMilliseconds).ToString(CultureInfo.InvariantCulture),
            RefusedRelease
                + ((long)RefusedReleaseWait.TotalMilliseconds).ToString(
                    CultureInfo.InvariantCulture
                ),
        ];

    /// <summary>The three inherited handles, in the order of the handle list.</summary>
    public ImmutableArray<nint> InheritedHandles => [ParentProcess, Ledger, HeartbeatPipe];

    private static string Hex(nint handle) =>
        "0x" + ((long)handle).ToString("X", CultureInfo.InvariantCulture);

    private static bool TryValue(string argument, string prefix, out string value)
    {
        if (argument is not null && argument.StartsWith(prefix, StringComparison.Ordinal))
        {
            value = argument[prefix.Length..];
            return value.Length > 0;
        }

        value = string.Empty;
        return false;
    }

    private static bool TryHandle(string argument, string prefix, out nint handle)
    {
        handle = 0;
        if (
            !TryValue(argument, prefix, out var text)
            || !text.StartsWith("0x", StringComparison.Ordinal)
            || !long.TryParse(
                text.AsSpan(2),
                NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture,
                out var value
            )
            || value <= 0
        )
        {
            return false;
        }

        handle = (nint)value;
        return true;
    }
}
