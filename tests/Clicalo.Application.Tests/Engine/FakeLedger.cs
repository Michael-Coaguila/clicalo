using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Engine;

/// <summary>The physical ledger's header as the host sees it: generation, marks and heartbeat.</summary>
internal sealed class FakeLedger : IKeyLedger
{
    public EngineGeneration CurrentGeneration { get; set; } = new(1);

    public KeyLedgerMarks Marks { get; private set; }

    public int PendingReleases => 0;

    public List<long> Heartbeats { get; } = [];

    public void WriteHeartbeat(long ticks) => Heartbeats.Add(ticks);

    public void SetMarks(KeyLedgerMarks marks) => Marks |= marks;

    public void ClearMarks(KeyLedgerMarks marks) => Marks &= ~marks;
}
