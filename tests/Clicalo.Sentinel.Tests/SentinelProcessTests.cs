using Clicalo.Platform.Core.Guardian;
using Clicalo.TestKit;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The real <c>Clicalo.Sentinel.exe</c> with arguments that do not follow the contract: it leaves at once with
/// <see cref="SentinelExitCode.InvalidArguments"/>, before it reads a key or waits on anything, so the test is safe on
/// any machine. What it does after a real death is the nightly chaos test of S9 (ADR-0023).
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class SentinelProcessTests
{
    private static string SentinelPath()
    {
        var local = Path.Combine(AppContext.BaseDirectory, "Clicalo.Sentinel.exe");
        if (File.Exists(local))
        {
            return local;
        }

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Name;
        return RepoPaths.Combine(
            "artifacts",
            "bin",
            "Clicalo.Sentinel",
            configuration,
            "Clicalo.Sentinel.exe"
        );
    }

    [Theory]
    [InlineData("--protocol=2")]
    [InlineData("--protocol=3")]
    public void Sentinel_refuses_arguments_that_do_not_follow_the_contract(string argument)
    {
        using var sentinel = GuardianProcess.Start(SentinelPath(), [argument], []);

        sentinel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        sentinel.ExitCode.ShouldBe((int)SentinelExitCode.InvalidArguments);
    }
}
