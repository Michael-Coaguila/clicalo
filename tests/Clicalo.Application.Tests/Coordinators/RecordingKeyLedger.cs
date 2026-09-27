using Clicalo.Application.Ports;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>A key ledger that only keeps its marks and writes every call to a shared log.</summary>
internal sealed class RecordingKeyLedger(List<string> log) : IKeyLedger
{
    public EngineGeneration CurrentGeneration { get; } = new(1);

    public KeyLedgerMarks Marks { get; private set; }

    public int PendingReleases => 0;

    public bool TryWriteHeartbeat(EngineGeneration generation, long ticks) =>
        generation == CurrentGeneration;

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

    public void SetMarks(KeyLedgerMarks marks)
    {
        Marks |= marks;
        log.Add("marks +" + marks);
    }

    public void ClearMarks(KeyLedgerMarks marks)
    {
        Marks &= ~marks;
        log.Add("marks -" + marks);
    }
}
