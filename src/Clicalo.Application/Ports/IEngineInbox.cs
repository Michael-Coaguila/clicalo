using Clicalo.Domain.Execution;

namespace Clicalo.Application.Ports;

/// <summary>Where other threads deliver events to the engine (results of the Shell thread and the clipboard).</summary>
public interface IEngineInbox
{
    /// <summary>Queues an event in its lane; never blocks. Returns <see langword="false"/> after the engine stopped.</summary>
    /// <param name="engineEvent">The event.</param>
    bool Post(EngineEvent engineEvent);
}
