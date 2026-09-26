using Clicalo.Application.Ports;
using Clicalo.Domain.Timing;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace Clicalo.Platform.Windows.Foreground;

/// <summary>
/// Adapter of <see cref="IInternalRightsHotkey"/> (blueprint §3.6): registers Ctrl+Alt+Shift+F24 (left modifiers,
/// <c>MOD_NOREPEAT</c>) with <c>RegisterHotKey</c> on the message window of the <see cref="SysEventsThread"/> and
/// completes the armed wait when its <c>WM_HOTKEY</c> arrives.
/// </summary>
/// <remarks>
/// The system consumes a registered hotkey: the final key (F24) never reaches the foreground app, and the
/// <c>WM_HOTKEY</c> delivered to Clícalo's thread makes it the process that received the last input, which is what
/// lets <c>SetForegroundWindow</c> succeed afterwards. The modifiers pressed before F24 do reach the foreground app as
/// key messages; spike S4 measures whether they have any effect there.
/// </remarks>
public sealed class InternalRightsHotkey : IInternalRightsHotkey, IDisposable
{
    /// <summary>The <c>RegisterHotKey</c> id of the reserved chord.</summary>
    public const int HotkeyId = 0x4C43;

    /// <summary>The virtual key of the reserved chord, <c>VK_F24</c>.</summary>
    public const uint VirtualKey = 0x87;

    /// <summary>The modifiers of the reserved chord, as <c>RegisterHotKey</c> takes them.</summary>
    internal const HOT_KEY_MODIFIERS Modifiers =
        HOT_KEY_MODIFIERS.MOD_CONTROL
        | HOT_KEY_MODIFIERS.MOD_ALT
        | HOT_KEY_MODIFIERS.MOD_SHIFT
        | HOT_KEY_MODIFIERS.MOD_NOREPEAT;

    private readonly Lock _gate = new();
    private readonly List<Waiter> _waiters = [];
    private IDisposable? _handler;
    private int _registered;
    private int _disposed;
    private long _arrivals;

    /// <summary>Creates the adapter on <paramref name="thread"/>; waits are bounded with <paramref name="timeProvider"/>.</summary>
    public InternalRightsHotkey(SysEventsThread thread, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Thread = thread;
        Clock = timeProvider;
    }

    /// <inheritdoc />
    public bool IsRegistered => Volatile.Read(ref _registered) != 0;

    /// <summary>The thread whose message window owns the registration.</summary>
    public SysEventsThread Thread { get; }

    /// <summary>Bounds the waits (<c>Timings.Foreground.RightsHotkeyTimeout</c>).</summary>
    public TimeProvider Clock { get; }

    /// <summary>Number of <c>WM_HOTKEY</c> messages of the reserved chord received so far (diagnostics, spike S4).</summary>
    public long Arrivals => Interlocked.Read(ref _arrivals);

    /// <summary>Registers the chord on the SysEvents thread; false if another program already owns it.</summary>
    public Task<bool> RegisterAsync()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return Thread.InvokeAsync(() =>
        {
            if (IsRegistered)
            {
                return true;
            }

            _handler ??= Thread.AddMessageHandler(PInvoke.WM_HOTKEY, OnHotkey);
            var registered = PInvoke.RegisterHotKey(
                (HWND)Thread.MessageWindow,
                HotkeyId,
                Modifiers,
                VirtualKey
            );
            Volatile.Write(ref _registered, registered ? 1 : 0);
            return (bool)registered;
        });
    }

    /// <inheritdoc />
    public ValueTask<bool> WaitForRightsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRegistered || Volatile.Read(ref _disposed) != 0)
        {
            return ValueTask.FromResult(false);
        }

        var waiter = new Waiter(this);
        lock (_gate)
        {
            _waiters.Add(waiter);
        }

        waiter.Arm(Clock, Timings.Foreground.RightsHotkeyTimeout, cancellationToken);
        return new ValueTask<bool>(waiter.Task);
    }

    /// <summary>Unregisters the chord and ends every pending wait with false.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        CompleteAll(arrived: false);
        try
        {
            Thread.Post(() =>
            {
                if (Interlocked.Exchange(ref _registered, 0) != 0)
                {
                    _ = PInvoke.UnregisterHotKey((HWND)Thread.MessageWindow, HotkeyId);
                }

                _handler?.Dispose();
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop has ended: its window, and with it the registration, is gone.
            Volatile.Write(ref _registered, 0);
        }
    }

    private bool OnHotkey(nint wParam, nint lParam)
    {
        if (wParam != HotkeyId)
        {
            return false;
        }

        Interlocked.Increment(ref _arrivals);
        CompleteAll(arrived: true);
        return true;
    }

    private void CompleteAll(bool arrived)
    {
        Waiter[] pending;
        lock (_gate)
        {
            pending = [.. _waiters];
            _waiters.Clear();
        }

        foreach (var waiter in pending)
        {
            waiter.Complete(arrived);
        }
    }

    private void Forget(Waiter waiter)
    {
        lock (_gate)
        {
            _ = _waiters.Remove(waiter);
        }
    }

    /// <summary>One armed wait: completed by <c>WM_HOTKEY</c>, its timeout or its cancellation, whichever comes first.</summary>
    private sealed class Waiter(InternalRightsHotkey owner)
    {
        private readonly TaskCompletionSource<bool> _completion = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private ITimer? _timeout;
        private CancellationTokenRegistration _cancellation;

        public Task<bool> Task => _completion.Task;

        public void Arm(TimeProvider clock, TimeSpan timeout, CancellationToken cancellationToken)
        {
            _timeout = clock.CreateTimer(
                static state => ((Waiter)state!).Complete(arrived: false),
                this,
                timeout,
                Timeout.InfiniteTimeSpan
            );
            _cancellation = cancellationToken.Register(
                static state => ((Waiter)state!).Cancel(),
                this
            );
            if (_completion.Task.IsCompleted)
            {
                Release();
            }
        }

        public void Complete(bool arrived)
        {
            if (_completion.TrySetResult(arrived))
            {
                Release();
            }
        }

        private void Cancel()
        {
            if (_completion.TrySetCanceled())
            {
                Release();
            }
        }

        private void Release()
        {
            owner.Forget(this);
            _timeout?.Dispose();
            _cancellation.Dispose();
        }
    }
}
