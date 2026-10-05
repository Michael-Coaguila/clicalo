using System.Diagnostics;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The engine mailbox of the panel tests: it records every event with the time it was posted, on the clock the pointer
/// layer stamps its frames with (the end of the «touch → engine» leg of the latency budget), and sends nothing anywhere.
/// </summary>
internal sealed class PanelEngineInbox : IEngineInbox
{
    private readonly List<(
        EngineEvent Event,
        DateTimeOffset PostedAt,
        long PostedTimestamp
    )> _posted = [];
    private readonly Lock _gate = new();

    /// <summary>Every event posted, in order.</summary>
    public IReadOnlyList<EngineEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _posted.Select(static posted => posted.Event)];
            }
        }
    }

    /// <summary>
    /// Every event with the time it was posted, on <see cref="TimeProvider.System"/> and on the performance counter
    /// (<see cref="Stopwatch"/>).
    /// </summary>
    public IReadOnlyList<(EngineEvent Event, DateTimeOffset PostedAt, long PostedTimestamp)> Posted
    {
        get
        {
            lock (_gate)
            {
                return [.. _posted];
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _posted.Count;
            }
        }
    }

    public bool Post(EngineEvent engineEvent)
    {
        lock (_gate)
        {
            _posted.Add((engineEvent, TimeProvider.System.GetUtcNow(), Stopwatch.GetTimestamp()));
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _posted.Clear();
        }
    }
}
