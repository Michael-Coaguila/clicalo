using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Engine;

/// <summary>
/// The physical ledger's header as the host sees it: generation, marks and heartbeat. Like the real gate, it writes the
/// heartbeat and the engine's marks only for the current generation (§3.2, rule 6).
/// </summary>
internal sealed class FakeLedger : IKeyLedger
{
    public EngineGeneration CurrentGeneration { get; set; } = HostWorld.Generation;

    public KeyLedgerMarks Marks { get; private set; }

    public int PendingReleases => 0;

    public List<long> Heartbeats { get; } = [];

    public bool TryWriteHeartbeat(EngineGeneration generation, long ticks)
    {
        if (generation != CurrentGeneration)
        {
            return false;
        }

        Heartbeats.Add(ticks);
        return true;
    }

    public bool TryUpdateMarks(
        EngineGeneration generation,
        KeyLedgerMarks toSet,
        KeyLedgerMarks toClear
    )
    {
        if (generation != CurrentGeneration)
        {
            return false;
        }

        SetMarks(toSet);
        ClearMarks(toClear);
        return true;
    }

    public void SetMarks(KeyLedgerMarks marks) => Marks |= marks;

    public void ClearMarks(KeyLedgerMarks marks) => Marks &= ~marks;
}
