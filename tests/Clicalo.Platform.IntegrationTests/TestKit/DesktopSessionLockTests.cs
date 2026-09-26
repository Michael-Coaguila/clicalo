using System.Globalization;
using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>
/// The lock that keeps two desktop test runs, or a run and a SpikeLab session, from taking the foreground from each
/// other: a second holder is refused while the first holds it and gets it once it is released; a helper process that
/// the holder starts (an out-of-process UI Automation client) runs inside its parent's session instead of waiting
/// for it.
/// </summary>
public sealed class DesktopSessionLockTests
{
    private const int Current = 4242;
    private const int Parent = 1717;

    [Fact]
    public void Only_one_holder_at_a_time()
    {
        using var first = DesktopSessionLock.TryAcquire(TimeSpan.Zero);
        if (first is null)
        {
            Assert.Skip("A desktop test run or SpikeLab holds the desktop right now.");
        }

        DesktopSessionLock.TryAcquire(TimeSpan.Zero).ShouldBeNull("the desktop is taken");

        first.Dispose();
        using var second = DesktopSessionLock.TryAcquire(TimeSpan.FromSeconds(5));
        second.ShouldNotBeNull("released, the desktop is free again");
    }

    [Fact]
    public void A_helper_started_by_the_holder_runs_inside_its_session() =>
        DesktopSessionLock
            .IsHeldByParent(Id(Parent), Current, processId => processId == Parent)
            .ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a process")]
    [InlineData("-5")]
    [InlineData("0")]
    public void Without_a_holder_the_process_takes_the_lock_itself(string? holder) =>
        DesktopSessionLock.IsHeldByParent(holder, Current, _ => true).ShouldBeFalse();

    [Fact]
    public void The_holder_itself_and_a_holder_that_ended_do_not_count()
    {
        DesktopSessionLock.IsHeldByParent(Id(Current), Current, _ => true).ShouldBeFalse();
        DesktopSessionLock.IsHeldByParent(Id(Parent), Current, _ => false).ShouldBeFalse();
    }

    private static string Id(int processId) => processId.ToString(CultureInfo.InvariantCulture);
}
