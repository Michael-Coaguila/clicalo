using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Clicalo.Platform.Core.Guardian;

/// <summary>
/// The start-up contract of <c>Clicalo.Sentinel.exe</c> (blueprint §3.1, ADR-0023): the one handle it inherits through
/// <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> (the main process) and the thresholds it needs, since it cannot read
/// <c>timings.json</c>. Written by the main process on the command line and parsed by Sentinel; a public contract
/// between two executables of the same version.
/// </summary>
/// <remarks>
/// The arguments, in this order and culture-invariant: <c>--protocol=4</c>, <c>--parent=0x…</c>,
/// <c>--retry-ms=&lt;n&gt;</c>, <c>--crash-loop=&lt;n&gt;/&lt;ms&gt;</c> and, only for a main process started with
/// <c>--data</c>, <c>--data=&lt;folder&gt;</c>. Protocol 4 (ADR-0032) added the data folder to protocol 3 (ADR-0023),
/// which had replaced protocol 2 of ADR-0018; any other version is refused.
/// </remarks>
/// <param name="ParentProcess">The main process, with <c>SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION</c>.</param>
/// <param name="ReleaseRetryInterval">
/// <c>Timings.Guardian.ReleaseRetryInterval</c>: how often Sentinel tries again a release Windows refused.
/// </param>
/// <param name="CrashLoopCount"><c>Timings.App.CrashLoop.Count</c>.</param>
/// <param name="CrashLoopWindow"><c>Timings.App.CrashLoop.Window</c>.</param>
public sealed record SentinelStartInfo(
    nint ParentProcess,
    TimeSpan ReleaseRetryInterval,
    int CrashLoopCount,
    TimeSpan CrashLoopWindow
)
{
    /// <summary>Version of this contract; Sentinel refuses any other.</summary>
    public const int ProtocolVersion = 4;

    private const string Protocol = "--protocol=";
    private const string Parent = "--parent=";
    private const string Retry = "--retry-ms=";
    private const string CrashLoop = "--crash-loop=";
    private const string Data = "--data=";
    private const int ArgumentCount = 4;

    /// <summary>
    /// The data folder of a main process started with <c>--data</c> (ADR-0030), or <see langword="null"/> for the
    /// user's folder. Sentinel reads the crash journal inside it and relaunches with it (ADR-0032).
    /// </summary>
    public string? DataFolder { get; init; }

    /// <summary>The inherited handles, in the order of the handle list: only the parent.</summary>
    public ImmutableArray<nint> InheritedHandles => [ParentProcess];

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
        if (
            arguments.Length is not (ArgumentCount or ArgumentCount + 1)
            || !TryNumber(arguments[0], Protocol, out var protocol)
            || protocol != ProtocolVersion
            || !TryHandle(arguments[1], Parent, out var parent)
            || !TryNumber(arguments[2], Retry, out var retryMs)
            || retryMs <= 0
            || !TryValue(arguments[3], CrashLoop, out var loopText)
        )
        {
            return false;
        }

        var slash = loopText.IndexOf('/', StringComparison.Ordinal);
        if (
            slash <= 0
            || !long.TryParse(
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
            || count is <= 0 or > int.MaxValue
            || windowMs <= 0
        )
        {
            return false;
        }

        string? folder = null;
        if (
            arguments.Length > ArgumentCount
            && (
                !TryValue(arguments[ArgumentCount], Data, out folder)
                || !CrashJournal.IsDataFolder(folder)
            )
        )
        {
            return false;
        }

        info = new SentinelStartInfo(
            parent,
            TimeSpan.FromMilliseconds(retryMs),
            (int)count,
            TimeSpan.FromMilliseconds(windowMs)
        )
        {
            DataFolder = folder,
        };
        return true;
    }

    /// <summary>
    /// The command line arguments, culture-invariant, starting with the protocol version; the data folder is the only
    /// one that may hold spaces, and <see cref="GuardianProcess"/> quotes it.
    /// </summary>
    public ImmutableArray<string> ToArguments()
    {
        ImmutableArray<string> arguments =
        [
            Protocol + ProtocolVersion.ToString(CultureInfo.InvariantCulture),
            Parent + "0x" + ((long)ParentProcess).ToString("X", CultureInfo.InvariantCulture),
            Retry + Milliseconds(ReleaseRetryInterval),
            CrashLoop
                + CrashLoopCount.ToString(CultureInfo.InvariantCulture)
                + "/"
                + Milliseconds(CrashLoopWindow),
        ];
        return DataFolder is null ? arguments : arguments.Add(Data + DataFolder);
    }

    private static string Milliseconds(TimeSpan duration) =>
        ((long)duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture);

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

    private static bool TryNumber(string argument, string prefix, out long value)
    {
        value = 0;
        return TryValue(argument, prefix, out var text)
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
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
