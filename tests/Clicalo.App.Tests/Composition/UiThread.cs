using System.Runtime.ExceptionServices;
using System.Windows.Threading;

namespace Clicalo.App.Tests.Composition;

/// <summary>
/// A UI thread of its own for one test of the composition: single-threaded apartment, with a dispatcher that the test
/// drains by hand, so what the composers queue for «later on the UI thread» runs when the test says. No window is ever
/// shown on it.
/// </summary>
internal static class UiThread
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    /// <summary>Runs <paramref name="test"/> on a new UI thread and rethrows what it throws.</summary>
    public static void Run(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(
                new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher)
            );
            try
            {
                test();
            }
#pragma warning disable CA1031 // Whatever the test throws goes back to the thread of the test.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        })
        {
            IsBackground = true,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    /// <summary>Runs what is queued on the dispatcher of this thread, whatever its priority.</summary>
    public static void Drain() =>
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.SystemIdle);

    /// <summary>Drains the dispatcher until <paramref name="task"/> ends and returns its result.</summary>
    public static T Wait<T>(Task<T> task)
    {
        var started = Environment.TickCount64;
        while (!task.IsCompleted)
        {
            (Environment.TickCount64 - started).ShouldBeLessThan(
                (long)Limit.TotalMilliseconds,
                "the task never ended"
            );
            Drain();
        }

        return task.GetAwaiter().GetResult();
    }
}
