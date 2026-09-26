using System.Collections.Concurrent;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// The SysEvents thread (blueprint §3.1, §3.2): an STA thread with AboveNormal priority, a message-only window and a
/// message loop. It hosts the WinEvent hooks (<see cref="ForegroundMonitor"/>), <c>RegisterHotKey</c>
/// (<c>Foreground.InternalRightsHotkey</c>), the tray (<c>Tray.TrayIcon</c>, <c>Tray.TrayMenuHost</c>) and, in the
/// app, the <c>ForegroundOrchestrator</c> actor. It only translates, enqueues and orchestrates the foreground: no
/// business logic and no call that can block.
/// </summary>
/// <remarks>
/// Work posted with <see cref="Post"/> or <see cref="InvokeAsync{T}"/> runs from the message loop, so it also runs
/// inside the modal loop of the tray menu. An exception thrown by work or by a message handler never crosses the
/// window procedure: <see cref="Post"/> and handler failures are reported through <see cref="UnhandledException"/>,
/// and <see cref="InvokeAsync{T}"/> puts them in its task.
/// </remarks>
public sealed class SysEventsThread : IDisposable
{
    /// <summary>
    /// Handles one message sent to <see cref="MessageWindow"/>; returns true when handled. Runs on the SysEvents
    /// thread and must not block.
    /// </summary>
    /// <param name="wParam">The message's <c>WPARAM</c>.</param>
    /// <param name="lParam">The message's <c>LPARAM</c>.</param>
    public delegate bool MessageHandler(nint wParam, nint lParam);

    private const uint WorkMessage = PInvoke.WM_APP + 1;

    private readonly ConcurrentQueue<Action> _work = new();
    private readonly Thread _thread;
    private SysEventsWindow? _messageWindow;
    private HWND _messageHandle;
    private int _managedThreadId = -1;
    private int _disposed;

    private SysEventsThread()
    {
        _thread = new Thread(Run)
        {
            Name = "Clicalo.SysEvents",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    /// <summary>
    /// Raised on the SysEvents thread when posted work or a message handler throws. The loop keeps running; the
    /// app logs it.
    /// </summary>
    public event EventHandler<UnhandledExceptionEventArgs>? UnhandledException;

    /// <summary>The message-only window (<c>HWND_MESSAGE</c>) that receives <c>WM_HOTKEY</c> and posted work.</summary>
    public nint MessageWindow => _messageHandle;

    /// <summary>True when the caller runs on the SysEvents thread.</summary>
    public bool CheckAccess() => Environment.CurrentManagedThreadId == _managedThreadId;

    /// <summary>Starts the thread and waits until its message window exists.</summary>
    public static SysEventsThread Start()
    {
        var thread = new SysEventsThread();
        using var ready = new ManualResetEventSlim();
        Exception? failure = null;
        thread._thread.Start(
            (Action<Exception?>)(
                error =>
                {
                    failure = error;
                    ready.Set();
                }
            )
        );
        ready.Wait();
        if (failure is not null)
        {
            throw new InvalidOperationException("The SysEvents thread could not start.", failure);
        }

        return thread;
    }

    /// <summary>Queues <paramref name="work"/> on the thread and returns at once.</summary>
    public void Post(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        _work.Enqueue(work);
        if (!PInvoke.PostMessage(_messageHandle, WorkMessage, default, default))
        {
            throw new ObjectDisposedException(
                nameof(SysEventsThread),
                "The SysEvents message loop has ended."
            );
        }
    }

    /// <summary>Runs <paramref name="work"/> on the thread and completes with its result.</summary>
    public Task<T> InvokeAsync<T>(Func<T> work)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (CheckAccess())
        {
            try
            {
                return Task.FromResult(work());
            }
            catch (Exception ex)
            {
                return Task.FromException<T>(ex);
            }
        }

        var completion = new TaskCompletionSource<T>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        Post(() =>
        {
            try
            {
                completion.SetResult(work());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    /// <summary>Runs <paramref name="work"/> on the thread and completes when it has run.</summary>
    public Task InvokeAsync(Action work)
    {
        ArgumentNullException.ThrowIfNull(work);
        return InvokeAsync(() =>
        {
            work();
            return true;
        });
    }

    /// <summary>
    /// Routes <paramref name="message"/> (for example <c>WM_HOTKEY</c> or the tray callback message) sent to
    /// <see cref="MessageWindow"/> to <paramref name="handler"/> until the returned registration is disposed.
    /// </summary>
    public IDisposable AddMessageHandler(uint message, MessageHandler handler)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _messageWindow!.AddHandler(message, handler);
    }

    /// <summary>Ends the message loop and joins the thread.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (CheckAccess())
        {
            PInvoke.PostQuitMessage(0);
            return;
        }

        _work.Enqueue(() => PInvoke.PostQuitMessage(0));
        if (PInvoke.PostMessage(_messageHandle, WorkMessage, default, default))
        {
            _thread.Join();
        }
    }

    /// <summary>Reports a failure of posted work or of a handler without ending the loop.</summary>
    internal void ReportUnhandled(Exception exception)
    {
        var handlers = UnhandledException;
        if (handlers is null)
        {
            return;
        }

        try
        {
            handlers(this, new UnhandledExceptionEventArgs(exception, isTerminating: false));
        }
        catch (Exception)
        {
            // A failing observer must not take the loop down with it.
        }
    }

    /// <summary>Creates a hidden top-level helper window on this thread (tray callback, tray menu host).</summary>
    internal SysEventsWindow CreateHiddenWindow() => SysEventsWindow.CreateHiddenTopLevel(this);

    private unsafe void Run(object? state)
    {
        var started = (Action<Exception?>)state!;
        _managedThreadId = Environment.CurrentManagedThreadId;
        try
        {
            _messageWindow = SysEventsWindow.CreateMessageOnly(this);
            _messageHandle = _messageWindow.Handle;
            _ = _messageWindow.AddHandler(
                WorkMessage,
                (_, _) =>
                {
                    Drain();
                    return true;
                }
            );
        }
        catch (Exception ex)
        {
            started(ex);
            return;
        }

        started(null);
        MSG message;
        while (PInvoke.GetMessage(&message, default, 0, 0).Value > 0)
        {
            _ = PInvoke.TranslateMessage(&message);
            _ = PInvoke.DispatchMessage(&message);
        }

        Drain();
        SysEventsWindow.DestroyAllOfThread();
    }

    private void Drain()
    {
        while (_work.TryDequeue(out var work))
        {
            try
            {
                work();
            }
            catch (Exception ex)
            {
                ReportUnhandled(ex);
            }
        }
    }
}
