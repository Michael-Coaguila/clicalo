using System.Windows.Threading;

namespace Clicalo.TestKit.Windows.Rendering;

/// <summary>
/// One long-lived STA thread with a running WPF dispatcher, shared by every test of the process. xUnit runs tests on
/// thread-pool (MTA) threads, and WPF objects must be created and used on the STA thread that owns them.
/// </summary>
/// <remarks>Work is serialized on that thread, which also makes WPF rendering in parallel tests deterministic.</remarks>
public static class WpfThread
{
    private static readonly Lazy<Dispatcher> SharedDispatcher = new(
        Start,
        LazyThreadSafetyMode.ExecutionAndPublication
    );

    /// <summary>The dispatcher of the shared STA thread.</summary>
    public static Dispatcher Dispatcher => SharedDispatcher.Value;

    /// <summary>Runs <paramref name="callback"/> on the WPF thread and returns its result (exceptions propagate).</summary>
    public static T Invoke<T>(Func<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        return Dispatcher.CheckAccess() ? callback() : Dispatcher.Invoke(callback);
    }

    /// <summary>Runs <paramref name="callback"/> on the WPF thread (exceptions propagate).</summary>
    public static void Invoke(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        if (Dispatcher.CheckAccess())
        {
            callback();
        }
        else
        {
            Dispatcher.Invoke(callback);
        }
    }

    /// <summary>
    /// Lets the dispatcher run everything queued above <see cref="DispatcherPriority.Background"/> (layout,
    /// data binding, rendering, <c>Loaded</c>). Call it on the WPF thread before capturing a visual.
    /// </summary>
    public static void DrainPendingWork()
    {
        if (!Dispatcher.CheckAccess())
        {
            throw new InvalidOperationException(
                "DrainPendingWork must run on the WPF thread (use WpfThread.Invoke)."
            );
        }

        Dispatcher.Invoke(static () => { }, DispatcherPriority.Background);
    }

    private static Dispatcher Start()
    {
        using var started = new ManualResetEventSlim();
        Dispatcher? dispatcher = null;
        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            started.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Clicalo.TestKit WPF",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        started.Wait();
        return dispatcher!;
    }
}
