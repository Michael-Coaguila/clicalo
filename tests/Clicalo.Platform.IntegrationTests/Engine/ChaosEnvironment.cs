using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Gate of the chaos tests of S9 (M2-ownership.md, rule 6): they kill processes with keys held and freeze threads with
/// real injection, so they run only on a continuous integration runner with desktop tests enabled, never on the
/// maintainer's machine, whatever <c>cl desk</c> selects. They also carry <c>[Trait("Category", "Chaos")]</c>.
/// </summary>
public static class ChaosEnvironment
{
    /// <summary>Reason reported for skipped chaos tests.</summary>
    public const string SkipReason =
        "Chaos test (S9): only on a continuous integration runner (CI=true) with "
        + DesktopTestEnvironment.Variable
        + "=1; it kills a process that holds keys.";

    /// <summary>True on a CI runner with desktop tests enabled.</summary>
    public static bool IsEnabled =>
        DesktopTestEnvironment.IsEnabled && DesktopTestEnvironment.IsContinuousIntegration;
}
