using System.ComponentModel;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Core.Guardian;
using Clicalo.Platform.Windows.SentinelHost;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The main process's side of the guardian (blueprint §3.1, ADR-0022): Sentinel starts with the protocol 3 contract and
/// one inherited handle, is checked every <c>Timings.Guardian.WatchInterval</c>, is started again after
/// <c>Timings.Guardian.RestartBackoff</c> when it died, and too many deaths stop the restarts. No guardian is started:
/// the launcher is a fake and the clock is a fake.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class SentinelSupervisorTests
{
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 10, 5, 10, 0, 0, TimeSpan.Zero)
    );

    private readonly List<FakeChild> _children = [];

    [Fact]
    public void Sentinel_gets_the_contract_is_watched_and_restarted_after_the_backoff()
    {
        using var supervisor = Create();

        supervisor.Start();
        var first = supervisor.ProcessId.ShouldNotBeNull();
        var info = supervisor.LastStartInfo.ShouldNotBeNull();
        info.InheritedHandles.Length.ShouldBe(1);
        info.ReleaseRetryInterval.ShouldBe(Timings.Guardian.ReleaseRetryInterval);
        info.CrashLoopCount.ShouldBe(Timings.App.CrashLoop.Count);
        info.ToArguments()[0].ShouldBe("--protocol=" + SentinelStartInfo.ProtocolVersion);

        // Sentinel dies: the next check sees it and the restart waits for the backoff.
        _children[0].HasExited = true;
        _time.Advance(Timings.Guardian.WatchInterval);
        supervisor.ProcessId.ShouldBeNull();
        _children[0].Disposed.ShouldBeTrue();
        _time.Advance(Timings.Guardian.RestartBackoff[0]);

        var second = supervisor.ProcessId.ShouldNotBeNull();
        second.ShouldNotBe(first);
        supervisor.Launches.ShouldBe(2);
    }

    [Fact]
    public void Stopping_only_stops_supervising()
    {
        using var supervisor = Create();
        supervisor.Start();

        supervisor.Stop();
        _children[0].HasExited = true;
        _time.Advance(Timings.Guardian.WatchInterval + Timings.Guardian.RestartBackoff[^1]);

        supervisor.Launches.ShouldBe(1);
        _children[0].Disposed.ShouldBeTrue();
    }

    [Fact]
    public void Too_many_deaths_in_the_window_stop_the_restarts_and_say_so()
    {
        using var supervisor = Create(fail: true);
        var unstable = 0;
        supervisor.GuardianUnstable += (_, _) => unstable++;

        supervisor.Start();
        foreach (var wait in Timings.Guardian.RestartBackoff)
        {
            _time.Advance(wait);
        }

        unstable.ShouldBe(1);
        supervisor.ProcessId.ShouldBeNull();
        supervisor.Launches.ShouldBe(0);
    }

    private SentinelSupervisor Create(bool fail = false) =>
        new(
            _ =>
            {
                if (fail)
                {
                    throw new Win32Exception(2);
                }

                var child = new FakeChild(100 + _children.Count);
                _children.Add(child);
                return child;
            },
            _time,
            NullLogger<SentinelSupervisor>.Instance
        );

    private sealed class FakeChild(int id) : ISentinelChild
    {
        public int Id { get; } = id;

        public bool HasExited { get; set; }

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
