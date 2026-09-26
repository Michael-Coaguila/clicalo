namespace Clicalo.Performance;

/// <summary>
/// Placeholder that keeps the test executable valid until the S5 measurements land (milestone M2, package app). Every
/// measurement that starts Clicalo.exe carries [Trait("Requires", "Desktop")] and runs with sending disabled outside
/// the CI.
/// </summary>
public sealed class SmokeTests
{
    [Fact]
    public void Test_project_runs() => true.ShouldBeTrue();
}
