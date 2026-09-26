using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Clicalo.Application.Ports;
using Clicalo.Domain.Keys;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;
using Clicalo.Platform.Windows.Input;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// A seeded scenario of the engine's key operations through the real physical path (blueprint §7.10, items 2 and 3):
/// the Domain's logical ledger decides the events, the adapter maps them, the real <see cref="InjectionGate"/> records
/// them in a real (in-memory) ledger v2 and a <see cref="PhysicalStateInjector"/> plays the system. Holders share keys,
/// both modes mix, mouse buttons come and go, and <c>SendInput</c> sometimes takes only part of a batch or is refused
/// by the secure desktop.
/// </summary>
internal sealed class LedgerScenario : IDisposable
{
    private static readonly InjectedKey[] KeyPool =
    [
        new(0xA2, 0x1D, false, InjectionMode.VirtualKey),
        new(0xA0, 0x2A, false, InjectionMode.VirtualKey),
        new(0xA4, 0x38, false, InjectionMode.VirtualKey),
        new(0x5B, 0x5B, true, InjectionMode.VirtualKey),
        new(0x41, 0x1E, false, InjectionMode.VirtualKey),
        new(0x25, 0x4B, true, InjectionMode.VirtualKey),
        new(0, 0x1D, false, InjectionMode.ScanCode),
        new(0, 0x2A, false, InjectionMode.ScanCode),
        new(0, 0x38, true, InjectionMode.ScanCode),
        new(0, 0x5B, true, InjectionMode.ScanCode),
        new(0, 0x1E, false, InjectionMode.ScanCode),
    ];

    private readonly Random _random;
    private readonly StringBuilder _log = new();

    public LedgerScenario(int seed)
    {
        Seed = seed;
        _random = new Random(seed);
        Ledger = KeyLedgerSection.CreateInMemory();
        System = new PhysicalStateInjector();
        Gate = new InjectionGate(Ledger, System);
        Injector = new GateInputInjector(Gate);
    }

    public int Seed { get; }

    public KeyLedgerSection Ledger { get; }

    public PhysicalStateInjector System { get; }

    public InjectionGate Gate { get; }

    public GateInputInjector Injector { get; }

    public KeyboardLedger Logical { get; set; } = KeyboardLedger.Empty;

    public ulong Generation { get; set; } = 1;

    public string Log => "seed " + Seed.ToString(CultureInfo.InvariantCulture) + ": " + _log;

    /// <summary>The next operation's logical transition, not sent yet.</summary>
    public LedgerTransition NextTransition(long step)
    {
        var holder = new HolderId("h" + _random.Next(5).ToString(CultureInfo.InvariantCulture));
        var roll = _random.Next(100);
        LedgerTransition transition;
        if (roll < 30 && !Logical.Items.ContainsKey(holder))
        {
            var keys = Enumerable
                .Range(0, 1 + _random.Next(3))
                .Select(_ => KeyPool[_random.Next(KeyPool.Length)])
                .ToArray();
            var buttons = _random.Next(6) switch
            {
                0 => MouseButtons.Left,
                1 => MouseButtons.Right,
                _ => MouseButtons.None,
            };
            transition = Logical.Acquire(Item(holder, step, keys) with { Buttons = buttons });
            _log.Append(
                CultureInfo.InvariantCulture,
                $"acquire({holder},{keys.Length},{buttons}) "
            );
        }
        else if (roll < 50)
        {
            transition = Logical.Press(Item(holder, step), KeyPool[_random.Next(KeyPool.Length)]);
            _log.Append(CultureInfo.InvariantCulture, $"press({holder}) ");
        }
        else if (roll < 65 && Logical.Items.TryGetValue(holder, out var item) && !item.Keys.IsEmpty)
        {
            transition = Logical.Lift(holder, item.Keys[_random.Next(item.Keys.Count)]);
            _log.Append(CultureInfo.InvariantCulture, $"lift({holder}) ");
        }
        else if (roll < 95)
        {
            transition = Logical.Release(holder);
            _log.Append(CultureInfo.InvariantCulture, $"release({holder}) ");
        }
        else
        {
            transition = Logical.ReleaseAll();
            _log.Append("releaseAll ");
        }

        return transition;
    }

    /// <summary>Makes <c>SendInput</c> take only part of the next batch, or refuse it like the secure desktop, now and then.</summary>
    public void MaybeFailNextSend(ImmutableArray<InjectedEvent> events)
    {
        var roll = _random.Next(20);
        if (roll == 0)
        {
            System.TakeNext = _random.Next(Math.Max(1, InputMapping.CountOf(events.AsSpan())));
            System.NextError = 87;
            _log.Append("(partial) ");
        }
        else if (roll == 1)
        {
            System.TakeNext = 0;
            System.NextError = InjectionGate.AccessDenied;
            _log.Append("(blocked) ");
        }
    }

    /// <summary>Sends a transition's events as the engine would, and adopts its ledger.</summary>
    public InjectionResult Send(LedgerTransition transition, ulong generation)
    {
        Logical = transition.Ledger;
        return transition.Events.IsEmpty
            ? new InjectionResult(InjectionStatus.Sent, 0, 0)
            : Injector.Send(new EngineGeneration(generation), transition.Events.AsSpan());
    }

    /// <summary>
    /// «Death at every step»: if the process died right now, the guardian's release of what the ledger records
    /// leaves nothing down (INV-2: every key down is recorded).
    /// </summary>
    public void ShouldSurviveDeathNow(string where)
    {
        var release = LedgerRelease.BuildReleaseBatch(Ledger.Snapshot());
        var (keys, buttons) = System.After(release.AsSpan());
        keys.ShouldBeEmpty(Log + where);
        buttons.ShouldBe(LedgerMouseButtons.None, Log + where);
    }

    /// <summary>INV-2: what the system has down is recorded.</summary>
    public void ShouldCoverWhatIsDown()
    {
        var recorded = Ledger.Snapshot().Slots.Select(static s => s.Key).ToHashSet();
        foreach (var key in System.Keys)
        {
            recorded.ShouldContain(key, Log);
        }

        (System.Buttons & ~Ledger.MouseButtons).ShouldBe(LedgerMouseButtons.None, Log);
    }

    public void Dispose() => Ledger.Dispose();

    private static PressedItem Item(HolderId holder, long since, params InjectedKey[] keys) =>
        new(
            holder,
            HoldOrigin.Contact,
            new ShortcutId(holder.Value),
            null,
            [.. keys],
            MouseButtons.None,
            since,
            null
        );
}
