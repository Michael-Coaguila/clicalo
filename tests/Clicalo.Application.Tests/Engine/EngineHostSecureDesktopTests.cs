using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.KeySafety;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// The host's side of the releases the secure desktop refuses (blueprint §7.6, INV-3, D-22): an exception never
/// forgets them, and the return of the input desktop sends them again.
/// </summary>
[Trait("Req", "SEG-006")]
[Trait("Req", "NFR-005")]
public sealed class EngineHostSecureDesktopTests
{
    [Fact]
    public void An_exception_keeps_the_releases_the_secure_desktop_refused()
    {
        var refused = InjectedEvent.KeyUp(HostWorld.Ctrl);
        using var world = new HostWorld(
            HostWorld.WithForeground(EngineState.Empty with { BlockedReleases = [refused] })
        )
        {
            ThrowOn = typeof(EngineEvent.SessionResumed),
        };

        world.Handle(new EngineEvent.SessionResumed());

        world.Host.State.BlockedReleases.Items.ShouldBe([refused]);
        world.Host.IsStopped.ShouldBeFalse();
    }

    [Fact]
    public void An_exception_whose_release_the_secure_desktop_refuses_keeps_it_to_send_again()
    {
        using var world = new HostWorld(HostWorld.HoldingShift())
        {
            ThrowOn = typeof(EngineEvent.SessionResumed),
        };
        world.Injector.NextStatus = InjectionStatus.Blocked;

        world.Handle(new EngineEvent.SessionResumed());

        world.Host.State.Keys.IsEmpty.ShouldBeTrue();
        world.Host.State.BlockedReleases.Items.ShouldBe([InjectedEvent.KeyUp(HostWorld.Shift)]);
    }
}
