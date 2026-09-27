using Clicalo.App.Lifecycle;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.App.Tests;

/// <summary>
/// Sentinel is no longer restarted (D-22): the user is told on the panel, assertively, that the key protection is off,
/// since from then on a death of the process would leave keys down with nobody to release them.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-006")]
public sealed class GuardianUnstableNoticeTests
{
    [Fact]
    public void An_unstable_guardian_tells_the_user_at_once()
    {
        var guardian = new FakeGuardian();
        var relay = new EngineObserverRelay(static work => work());
        var notices = new List<EngineNoticeEventArgs>();
        relay.NoticeRaised += (_, notice) => notices.Add(notice);
        using var watch = new GuardianUnstableNotice(guardian, relay, NullLogger.Instance);

        guardian.RaiseUnstable();

        var notice = notices.ShouldHaveSingleItem();
        notice.Text.ShouldBe(L.GuardianUnstable);
        notice.Urgency.ShouldBe(NoticeUrgency.Assertive);
    }

    [Fact]
    public void Nothing_is_said_after_the_teardown()
    {
        var guardian = new FakeGuardian();
        var relay = new EngineObserverRelay(static work => work());
        var notices = new List<EngineNoticeEventArgs>();
        relay.NoticeRaised += (_, notice) => notices.Add(notice);
        new GuardianUnstableNotice(guardian, relay, NullLogger.Instance).Dispose();

        guardian.RaiseUnstable();

        notices.ShouldBeEmpty();
    }

    private sealed class FakeGuardian : IGuardian
    {
        public event EventHandler? Unstable;

        public bool IsRunning => false;

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void RaiseUnstable() => Unstable?.Invoke(this, EventArgs.Empty);
    }
}
