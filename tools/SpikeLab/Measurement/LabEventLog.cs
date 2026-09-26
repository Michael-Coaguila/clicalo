using System.Collections.Immutable;
using Clicalo.Tools.SpikeLab.Reporting;

namespace Clicalo.Tools.SpikeLab.Measurement;

/// <summary>
/// The timeline of the laboratory: bounded to <see cref="ReportContext.MaxEvents"/> entries (the oldest are dropped
/// and counted). Thread-safe: the probe and the SysEvents thread write to it too.
/// </summary>
internal sealed class LabEventLog
{
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Queue<LabLogEntry> _entries = new();
    private int _dropped;

    /// <summary>Creates an empty log stamped with <paramref name="time"/>.</summary>
    public LabEventLog(TimeProvider time) => _time = time;

    /// <summary>Entries dropped because the log was full.</summary>
    public int Dropped
    {
        get
        {
            lock (_gate)
            {
                return _dropped;
            }
        }
    }

    /// <summary>Adds an entry stamped now.</summary>
    /// <param name="kind">Short machine-readable kind.</param>
    /// <param name="detail">One sentence, without titles or typed text.</param>
    public void Add(string kind, string detail)
    {
        var entry = new LabLogEntry(_time.GetUtcNow(), kind, detail);
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > ReportContext.MaxEvents)
            {
                _entries.Dequeue();
                _dropped++;
            }
        }
    }

    /// <summary>A copy of the entries, oldest first.</summary>
    public ImmutableArray<LabLogEntry> Snapshot()
    {
        lock (_gate)
        {
            return [.. _entries];
        }
    }
}
