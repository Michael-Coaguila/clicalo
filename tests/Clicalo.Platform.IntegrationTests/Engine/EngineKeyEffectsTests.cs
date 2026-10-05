using Clicalo.Application.Engine;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Touch;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Clícalo's own chords (the rights chord of the foreground ladder, Win+H; blueprint §3.6, D-14, D-22) are sent by the
/// running engine, never by a thread beside it, and balanced. The real host and injector; nothing is injected
/// (<see cref="PhysicalStateInjector"/>).
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "BUS-003")]
public sealed class EngineKeyEffectsTests
{
    private static readonly EngineConfig Config = new(
        TimeSpan.FromSeconds(60),
        ReleaseOnAppSwitch: true,
        TimeSpan.FromMilliseconds(20),
        new TouchSettings(TimeSpan.FromMilliseconds(250), 8, 24, TimeSpan.Zero)
    );

    [Fact]
    public async Task The_running_engine_sends_them_balanced_and_only_when_the_rights_chord_is_registered()
    {
        var system = new PhysicalStateInjector();
        var replies = new InternalChordReplies();
        var ports = new EngineHostPorts(
            new InputInjector(system, system),
            NoShell.Instance,
            NoShell.Instance,
            SilentObserver.Instance
        )
        {
            ChordReplies = replies,
        };
        using var host = new EngineHost(
            ports,
            Config,
            TimeProvider.System,
            NullLogger<EngineHost>.Instance
        );
        using var stop = new CancellationTokenSource();
        var engine = host.StartOnDedicatedThread(stop.Token);
        var hotkey = new FakeRightsHotkey();
        var effects = new EngineKeyEffects(host, replies, hotkey, TimeProvider.System);
        var token = TestContext.Current.CancellationToken;
        try
        {
            (await effects.SendRightsHotkeyAsync(token)).ShouldBeFalse();
            system.Batches.ShouldBeEmpty();

            hotkey.IsRegistered = true;
            (await effects.SendRightsHotkeyAsync(token)).ShouldBeTrue();
            (await effects.SendDictationChordAsync(token)).ShouldBeTrue();
            system.Batches.Count.ShouldBe(2);
            system.Batches[0].Length.ShouldBe(8);
            system.IsEmpty.ShouldBeTrue();
            replies.Pending.ShouldBe(0);
        }
        finally
        {
            await stop.CancelAsync();
            engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
    }
}
