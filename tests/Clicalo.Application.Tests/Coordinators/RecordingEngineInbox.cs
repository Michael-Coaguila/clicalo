using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Tests.Coordinators;

/// <summary>An engine mailbox that records what it receives; after <see cref="Stop"/> it refuses, like a stopped engine.</summary>
internal sealed class RecordingEngineInbox : IEngineInbox
{
    private readonly List<EngineEvent> _events = [];
    private readonly Lock _gate = new();
    private bool _stopped;

    /// <summary>Called for every event, before it is recorded; may throw to simulate a failing engine.</summary>
    public Action<EngineEvent>? OnPost { get; set; }

    public IReadOnlyList<EngineEvent> Events
    {
        get
        {
            lock (_gate)
            {
                return [.. _events];
            }
        }
    }

    public bool Post(EngineEvent engineEvent)
    {
        OnPost?.Invoke(engineEvent);
        lock (_gate)
        {
            if (_stopped)
            {
                return false;
            }

            _events.Add(engineEvent);
            return true;
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _stopped = true;
        }
    }
}
