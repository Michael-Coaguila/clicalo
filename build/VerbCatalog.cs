namespace Clicalo.Build;

/// <summary>
/// Every <c>cl</c> verb of blueprint §13: one dictable word each. Verbs that later milestones deliver are
/// listed too, so <c>cl run</c> answers "available in M2" instead of "unknown verb".
/// </summary>
internal static class VerbCatalog
{
    public const string Setup = "setup";
    public const string Build = "build";
    public const string Fast = "fast";
    public const string Test = "test";
    public const string Desk = "desk";
    public const string Fix = "fix";
    public const string Check = "check";
    public const string Clean = "clean";
    public const string I18nCheck = "i18n-check";
    public const string I18nImport = "i18n-import";
    public const string AdrCheck = "adr-check";
    public const string Run = "run";
    public const string Note = "note";
    public const string Perf = "perf";

    /// <summary>
    /// Verbs that run a verb of <c>tools/Clicalo.DevCli</c> with the same name. Everything written after one of
    /// them on the command line is passed to it (for example <c>cl i18n-import --check</c>).
    /// </summary>
    public static IReadOnlyList<string> DevCli { get; } = [I18nCheck, I18nImport, AdrCheck];

    /// <summary>Verbs implemented so far (M0 and the M2 walking skeleton), in the order they are listed to people.</summary>
    public static IReadOnlyList<string> Available { get; } =
    [Setup, Build, Fast, Test, Desk, Fix, Check, Clean, .. DevCli, Run, Note, Perf];

    /// <summary>Verbs of later milestones (blueprint §14), in the order of blueprint §13.</summary>
    public static IReadOnlyList<FutureVerb> Future { get; } =
    [
        // The state matrix with approved render snapshots is an M3 exit criterion.
        new("states", "M3"),
        // Hardware acceptance and the manual script first gate a milestone in M3.
        new("accept", "M3"),
        // M3 requires every MUST of the panel, engine, touch and safety modules to carry [Req].
        new("trace", "M3"),
        // Spike pull requests start in M1.
        new("pr", "M1"),
        // The first signed beta is the M5 exit criterion.
        new("beta", "M5"),
        // Manifest signing with the hardware key ships with the update channel (M5).
        new("sign-manifest", "M5"),
    ];

    /// <summary>Whether <paramref name="verb"/> runs a verb of the developer CLI.</summary>
    public static bool IsDevCli(string verb) => DevCli.Contains(verb, StringComparer.Ordinal);

    /// <summary>
    /// Splits a command line at the first developer CLI verb: <c>cl</c> parses what comes up to and including that
    /// verb, and everything after it goes to the developer CLI untouched, so <c>cl i18n-import --check</c> passes
    /// <c>--check</c> on instead of rejecting it as an option of <c>cl</c>.
    /// </summary>
    public static (IReadOnlyList<string> Cl, IReadOnlyList<string> DevCli) SplitArguments(
        IReadOnlyList<string> args
    )
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (IsDevCli(args[i]))
            {
                return ([.. args.Take(i + 1)], [.. args.Skip(i + 1)]);
            }
        }

        return (args, []);
    }

    /// <summary>Whether <paramref name="verb"/> is implemented.</summary>
    public static bool IsAvailable(string verb) => Available.Contains(verb, StringComparer.Ordinal);

    /// <summary>The planned verb called <paramref name="verb"/>, or <see langword="null"/>.</summary>
    public static FutureVerb? FindFuture(string verb) =>
        Future.FirstOrDefault(future => string.Equals(future.Name, verb, StringComparison.Ordinal));
}
