using System.Globalization;
using System.IO;
using Clicalo.Platform.Core.Guardian;

namespace Clicalo.App;

/// <summary>
/// The command line of <c>Clicalo.exe</c> in M2. A normal start has no option; Sentinel adds
/// <c>--after-crash=&lt;Unix ms&gt;</c> (and <c>--safe-mode</c> after a crash loop) when it relaunches a process that
/// died (ADR-0018). The other options are for development and measurement, and a second start passes its arguments to
/// the running instance only as «show» (SIS-003).
/// </summary>
/// <param name="DataDirectory">
/// Where the document, the usage and the backups live: <c>%AppData%\Clicalo</c>, or the folder of
/// <c>--data &lt;folder&gt;</c> (<c>cl run</c> isolates its data in <c>%TEMP%\clicalo-dev</c>).
/// </param>
/// <param name="SendInput">
/// False with <c>--no-input</c>: nothing is ever injected (no key, no internal chord, no preventive release at start),
/// so the app can run on a development machine without touching its keyboard. Sentinel is not started either: there
/// is nothing to guard.
/// </param>
/// <param name="GuardianAfterFirstFrame">
/// <c>--guardian after-first-frame</c>: launch Sentinel only after the first frame instead of in parallel to it, the
/// alternative spike S5 compares (blueprint §3.1).
/// </param>
public sealed record AppOptions(string DataDirectory, bool SendInput, bool GuardianAfterFirstFrame)
{
    /// <summary>The option that isolates the data folder.</summary>
    public const string DataOption = "--data";

    /// <summary>The option that disables every injection.</summary>
    public const string NoInputOption = "--no-input";

    /// <summary>The option that chooses when Sentinel starts.</summary>
    public const string GuardianOption = "--guardian";

    /// <summary>The value of <see cref="GuardianOption"/> that launches Sentinel after the first frame.</summary>
    public const string AfterFirstFrame = "after-first-frame";

    /// <summary>The value of <see cref="GuardianOption"/> that launches Sentinel in parallel (the default).</summary>
    public const string Parallel = "parallel";

    /// <summary>The diagnostic option that exits normally a number of seconds after the first frame.</summary>
    public const string ExitAfterOption = "--exit-after";

    /// <summary>
    /// Whether the data folder came from <see cref="DataOption"/>: an isolated folder keeps everything, the emergency
    /// copy of <c>pending\</c> and the crash journal included, inside it.
    /// </summary>
    public bool IsolatedData { get; init; }

    /// <summary>When the previous process died, from Sentinel's <c>--after-crash=&lt;Unix ms&gt;</c>.</summary>
    public DateTimeOffset? AfterCrash { get; init; }

    /// <summary>Sentinel's <c>--safe-mode</c>: the previous processes crashed in a loop (<c>Timings.App.CrashLoop</c>).</summary>
    public bool SafeMode { get; init; }

    /// <summary>The delay of <see cref="ExitAfterOption"/>: the start and the whole exit sequence, unattended.</summary>
    public TimeSpan? ExitAfter { get; init; }

    /// <summary>Parses <paramref name="arguments"/>; unknown words are ignored (a newer shortcut or link may pass more).</summary>
    /// <param name="arguments">The command line.</param>
    /// <param name="defaultDataDirectory">The data folder of a normal start.</param>
    public static AppOptions Parse(IReadOnlyList<string> arguments, string defaultDataDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var data = defaultDataDirectory;
        var isolated = false;
        var send = true;
        var afterFirstFrame = false;
        var safeMode = false;
        DateTimeOffset? afterCrash = null;
        TimeSpan? exitAfter = null;
        for (var i = 0; i < arguments.Count; i++)
        {
            var argument = arguments[i];
            if (Is(argument, NoInputOption))
            {
                send = false;
            }
            else if (Is(argument, DataOption) && i + 1 < arguments.Count)
            {
                data = Path.GetFullPath(arguments[++i]);
                isolated = true;
            }
            else if (Is(argument, GuardianOption) && i + 1 < arguments.Count)
            {
                afterFirstFrame = Is(arguments[++i], AfterFirstFrame);
            }
            else if (Is(argument, CrashJournal.SafeModeArgument))
            {
                safeMode = true;
            }
            else if (
                argument.StartsWith(
                    CrashJournal.AfterCrashArgument,
                    StringComparison.OrdinalIgnoreCase
                )
                && long.TryParse(
                    argument.AsSpan(CrashJournal.AfterCrashArgument.Length),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var diedAt
                )
                && diedAt <= DateTimeOffset.MaxValue.ToUnixTimeMilliseconds()
            )
            {
                afterCrash = DateTimeOffset.FromUnixTimeMilliseconds(diedAt);
            }
            else if (
                Is(argument, ExitAfterOption)
                && i + 1 < arguments.Count
                && int.TryParse(
                    arguments[++i],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var seconds
                )
            )
            {
                exitAfter = TimeSpan.FromSeconds(seconds);
            }
        }

        return new AppOptions(data, send, afterFirstFrame)
        {
            IsolatedData = isolated,
            AfterCrash = afterCrash,
            SafeMode = safeMode,
            ExitAfter = exitAfter,
        };
    }

    private static bool Is(string argument, string option) =>
        string.Equals(argument, option, StringComparison.OrdinalIgnoreCase);
}
