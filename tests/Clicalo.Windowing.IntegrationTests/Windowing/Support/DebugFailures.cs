using System.Collections.Concurrent;
using System.Diagnostics;

namespace Clicalo.Windowing.IntegrationTests.Windowing.Support;

/// <summary>
/// Records the <c>Debug.Fail</c> calls of <c>ActivationGuard</c> while a test forces violations on purpose. In a Debug
/// build (<c>cl test</c>, <c>cl desk</c> and the CI desktop job) the guard stops the run on a violation; this scope
/// replaces the trace listeners with a recorder, so the expected ones are counted instead. In Release the calls are
/// compiled out and nothing is recorded. Scopes are exclusive: the listeners belong to the whole process.
/// </summary>
public sealed class DebugFailures : IDisposable
{
    private static readonly SemaphoreSlim Exclusive = new(1, 1);

    private readonly TraceListener[] _previous;
    private readonly Recorder _recorder = new();
    private bool _disposed;

    private DebugFailures()
    {
        var listeners = Trace.Listeners;
        lock (listeners)
        {
            _previous = [.. listeners.Cast<TraceListener>()];
            listeners.Clear();
            listeners.Add(_recorder);
        }
    }

    /// <summary>True when the product assemblies were built with <c>DEBUG</c>, so <c>Debug.Fail</c> is live.</summary>
    public static bool AreLive =>
#if DEBUG
        true;
#else
        false;
#endif

    /// <summary>The messages of the failures recorded so far.</summary>
    public IReadOnlyList<string> Messages => [.. _recorder.Messages];

    /// <summary>Starts recording, waiting for any other scope to end.</summary>
    public static DebugFailures Capture()
    {
        Exclusive.Wait();
        try
        {
            return new DebugFailures();
        }
        catch
        {
            Exclusive.Release();
            throw;
        }
    }

    /// <summary>Puts the previous listeners back.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        var listeners = Trace.Listeners;
        lock (listeners)
        {
            listeners.Remove(_recorder);
            listeners.AddRange(_previous);
        }

        _recorder.Dispose();
        Exclusive.Release();
    }

    private sealed class Recorder : TraceListener
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public override void Fail(string? message) => Messages.Enqueue(message ?? string.Empty);

        public override void Fail(string? message, string? detailMessage) => Fail(message);

        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }
    }
}
