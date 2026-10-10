using Clicalo.Application.Ports;
using Clicalo.Platform.Windows.PointerTracking;
using Clicalo.Platform.Windows.SysEvents;

namespace Clicalo.Platform.Windows.Hotkeys;

/// <summary>
/// <see cref="IMenuCancelSignals"/> on Windows (CUA-014): Esc through <see cref="MenuEscapeHotkey"/> and the pointer
/// outside Clícalo through the <see cref="PointerPositionTracker"/> that the mouse actions already use (EJE-009). No
/// keyboard or mouse hook, and nothing takes the foreground (REG-01). Both events are raised on the SysEvents thread.
/// </summary>
public sealed class MenuCancelSignals : IMenuCancelSignals, IDisposable
{
    private readonly MenuEscapeHotkey _escape;
    private readonly PointerPositionTracker _pointer;

    /// <summary>Joins the Esc hotkey of <paramref name="thread"/> and <paramref name="tracker"/>.</summary>
    /// <param name="thread">The SysEvents thread.</param>
    /// <param name="tracker">The tracker of the pointer outside Clícalo.</param>
    public MenuCancelSignals(SysEventsThread thread, PointerPositionTracker tracker)
    {
        ArgumentNullException.ThrowIfNull(thread);
        ArgumentNullException.ThrowIfNull(tracker);
        _escape = new MenuEscapeHotkey(thread);
        _pointer = tracker;
        _escape.Pressed += OnEscape;
        _pointer.MovedOutside += OnMovedOutside;
    }

    /// <inheritdoc />
    public event EventHandler? EscapePressed;

    /// <inheritdoc />
    public event EventHandler? PointerWentOutside;

    /// <inheritdoc />
    public void WatchEscape(bool watch) => _escape.Watch(watch);

    /// <inheritdoc />
    public void Dispose()
    {
        _escape.Pressed -= OnEscape;
        _pointer.MovedOutside -= OnMovedOutside;
        _escape.Dispose();
    }

    private void OnEscape(object? sender, EventArgs e) => EscapePressed?.Invoke(this, e);

    private void OnMovedOutside(object? sender, EventArgs e) => PointerWentOutside?.Invoke(this, e);
}
