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

    /// <summary>Verbs implemented in M0, in the order they are listed to people.</summary>
    public static IReadOnlyList<string> Available { get; } =
    [Setup, Build, Fast, Test, Desk, Fix, Check, Clean];

    /// <summary>Verbs of later milestones (blueprint §14), in the order of blueprint §13.</summary>
    public static IReadOnlyList<FutureVerb> Future { get; } =
    [
        // The first runnable panel is the M2 walking skeleton.
        new("run", "M2"),
        // The state matrix with approved render snapshots is an M3 exit criterion.
        new("states", "M3"),
        // Hardware acceptance and the manual script first gate a milestone in M3.
        new("accept", "M3"),
        // M3 requires every MUST of the panel, engine, touch and safety modules to carry [Req].
        new("trace", "M3"),
        // User-facing notes become mandatory with the first feature pull requests (M2).
        new("note", "M2"),
        // Spike pull requests start in M1.
        new("pr", "M1"),
        // The first signed beta is the M5 exit criterion.
        new("beta", "M5"),
        // The touch-to-SendInput p95 budget is an M2 exit criterion.
        new("perf", "M2"),
        // Manifest signing with the hardware key ships with the update channel (M5).
        new("sign-manifest", "M5"),
    ];

    /// <summary>Whether <paramref name="verb"/> is implemented.</summary>
    public static bool IsAvailable(string verb) => Available.Contains(verb, StringComparer.Ordinal);

    /// <summary>The planned verb called <paramref name="verb"/>, or <see langword="null"/>.</summary>
    public static FutureVerb? FindFuture(string verb) =>
        Future.FirstOrDefault(future => string.Equals(future.Name, verb, StringComparison.Ordinal));
}
