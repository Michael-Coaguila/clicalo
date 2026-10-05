using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.TestKit;

namespace Clicalo.Sentinel.Tests;

/// <summary>
/// The real <c>Clicalo.Sentinel.exe</c>, started as the main process starts it: three inherited handles through
/// <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> (ADR-0018). Safe on any machine: the ledger records nothing and carries
/// <c>CleanShutdown | NoRelaunch</c>, and the parent (this test process) stays alive, so Sentinel neither releases nor
/// relaunches anything; it only proves that it waits on the pipe and leaves when the pipe closes.
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

    [Fact]
    public void Sentinel_refuses_arguments_that_do_not_follow_the_contract()
    {
        using var sentinel = GuardianProcess.Start(SentinelPath(), ["--protocol=1"], []);

        sentinel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        sentinel.ExitCode.ShouldBe((int)SentinelExitCode.InvalidArguments);
    }

    [Fact]
    public void Sentinel_waits_on_its_three_handles_and_leaves_when_the_pipe_closes_while_the_parent_lives()
    {
        using var ledger = KeyLedgerSection.CreateForEngine();
        ledger.SetMarks(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch);
        var parent = GuardianHandles.DuplicateCurrentProcessForChild();
        var ledgerHandle = ledger.DuplicateForGuardian();
        var (read, write) = GuardianHandles.CreateHeartbeatPipe();
        var info = new SentinelStartInfo(
            parent,
            ledgerHandle,
            read,
            TimeSpan.FromMilliseconds(100),
            3,
            TimeSpan.FromMinutes(10),
            TimeSpan.FromSeconds(30)
        );
        GuardianProcess sentinel;
        try
        {
            sentinel = GuardianProcess.Start(
                SentinelPath(),
                info.ToArguments(),
                info.InheritedHandles.AsSpan()
            );
        }
        finally
        {
            GuardianHandles.Close(parent);
            GuardianHandles.Close(ledgerHandle);
            GuardianHandles.Close(read);
        }

        using (sentinel)
        {
            try
            {
                GuardianHandles.WriteHeartbeat(write).ShouldBeTrue();
                sentinel.WaitForExit(TimeSpan.FromMilliseconds(500)).ShouldBeFalse();
            }
            finally
            {
                GuardianHandles.Close(write);
            }

            sentinel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            sentinel.ExitCode.ShouldBe((int)SentinelExitCode.CleanExit);
        }
    }
}
