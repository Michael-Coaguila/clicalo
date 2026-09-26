namespace Clicalo.DevCli.I18n;

/// <summary>
/// Checks that only the DevCli runs: the handoff import (CLCI100) and the usage checks, which need the product code
/// (the build validates the data alone with CLCI001–015). Documented in <c>docs/guides/i18n.md</c>.
/// </summary>
internal static class I18nCheckIds
{
    /// <summary><c>i18n-import</c>: the handoff or the recipe has a problem, or <c>data/i18n</c> differs from a fresh import.</summary>
    public const string ImportFailed = "CLCI100";

    /// <summary><c>allow-unused.txt</c> is missing or lists something that is not a key.</summary>
    public const string AllowUnusedUnknown = "CLCI101";

    /// <summary>A key of <c>allow-unused.txt</c> is used: remove it from the list.</summary>
    public const string AllowUnusedButUsed = "CLCI102";

    /// <summary>A key appears twice in <c>allow-unused.txt</c>.</summary>
    public const string AllowUnusedRepeated = "CLCI103";

    /// <summary>With <c>--strict-unused</c>: a key no code uses and <c>allow-unused.txt</c> does not list.</summary>
    public const string UnusedKey = "CLCI104";
}
