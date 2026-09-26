namespace Clicalo.TestKit.Windows;

/// <summary>
/// Gate for tests that need an interactive desktop (they inject input, own the foreground or show windows).
/// They are skipped unless <c>CLICALO_DESKTOP_TESTS=1</c>, and carry <c>[Trait("Requires", "Desktop")]</c>.
/// </summary>
/// <remarks>
/// Use it with xUnit v3 dynamic skipping:
/// <c>[Fact(Skip = DesktopTestEnvironment.SkipReason, SkipUnless = nameof(DesktopTestEnvironment.IsEnabled),
/// SkipType = typeof(DesktopTestEnvironment))]</c>. Whether a hosted CI runner qualifies is what spike S0 measures.
/// </remarks>
public static class DesktopTestEnvironment
{
    /// <summary>Environment variable that enables desktop tests when set to <c>1</c>.</summary>
    public const string Variable = "CLICALO_DESKTOP_TESTS";

    /// <summary>Reason reported for skipped desktop tests.</summary>
    public const string SkipReason =
        "Needs an interactive desktop: set "
        + Variable
        + "=1 on a machine where input may be injected into InputProbe.";

    /// <summary>
    /// Trait of the desktop tests that inject keys the maintainer's dictation and voice tools capture (right Ctrl,
    /// AltGr): <c>[Trait(ReservedKeysTraitName, ReservedKeysTraitValue)]</c>. <c>cl desk</c> runs them only in
    /// continuous integration, and <c>TestKeyboardInjector</c> refuses those keys anywhere else.
    /// </summary>
    public const string ReservedKeysTraitName = "Injects";

    /// <summary>Value of <see cref="ReservedKeysTraitName"/> for the tests that inject reserved keys.</summary>
    public const string ReservedKeysTraitValue = "ReservedKeys";

    /// <summary>True when <see cref="Variable"/> is <c>1</c>.</summary>
    public static bool IsEnabled =>
        string.Equals(Environment.GetEnvironmentVariable(Variable), "1", StringComparison.Ordinal);

    /// <summary>
    /// True in continuous integration (<c>CI=true</c> or <c>GITHUB_ACTIONS=true</c>): a disposable runner where right
    /// Ctrl and AltGr may be injected. False on the maintainer's machine, whose dictation hooks capture them.
    /// </summary>
    public static bool IsContinuousIntegration => IsTrue("CI") || IsTrue("GITHUB_ACTIONS");

    private static bool IsTrue(string variable) =>
        string.Equals(
            Environment.GetEnvironmentVariable(variable),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
}
