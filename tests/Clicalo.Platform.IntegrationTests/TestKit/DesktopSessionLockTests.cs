using Clicalo.TestKit.Windows;

namespace Clicalo.Platform.IntegrationTests.TestKit;

/// <summary>
/// The lock that keeps two desktop test runs, or a run and a SpikeLab session, from taking the foreground from each
/// other: a second holder is refused while the first holds it and gets it once it is released.
/// </summary>
public sealed class DesktopSessionLockTests
{
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
}
