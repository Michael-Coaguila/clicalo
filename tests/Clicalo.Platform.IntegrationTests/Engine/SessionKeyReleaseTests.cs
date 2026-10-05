using Clicalo.Domain.Execution;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// What the engine hears when the session is locked or the machine sleeps (SEG-006, blueprint §7.6): lock and suspend
/// release everything, unlock and resume send again what the secure desktop refused, anything else is ignored. The
/// translation is pure; the messages themselves cannot be produced in a test without locking the user's session.
/// </summary>
[Trait("Req", "SEG-006")]
public sealed class SessionKeyReleaseTests
{
    private const uint Lock = 0x7;
    private const uint Unlock = 0x8;
    private const uint Suspend = 0x4;
    private const uint ResumeSuspend = 0x7;
    private const uint ResumeAutomatic = 0x12;

    [Fact]
    public void Locking_releases_everything() =>
        SessionKeyRelease
            .Translate(SessionKeyRelease.SessionChangeMessage, Lock)
            .ShouldBe(new EngineEvent.Terminal(TerminalReason.Lock));

    [Fact]
    public void Suspending_releases_everything() =>
        SessionKeyRelease
            .Translate(SessionKeyRelease.PowerBroadcastMessage, Suspend)
            .ShouldBe(new EngineEvent.Terminal(TerminalReason.Suspend));

    [Theory]
    [InlineData(SessionKeyRelease.SessionChangeMessage, Unlock)]
    [InlineData(SessionKeyRelease.PowerBroadcastMessage, ResumeSuspend)]
    [InlineData(SessionKeyRelease.PowerBroadcastMessage, ResumeAutomatic)]
    public void Unlocking_or_resuming_resends_what_was_refused(uint message, uint change) =>
        SessionKeyRelease.Translate(message, change).ShouldBeOfType<EngineEvent.SessionResumed>();

    [Theory]
    [InlineData(SessionKeyRelease.SessionChangeMessage, 0x1u)]
    [InlineData(SessionKeyRelease.SessionChangeMessage, 0x5u)]
    [InlineData(SessionKeyRelease.PowerBroadcastMessage, 0xAu)]
    [InlineData(SessionKeyRelease.PowerBroadcastMessage, 0x8013u)]
    [InlineData(0x0010u, Lock)]
    public void Other_changes_are_ignored(uint message, uint change) =>
        SessionKeyRelease.Translate(message, change).ShouldBeNull();
}
