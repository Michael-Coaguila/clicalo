using System.Diagnostics.CodeAnalysis;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Engine;

/// <summary>
/// The engine's two-lane mailbox (blueprint §3.2, rule 3): many writers, one reader (the engine thread).
/// <see cref="EngineLane.Priority"/> is always drained before <see cref="EngineLane.Normal"/>, so «Release all», the end
/// of a contact and terminal events never wait behind a macro.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the engine package implements it (docs/testing/spikes/M2-ownership.md)."
)]
public sealed class EngineMailbox
{
    /// <summary>Events waiting in both lanes.</summary>
    public int Count => throw new NotImplementedException();

    /// <summary>Queues an event in its lane; never blocks. <see langword="false"/> after <see cref="Complete"/>.</summary>
    /// <param name="engineEvent">The event.</param>
    public bool Post(EngineEvent engineEvent) => throw new NotImplementedException();

    /// <summary>Takes the next event: the oldest priority event, or else the oldest normal one.</summary>
    /// <param name="engineEvent">The event.</param>
    public bool TryTake([NotNullWhen(true)] out EngineEvent? engineEvent) =>
        throw new NotImplementedException();

    /// <summary>Waits until an event is queued, the timeout passes or the token is cancelled (the engine loop's only wait).</summary>
    /// <param name="timeout">Longest wait (the next timer or heartbeat).</param>
    /// <param name="cancellationToken">Stops the engine.</param>
    public bool WaitForEvent(TimeSpan timeout, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    /// <summary>Refuses further events.</summary>
    public void Complete() => throw new NotImplementedException();
}
