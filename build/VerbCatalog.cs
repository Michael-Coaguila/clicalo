namespace Clicalo.Build;

/// <summary>
/// Every <c>cl</c> verb of blueprint §13: one dictable word each. Verbs that are planned but not built yet are
/// listed too, so <c>cl beta</c> answers "available in M5" instead of "unknown verb". <c>states</c> and
/// <c>accept</c> are not built (deviations D-29): the headless previews and the manual acceptance script replace them.
/// <c>pr</c> is not built either: a pull request is opened from GitHub, and a verb that answered "available in M1"
/// five milestones later only misled.
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
    public const string Trace = "trace";
    public const string Run = "run";
    public const string Note = "note";
    public const string Perf = "perf";
    public const string Quarantine = "quarantine";
    public const string Package = "package";

    /// <summary>
    /// Verbs that run a verb of <c>tools/Clicalo.DevCli</c> with the same name. Everything written after one of
    /// them on the command line is passed to it (for example <c>cl i18n-import --check</c>).
    /// </summary>
    public static IReadOnlyList<string> DevCli { get; } = [I18nCheck, I18nImport, AdrCheck, Trace];

    /// <summary>
    /// Verbs whose following words are their own options: the developer CLI verbs and <c>cl package</c>
    /// (<c>cl package --channel beta --version 2.0.0-beta.1</c>).
    /// </summary>
    public static IReadOnlyList<string> WithArguments { get; } = [.. DevCli, Package];

    /// <summary>
    /// Verbs implemented so far (M0, the M2 walking skeleton, the nightly quarantine, the package and the
    /// traceability report), in the order they are listed to people.
    /// </summary>
    public static IReadOnlyList<string> Available { get; } =
    [
        Setup,
        Build,
        Fast,
        Test,
        Desk,
        Fix,
        Check,
        Clean,
        .. DevCli,
        Run,
        Note,
        Perf,
        Quarantine,
        Package,
    ];

    /// <summary>Verbs that are planned and not built yet (blueprint §14), in the order of blueprint §13.</summary>
    public static IReadOnlyList<FutureVerb> Future { get; } =
    [
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
            if (WithArguments.Contains(args[i], StringComparer.Ordinal))
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
