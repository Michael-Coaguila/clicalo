using Clicalo.Application.Engine;
using Microsoft.Extensions.Logging;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// The dedicated engine thread (blueprint §3.2): <c>AboveNormal</c>, it runs <see cref="EngineHost.Run"/> and nothing
/// else, so the engine accepts touches from the first frame and never shares a thread with the UI or with I/O. A
/// background thread: a hung engine never keeps the process alive («Soltar todo» of the tray and Sentinel cover the keys, ADR-0022).
/// </summary>
internal sealed partial class EngineThread(ILogger<EngineThread> logger)
{
    private readonly TaskCompletionSource _stopped = new(
        TaskCreationOptions.RunContinuationsAsynchronously
    );
    private int _started;

    /// <summary>Completes when the engine loop has ended (after <c>Terminal(Exit)</c> or the stop token).</summary>
    public Task Stopped => _stopped.Task;

    /// <summary>Starts the loop of <paramref name="host"/>.</summary>
    /// <param name="host">The engine.</param>
    /// <param name="cancellationToken">Stops the loop after releasing everything.</param>
    public void Start(EngineHost host, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The engine thread runs one engine.");
        }

        var thread = new Thread(() => Run(host, cancellationToken))
        {
            Name = "Clicalo.Engine",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        thread.Start();
    }

    private void Run(EngineHost host, CancellationToken cancellationToken)
    {
        try
        {
            host.Run(cancellationToken);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The host catches every message's exception itself (NFR-005); reaching here is a defect of the loop.
            // Sentinel still releases whatever Windows reports down when the process ends.
            var failure = ex.GetType().Name;
            LogEngineLoopFailed(logger, failure);
        }
        finally
        {
            _stopped.TrySetResult();
        }
    }

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Critical,
        Message = "engine.loop_failed ({Exception})"
    )]
    private static partial void LogEngineLoopFailed(ILogger logger, string exception);
}
