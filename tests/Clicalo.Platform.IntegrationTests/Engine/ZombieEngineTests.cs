using Clicalo.Application.Engine;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// A zombie engine against the real gate, ledger port and emergency releaser (blueprint §3.2 rule 6, INV-11): the first
/// engine hangs outside the gate, the emergency fences it and starts a second one, which presses Shift and hangs too;
/// then the zombie resumes with nothing to send. The zombie must learn it is fenced from its heartbeat and never renew
/// the heartbeat of the second engine, so the second hang is still seen and escalates to a process restart (REG-03).
/// Nothing is injected: the gate sends to a <see cref="PhysicalStateInjector"/>.
/// </summary>
[Trait("Req", "REG-03")]
[Trait("Req", "SEG-007")]
public sealed class ZombieEngineTests
{
    private static readonly PhysicalKey Shift = new(0xA0, 0x2A, LedgerKeyAttributes.None);

    private static readonly EngineConfig Config = new(
        TimeSpan.FromSeconds(60),
        ReleaseOnAppSwitch: true,
        TimeSpan.FromMilliseconds(20),
        new TouchSettings(TimeSpan.FromMilliseconds(250), 8, 24, TimeSpan.Zero)
    );

    [Fact]
    public void A_zombie_that_resumes_never_hides_the_hang_of_the_engine_that_replaced_it()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero));
        using var ledger = KeyLedgerSection.CreateInMemory();
        var system = new PhysicalStateInjector();
        var gate = new InjectionGate(ledger, system);
        using var observer = new HangingObserver();
        var ports = new EngineHostPorts(
            new GateInputInjector(gate),
            new KeyLedgerPort(gate),
            NoShell.Instance,
            NoShell.Instance,
            observer
        );
        using var stop = new CancellationTokenSource();
        EngineHost? second = null;
        Thread? secondThread = null;
        var escalations = 0;
        using var releaser = new EmergencyReleaser(
            gate,
            time,
            generation =>
            {
                second = new EngineHost(
                    ports,
                    generation,
                    Config,
                    time,
                    NullLogger<EngineHost>.Instance
                );
                secondThread = second.StartOnDedicatedThread(stop.Token);
            },
            () => escalations++
        );
        using var first = new EngineHost(
            ports,
            new EngineGeneration(ledger.Generation),
            Config,
            time,
            NullLogger<EngineHost>.Instance
        );
        var firstThread = first.StartOnDedicatedThread(stop.Token);
        try
        {
            // 1. The first engine hangs outside the gate (an observer that does not answer).
            first.Post(new EngineEvent.ReleaseAll(ReleaseReason.User)).ShouldBeTrue();
            observer.WaitUntilHung(1);
            releaser.Check().ShouldBeNull();
            time.Advance(Timings.Engine.EngineStallThreshold);
            releaser.Check().ShouldBe(EmergencyOutcome.Released);
            second.ShouldNotBeNull();

            // 2. The engine that replaced it presses Shift and hangs too.
            gate.TryInject(ledger.Generation, [LowLevelInput.KeyDown(Shift)])
                .Result.ShouldBe(GateResult.Ran);
            second.Post(new EngineEvent.ReleaseAll(ReleaseReason.User)).ShouldBeTrue();
            observer.WaitUntilHung(2);

            // 3. The zombie resumes with nothing to send: its first heartbeat is fenced and it stops.
            observer.Resume(1);
            firstThread.Join(TimeSpan.FromSeconds(10)).ShouldBeTrue();
            first.IsStopped.ShouldBeTrue();

            // 4. The second hang is seen, inside the hang loop's window: the process restarts (Sentinel releases Shift).
            EmergencyOutcome? outcome = null;
            for (var i = 0; i < 40 && outcome is null; i++)
            {
                time.Advance(Timings.Engine.LedgerHeartbeatInterval);
                outcome = releaser.Check();
            }

            outcome.ShouldBe(EmergencyOutcome.GateBusy);
            escalations.ShouldBe(1);
            ledger.Marks.HasFlag(LedgerMarks.EmergencyRestart).ShouldBeTrue();
            ledger.Marks.HasFlag(LedgerMarks.CleanShutdown).ShouldBeFalse();
            system.Keys.ShouldBe([Shift]);
        }
        finally
        {
            observer.ResumeAll();
            stop.Cancel();
            firstThread.Join(TimeSpan.FromSeconds(10));
            secondThread?.Join(TimeSpan.FromSeconds(10));
            second?.Dispose();
        }
    }

    /// <summary>An observer whose n-th notice blocks its caller until the test resumes it.</summary>
    private sealed class HangingObserver : IEngineObserver, IDisposable
    {
        private readonly Lock _sync = new();
        private readonly Dictionary<
            int,
            (ManualResetEventSlim Hung, ManualResetEventSlim Resume)
        > _calls = [];
        private int _notices;

        public void WaitUntilHung(int call) =>
            Gates(call).Hung.Wait(TimeSpan.FromSeconds(10)).ShouldBeTrue();

        public void Resume(int call) => Gates(call).Resume.Set();

        public void ResumeAll()
        {
            lock (_sync)
            {
                foreach (var (_, resume) in _calls.Values)
                {
                    resume.Set();
                }
            }
        }

        public void OnNotice(Message text, NoticeUrgency urgency)
        {
            var (hung, resume) = Gates(Interlocked.Increment(ref _notices));
            hung.Set();
            resume.Wait(TimeSpan.FromSeconds(30));
        }

        public void OnSnapshot(EngineSnapshot snapshot) { }

        public void OnUsage(ShortcutId shortcut, DateTimeOffset at) { }

        public void OnLastAction(ShortcutId shortcut) { }

        public void Dispose()
        {
            lock (_sync)
            {
                foreach (var (hung, resume) in _calls.Values)
                {
                    hung.Dispose();
                    resume.Dispose();
                }
            }
        }

        private (ManualResetEventSlim Hung, ManualResetEventSlim Resume) Gates(int call)
        {
            lock (_sync)
            {
                if (!_calls.TryGetValue(call, out var gates))
                {
                    gates = (new ManualResetEventSlim(), new ManualResetEventSlim());
                    _calls[call] = gates;
                }

                return gates;
            }
        }
    }
}
