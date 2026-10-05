using System.Diagnostics;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.SentinelHost;
using Clicalo.TestKit;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The main process's side of the guardian (blueprint §3.1): Sentinel starts with exactly three inherited handles, is
/// fed a heartbeat, is started again after <c>Timings.Guardian.RestartBackoff</c> when it dies, and leaves when the
/// pipe closes. Safe on any machine: the ledger holds nothing and says <c>CleanShutdown | NoRelaunch</c>, and this test
/// process stays alive, so Sentinel never releases or relaunches anything.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class SentinelSupervisorTests
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
    public void Sentinel_is_started_with_three_handles_fed_restarted_after_the_backoff_and_left_on_stop()
    {
        using var ledger = KeyLedgerSection.CreateForEngine();
        ledger.SetMarks(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
        using var supervisor = new SentinelSupervisor(
            ledger,
            SentinelPath(),
            time,
            NullLogger<SentinelSupervisor>.Instance
        );

        supervisor.Start();
        var first = supervisor.ProcessId.ShouldNotBeNull();
        supervisor.LastStartInfo!.InheritedHandles.Length.ShouldBe(
            SentinelStartInfo.InheritedHandleCount
        );
        supervisor.LastStartInfo.HeartbeatInterval.ShouldBe(Timings.Guardian.PipeHeartbeatInterval);
        supervisor.LastStartInfo.RefusedReleaseWait.ShouldBe(Timings.Guardian.RefusedReleaseWait);
        time.Advance(Timings.Guardian.PipeHeartbeatInterval);
        supervisor.ProcessId.ShouldBe(first);

        // Sentinel dies (its ledger is empty: nothing to release).
        using (var sentinel = Process.GetProcessById(first))
        {
            sentinel.Kill();
            sentinel.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }

        supervisor.Beat();
        supervisor.ProcessId.ShouldBeNull();
        time.Advance(Timings.Guardian.RestartBackoff[0]);
        var second = supervisor.ProcessId.ShouldNotBeNull();
        second.ShouldNotBe(first);
        supervisor.Launches.ShouldBe(2);

        using var restarted = Process.GetProcessById(second);
        supervisor.Stop();
        restarted.WaitForExit(TimeSpan.FromSeconds(10)).ShouldBeTrue();
    }

    [Fact]
    public void Too_many_deaths_in_the_window_stop_the_restarts_and_say_so()
    {
        using var ledger = KeyLedgerSection.CreateForEngine();
        ledger.SetMarks(LedgerMarks.CleanShutdown | LedgerMarks.NoRelaunch);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
        var missing = Path.Combine(
            Path.GetTempPath(),
            "clicalo-no-sentinel",
            "Clicalo.Sentinel.exe"
        );
        using var supervisor = new SentinelSupervisor(
            ledger,
            missing,
            time,
            NullLogger<SentinelSupervisor>.Instance
        );
        var unstable = 0;
        supervisor.GuardianUnstable += (_, _) => unstable++;

        supervisor.Start();
        foreach (var wait in Timings.Guardian.RestartBackoff)
        {
            time.Advance(wait);
        }

        unstable.ShouldBe(1);
        supervisor.ProcessId.ShouldBeNull();
        supervisor.Launches.ShouldBe(0);
    }
}
