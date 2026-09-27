using System.Diagnostics.CodeAnalysis;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Persistence;
using Clicalo.Domain.Timing;

namespace Clicalo.App.Shutdown;

/// <summary>
/// The flush of the suspend (<c>PBT_APMSUSPEND</c>; blueprint §6.4 «vaciados síncronos de ambos: al suspender», §7.6
/// «Terminal(Suspend) y vaciar la persistencia»): after the release, the document, the usage and the queued backups are
/// written before the message is answered, since the machine may sleep as soon as it returns and the battery may run
/// out while it sleeps. The flush runs on the thread pool, never on the SysEvents thread that waits for it, and the
/// wait is bounded by <c>Timings.App.SuspendFlushTimeout</c>.
/// </summary>
internal static class SuspendFlush
{
    /// <summary>
    /// The suspend of the running instance: the release the engine confirms through <paramref name="relay"/>, then the
    /// flush of <paramref name="scheduler"/>, the single Persistence consumer (the same flush as the exit sequence's).
    /// </summary>
    /// <param name="relay">The engine's observer.</param>
    /// <param name="scheduler">The autosave.</param>
    /// <param name="time">The clock of the limit.</param>
    /// <returns>Whether the flush finished within the limit.</returns>
    public static bool Run(
        EngineObserverRelay relay,
        PersistenceScheduler scheduler,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(relay);
        ArgumentNullException.ThrowIfNull(scheduler);
        return Run(() => SuspendRelease.Wait(relay), scheduler.FlushAsync, time);
    }

    /// <summary>
    /// Runs <paramref name="release"/> (itself bounded), then <paramref name="flush"/>, and waits for the flush or for
    /// the limit, whichever comes first.
    /// </summary>
    /// <param name="release">The release the engine confirms (<see cref="SuspendRelease.Wait"/>).</param>
    /// <param name="flush">The flush (the same as the exit sequence's).</param>
    /// <param name="time">The clock of the limit.</param>
    /// <returns>Whether the flush finished within the limit.</returns>
    [SuppressMessage(
        "ApiDesign",
        "RS0030:Do not use banned APIs",
        Justification = "PBT_APMSUSPEND must be answered after the flush; the wait is bounded (app-suspend)."
    )]
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A flush that fails must never keep the suspend from being answered; the scheduler logs it."
    )]
    public static bool Run(Action release, Func<CancellationToken, Task> flush, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(release);
        ArgumentNullException.ThrowIfNull(flush);
        ArgumentNullException.ThrowIfNull(time);
        release();
        using var limit = new CancellationTokenSource(Timings.App.SuspendFlushTimeout, time);
        try
        {
            Task.Run(() => flush(limit.Token), limit.Token)
                .WaitAsync(limit.Token)
                .GetAwaiter()
                .GetResult();
            return true;
        }
        catch (OperationCanceledException)
        {
            // The limit: what is still pending is written on resume, or on the next change.
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return false;
        }
    }
}
