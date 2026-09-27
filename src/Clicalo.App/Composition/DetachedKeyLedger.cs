using Clicalo.Application.Ports;

namespace Clicalo.App.Composition;

/// <summary>
/// The ledger of <c>--no-input</c>: nothing is ever pressed, so there is nothing to record and no guardian to read it.
/// It keeps the generation and the marks in memory so the engine and the exit sequence run unchanged.
/// </summary>
internal sealed class DetachedKeyLedger : IKeyLedger
{
    private int _marks;

    /// <inheritdoc />
    public EngineGeneration CurrentGeneration { get; } = new(1);

    /// <inheritdoc />
    public KeyLedgerMarks Marks => (KeyLedgerMarks)Volatile.Read(ref _marks);

    /// <inheritdoc />
    public int PendingReleases => 0;

    /// <inheritdoc />
    public void WriteHeartbeat(long ticks) { }

    /// <inheritdoc />
    public void SetMarks(KeyLedgerMarks marks) => _ = Interlocked.Or(ref _marks, (int)marks);

    /// <inheritdoc />
    public void ClearMarks(KeyLedgerMarks marks) => _ = Interlocked.And(ref _marks, ~(int)marks);
}
