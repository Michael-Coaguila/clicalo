using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Touch;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// Clícalo's own chords (the rights chord of the foreground ladder, Win+H; blueprint §3.6, D-14, D-22) are sent by the
/// running engine, under the gate, with its generation: never by a thread beside it, never by an engine an emergency
/// replaced (INV-11). The real host, gate and ledger port; nothing is injected (<see cref="PhysicalStateInjector"/>).
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
    public async Task The_running_engine_sends_them_balanced_and_a_replaced_one_never_does()
    {
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        var replies = new InternalChordReplies();
        var ports = new EngineHostPorts(
            new GateInputInjector(gate),
            new KeyLedgerPort(gate),
            NoShell.Instance,
            NoShell.Instance,
            SilentObserver.Instance
        )
        {
            ChordReplies = replies,
        };
        using var host = new EngineHost(
            ports,
            new EngineGeneration(ledger.Generation),
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
            ledger.Snapshot().Slots.ShouldBeEmpty();

            // An emergency replaces this engine: nothing it is asked for goes any more.
            gate.TryEmergencyRelease(TimeSpan.FromMilliseconds(250), out _)
                .ShouldBe(EmergencyOutcome.Released);
            (await effects.SendDictationChordAsync(token)).ShouldBeFalse();

            system.Batches.Count.ShouldBe(2);
            replies.Pending.ShouldBe(0);
        }
        finally
        {
            await stop.CancelAsync();
            engine.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
        }
    }
}
