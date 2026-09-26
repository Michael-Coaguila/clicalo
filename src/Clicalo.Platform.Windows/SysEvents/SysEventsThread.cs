using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Platform.Windows.SysEvents;

/// <summary>
/// The SysEvents thread (blueprint §3.1, §3.2): an STA thread with AboveNormal priority, a message-only window and a
/// message loop. It hosts the WinEvent hooks (<see cref="ForegroundMonitor"/>), <c>RegisterHotKey</c>
/// (<c>Foreground.InternalRightsHotkey</c>), the tray (<c>Tray.TrayIcon</c>, <c>Tray.TrayMenuHost</c>) and, in the
/// app, the <c>ForegroundOrchestrator</c> actor. It only translates, enqueues and orchestrates the foreground: no
/// business logic and no call that can block.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the foreground package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public sealed class SysEventsThread : IDisposable
{
    /// <summary>
    /// Handles one message sent to <see cref="MessageWindow"/>; returns true when handled. Runs on the SysEvents
    /// thread and must not block.
    /// </summary>
    /// <param name="wParam">The message's <c>WPARAM</c>.</param>
    /// <param name="lParam">The message's <c>LPARAM</c>.</param>
    public delegate bool MessageHandler(nint wParam, nint lParam);

    private SysEventsThread() { }

    /// <summary>The message-only window (<c>HWND_MESSAGE</c>) that receives <c>WM_HOTKEY</c> and posted work.</summary>
    public nint MessageWindow => throw new NotImplementedException("M1 foreground package.");

    /// <summary>True when the caller runs on the SysEvents thread.</summary>
    public bool CheckAccess() => throw new NotImplementedException("M1 foreground package.");

    /// <summary>Starts the thread and waits until its message window exists.</summary>
    public static SysEventsThread Start() =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Queues <paramref name="work"/> on the thread and returns at once.</summary>
    public void Post(Action work) => throw new NotImplementedException("M1 foreground package.");

    /// <summary>Runs <paramref name="work"/> on the thread and completes with its result.</summary>
    public Task<T> InvokeAsync<T>(Func<T> work) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>
    /// Routes <paramref name="message"/> (for example <c>WM_HOTKEY</c> or the tray callback message) sent to
    /// <see cref="MessageWindow"/> to <paramref name="handler"/> until the returned registration is disposed.
    /// </summary>
    public IDisposable AddMessageHandler(uint message, MessageHandler handler) =>
        throw new NotImplementedException("M1 foreground package.");

    /// <summary>Ends the message loop and joins the thread.</summary>
    public void Dispose() { }
}
