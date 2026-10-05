using System.Collections.Concurrent;

namespace Clicalo.Application.Engine;

/// <summary>
/// The results of the internal chords the engine sends for others (blueprint §3.6): a requester registers, posts
/// <c>EngineEvent.InternalChordRequested</c> with its number and waits; the engine thread answers after the gate ran.
/// Thread-safe: requesters run on the thread pool or SysEvents, the answer comes from the engine thread, and the
/// continuation never runs on the engine thread.
/// </summary>
public sealed class InternalChordReplies
{
    private readonly ConcurrentDictionary<long, TaskCompletionSource<bool>> _pending = new();
    private long _last;

    /// <summary>How many requests wait for their answer.</summary>
    public int Pending => _pending.Count;

    /// <summary>Registers a request.</summary>
    /// <returns>Its number and the task its answer completes.</returns>
    public (long Request, Task<bool> Reply) Register()
    {
        var request = Interlocked.Increment(ref _last);
        var reply = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        _pending[request] = reply;
        return (request, reply.Task);
    }

    /// <summary>Answers a request (the engine thread, after the gate ran); an abandoned one is ignored.</summary>
    /// <param name="request">The request's number.</param>
    /// <param name="sent">Whether the chord went.</param>
    public void Complete(long request, bool sent)
    {
        if (_pending.TryRemove(request, out var reply))
        {
            _ = reply.TrySetResult(sent);
        }
    }

    /// <summary>Drops a request its requester no longer waits for.</summary>
    /// <param name="request">The request's number.</param>
    public void Forget(long request) => _ = _pending.TryRemove(request, out _);
}
