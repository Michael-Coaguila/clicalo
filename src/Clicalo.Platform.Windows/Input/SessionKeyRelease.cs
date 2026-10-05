using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clicalo.Application.Ports;
using Clicalo.Domain.Execution;
using Clicalo.Platform.Windows.SysEvents;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;

namespace Clicalo.Platform.Windows.Input;

/// <summary>
/// Releases on lock and suspend, and resends refused releases on unlock and resume (SEG-006, blueprint §7.6): a hidden
/// top-level window of the SysEvents thread hears <c>WM_WTSSESSION_CHANGE</c> and <c>WM_POWERBROADCAST</c> and posts
/// the matching terminal or <see cref="EngineEvent.SessionResumed"/> to the engine. A suspend is answered only after
/// <c>beforeSuspendReturns</c>, which waits a bounded time for the engine to confirm the release, since the machine may
/// sleep as soon as the message returns.
/// </summary>
/// <remarks>
/// The secure desktop of UAC and Ctrl+Alt+Del refuses releases without locking the session: an out-of-context
/// <c>EVENT_SYSTEM_DESKTOPSWITCH</c> hook hands every desktop switch to <see cref="InputDesktopWatch"/>, which tells
/// the engine as soon as the input desktop is Clícalo's again (D-22).
/// </remarks>
public sealed class SessionKeyRelease : IDisposable
{
    private static readonly ConcurrentDictionary<nint, SessionKeyRelease> DesktopHooks = new();

    /// <summary><c>WM_WTSSESSION_CHANGE</c>.</summary>
    public const uint SessionChangeMessage = 0x02B1;

    /// <summary><c>WM_POWERBROADCAST</c>.</summary>
    public const uint PowerBroadcastMessage = 0x0218;

    private const uint SessionLock = 0x7;
    private const uint SessionUnlock = 0x8;
    private const uint PowerSuspend = 0x4;
    private const uint PowerResumeSuspend = 0x7;
    private const uint PowerResumeAutomatic = 0x12;

    private readonly SysEventsThread _thread;
    private readonly SysEventsWindow _window;
    private readonly InputDesktopWatch _desktop;
    private readonly List<IDisposable> _handlers = [];
    private HWINEVENTHOOK _desktopHook;
    private int _disposed;

    private SessionKeyRelease(
        SysEventsThread thread,
        SysEventsWindow window,
        InputDesktopWatch desktop
    )
    {
        _thread = thread;
        _window = window;
        _desktop = desktop;
    }

    /// <summary>Starts listening on <paramref name="thread"/>.</summary>
    /// <param name="thread">The SysEvents thread.</param>
    /// <param name="engine">The engine mailbox.</param>
    /// <param name="beforeSuspendReturns">Runs after a suspend was posted, before the message is answered.</param>
    /// <param name="time">The timers of the input desktop's checks.</param>
    public static Task<SessionKeyRelease> StartAsync(
        SysEventsThread thread,
        IEngineInbox engine,
        Action beforeSuspendReturns,
        TimeProvider time
    )
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(beforeSuspendReturns);
        ArgumentNullException.ThrowIfNull(time);
        return thread.InvokeAsync(() =>
        {
            var window = SysEventsWindow.CreateHiddenTopLevel(thread);
            var release = new SessionKeyRelease(
                thread,
                window,
                new InputDesktopWatch(engine, InputDesktopWatch.InputDesktopIsReachable, time)
            );
            foreach (
                var message in (ReadOnlySpan<uint>)[SessionChangeMessage, PowerBroadcastMessage]
            )
            {
                var kind = message;
                release._handlers.Add(
                    window.AddHandler(
                        message,
                        (wParam, lParam) =>
                        {
                            if (Translate(kind, (nuint)wParam) is { } engineEvent)
                            {
                                _ = engine.Post(engineEvent);
                                if (
                                    engineEvent is EngineEvent.Terminal
                                    {
                                        Reason: TerminalReason.Suspend
                                    }
                                )
                                {
                                    beforeSuspendReturns();
                                }
                            }

                            // Never handled: the default procedure still answers Windows.
                            return false;
                        }
                    )
                );
            }

            // Without the registration only the console session's changes arrive; failing it keeps the suspend path.
            _ = PInvoke.WTSRegisterSessionNotification(
                window.Handle,
                PInvoke.NOTIFY_FOR_THIS_SESSION
            );
            release.InstallDesktopHook();
            return release;
        });
    }

    /// <summary>
    /// The engine event of a session or power message: lock and suspend release everything (a terminal), unlock and
    /// resume send again what the secure desktop refused; <see langword="null"/> for anything else.
    /// </summary>
    /// <param name="message">The window message.</param>
    /// <param name="wParam">Its <c>wParam</c>: the session change or the power event.</param>
    public static EngineEvent? Translate(uint message, nuint wParam) =>
        (message, (uint)wParam) switch
        {
            (SessionChangeMessage, SessionLock) => new EngineEvent.Terminal(TerminalReason.Lock),
            (SessionChangeMessage, SessionUnlock) => new EngineEvent.SessionResumed(),
            (PowerBroadcastMessage, PowerSuspend) => new EngineEvent.Terminal(
                TerminalReason.Suspend
            ),
            (PowerBroadcastMessage, PowerResumeSuspend or PowerResumeAutomatic) =>
                new EngineEvent.SessionResumed(),
            _ => null,
        };

    /// <summary>Stops listening; the window goes with the SysEvents thread if it has already ended.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _thread.Post(() =>
            {
                foreach (var handler in _handlers)
                {
                    handler.Dispose();
                }

                _ = PInvoke.WTSUnRegisterSessionNotification(_window.Handle);
                UninstallDesktopHook();
                _window.Dispose();
            });
        }
        catch (ObjectDisposedException)
        {
            // The SysEvents loop ended first and destroyed its windows (SysEventsWindow.DestroyAllOfThread) and hooks.
            _ = DesktopHooks.TryRemove(_desktopHook, out _);
        }

        _desktop.Dispose();
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void OnDesktopSwitch(
        HWINEVENTHOOK hook,
        uint eventType,
        HWND window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime
    )
    {
        if (!DesktopHooks.TryGetValue(hook, out var release))
        {
            return;
        }

        try
        {
            release._desktop.OnDesktopSwitched();
        }
        catch (Exception ex)
        {
            // Never across the unmanaged boundary.
            release._thread.ReportUnhandled(ex);
        }
    }

    private unsafe void InstallDesktopHook()
    {
        _desktopHook = PInvoke.SetWinEventHook(
            PInvoke.EVENT_SYSTEM_DESKTOPSWITCH,
            PInvoke.EVENT_SYSTEM_DESKTOPSWITCH,
            default(HMODULE),
            &OnDesktopSwitch,
            0,
            0,
            PInvoke.WINEVENT_OUTOFCONTEXT
        );

        // Without the hook, unlock and resume still send the refused releases again, as do «Release all» and the
        // terminal events.
        if (!_desktopHook.IsNull)
        {
            DesktopHooks[_desktopHook] = this;
        }
    }

    private void UninstallDesktopHook()
    {
        if (_desktopHook.IsNull)
        {
            return;
        }

        _ = DesktopHooks.TryRemove(_desktopHook, out _);
        _ = PInvoke.UnhookWinEvent(_desktopHook);
        _desktopHook = default;
    }
}
