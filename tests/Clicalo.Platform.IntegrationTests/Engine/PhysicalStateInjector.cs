using Clicalo.Platform.Core.Injection;
using Clicalo.Platform.Core.KeyLedger;

namespace Clicalo.Platform.IntegrationTests.Engine;

/// <summary>
/// The model of what the system has down (<c>D</c> of blueprint §7.5), behind the real <see cref="InjectionGate"/>
/// (§7.10: «el modelo usa un InjectionGate real sobre un PhysicalStateInjector»). It never injects: it applies each
/// batch to its own state. It can answer like a partial or refused <c>SendInput</c>, run a probe before and after each
/// batch («death at every step»), and block inside one call to freeze the engine thread inside the gate (INV-11).
/// </summary>
internal sealed class PhysicalStateInjector : ILowLevelSender
{
    private readonly Lock _sync = new();
    private readonly HashSet<PhysicalKey> _keys = [];
    private int _calls;

    public LedgerMouseButtons Buttons { get; private set; }

    public IReadOnlySet<PhysicalKey> Keys
    {
        get
        {
            lock (_sync)
            {
                return _keys.ToHashSet();
            }
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_sync)
            {
                return _keys.Count == 0 && Buttons == LedgerMouseButtons.None;
            }
        }
    }

    public List<LowLevelInput[]> Batches { get; } = [];

    /// <summary>How many events the next call takes (then back to all); <see langword="null"/> for all.</summary>
    public int? TakeNext { get; set; }

    /// <summary>The error the next partial call reports.</summary>
    public int NextError { get; set; } = 87;

    /// <summary>Runs inside each call, before the batch is applied (the ledger already recorded its downs).</summary>
    public Action<PhysicalStateInjector, ReadOnlyMemory<LowLevelInput>>? BeforeApply { get; set; }

    /// <summary>Runs inside each call, after the batch is applied.</summary>
    public Action<PhysicalStateInjector, ReadOnlyMemory<LowLevelInput>>? AfterApply { get; set; }

    /// <summary>When set, the call with this number (1-based) blocks until <see cref="Resume"/>.</summary>
    public int? FreezeOnCall { get; set; }

    /// <summary>Whether the frozen call blocks after applying its batch (else before).</summary>
    public bool FreezeAfterApply { get; set; }

    public ManualResetEventSlim Frozen { get; } = new();

    public ManualResetEventSlim Resume { get; } = new();

    public SendResult Send(ReadOnlySpan<LowLevelInput> inputs)
    {
        var batch = inputs.ToArray();
        var call = Interlocked.Increment(ref _calls);
        var take = TakeNext is { } limit ? Math.Clamp(limit, 0, batch.Length) : batch.Length;
        TakeNext = null;
        lock (_sync)
        {
            Batches.Add(batch);
        }

        BeforeApply?.Invoke(this, batch);
        var freezes = FreezeOnCall == call;
        if (freezes && !FreezeAfterApply)
        {
            Freeze();
        }

        Apply(batch.AsSpan(0, take));
        AfterApply?.Invoke(this, batch);
        if (freezes && FreezeAfterApply)
        {
            Freeze();
        }

        return take == batch.Length ? new SendResult(take, 0) : new SendResult(take, NextError);
    }

    /// <summary>What the system would have down after <paramref name="batch"/> (the guardian's release, for example).</summary>
    public (HashSet<PhysicalKey> Keys, LedgerMouseButtons Buttons) After(
        ReadOnlySpan<LowLevelInput> batch
    )
    {
        lock (_sync)
        {
            var keys = _keys.ToHashSet();
            var buttons = Buttons;
            Apply(batch, keys, ref buttons);
            return (keys, buttons);
        }
    }

    private void Freeze()
    {
        Frozen.Set();
        Resume.Wait();
    }

    private void Apply(ReadOnlySpan<LowLevelInput> batch)
    {
        lock (_sync)
        {
            var buttons = Buttons;
            Apply(batch, _keys, ref buttons);
            Buttons = buttons;
        }
    }

    private static void Apply(
        ReadOnlySpan<LowLevelInput> batch,
        HashSet<PhysicalKey> keys,
        ref LedgerMouseButtons buttons
    )
    {
        foreach (var input in batch)
        {
            switch (input.Kind)
            {
                case LowLevelInputKind.KeyDown:
                    keys.Add(input.Key);
                    break;
                case LowLevelInputKind.KeyUp:
                    keys.Remove(input.Key);
                    break;
                case LowLevelInputKind.MouseButtonDown:
                    buttons |= input.Button;
                    break;
                case LowLevelInputKind.MouseButtonUp:
                    buttons &= ~input.Button;
                    break;
            }
        }
    }
}
