using System.IO;

namespace Clicalo.App;

/// <summary>
/// The command line of <c>Clicalo.exe</c> in M2. Every option is for development and measurement: a normal start has
/// none, and a second start passes its arguments to the running instance only as «show» (SIS-003).
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

    /// <summary>Parses <paramref name="arguments"/>; unknown words are ignored (a newer shortcut or link may pass more).</summary>
    /// <param name="arguments">The command line.</param>
    /// <param name="defaultDataDirectory">The data folder of a normal start.</param>
    public static AppOptions Parse(IReadOnlyList<string> arguments, string defaultDataDirectory)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var data = defaultDataDirectory;
        var send = true;
        var afterFirstFrame = false;
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
            }
            else if (Is(argument, GuardianOption) && i + 1 < arguments.Count)
            {
                afterFirstFrame = Is(arguments[++i], AfterFirstFrame);
            }
        }

        return new AppOptions(data, send, afterFirstFrame);
    }

    private static bool Is(string argument, string option) =>
        string.Equals(argument, option, StringComparison.OrdinalIgnoreCase);
}
