using System.Diagnostics.CodeAnalysis;
using System.Windows;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;

namespace Clicalo.UI.Wpf.Windowing;

/// <summary>
/// The only base class allowed for the panel surfaces (blueprint §3.5, ADR-0005, REG-01): panel, bar and handle,
/// bubble, side windows, menus and floating notices. A surface never takes the foreground or the keyboard focus
/// unless the orchestrator grants a <c>TextInput</c> or <c>KeyboardNavigation</c> lease on it.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="OnSourceInitialized"/> is sealed and applies everything BEFORE the first show:
/// <c>WS_EX_NOACTIVATE | WS_EX_TOPMOST</c>; the hidden <see cref="OwnerAnchor"/> as owner instead of
/// <c>WS_EX_TOOLWINDOW</c> (out of Alt+Tab and the taskbar, still a normal window for UI Automation);
/// <c>SetWindowFeedbackSetting</c> without touch feedback (<c>Pointer.PointerSetup</c>); the common
/// <c>HwndSource</c> hook; and registration in <see cref="SurfaceRegistry"/> and <see cref="ActivationGuard"/>.
/// Derived classes continue in <see cref="OnSurfaceInitialized"/>.
/// </para>
/// <para>
/// The common hook: <c>WM_MOUSEACTIVATE</c> → <c>MA_NOACTIVATE</c>; <c>WM_POINTERACTIVATE</c> →
/// <c>PA_NOACTIVATE</c>; <c>WM_WINDOWPOSCHANGING</c> adds <c>SWP_NOACTIVATE</c> except during a lease on this
/// surface; <c>WM_ACTIVATE</c> (not <c>WA_INACTIVE</c>), <c>WM_NCACTIVATE(TRUE)</c> and <c>WM_ACTIVATEAPP(TRUE)</c>
/// → <see cref="ActivationGuard.OnActivated"/>; <c>WM_DPICHANGED</c> applies the suggested rectangle with
/// <c>SWP_NOZORDER | SWP_NOACTIVATE</c> and is marked handled (#7561); <c>WM_GETDPISCALEDSIZE</c> returns the
/// surface's own size. The non-clickable shadow margin (<c>WM_NCHITTEST</c>) is decided in spike S6.
/// </para>
/// <para>
/// Showing is only <see cref="ShowPassive"/>: <c>Show()</c>, <c>ShowDialog()</c>, <c>Activate()</c>, <c>Focus()</c>
/// and <c>ShowActivated = true</c> are compile errors in derived classes (CLC0001), and <c>Window.Activate</c> and
/// <c>UIElement.Focus</c> are banned everywhere (RS0030). <c>Popup</c>, <c>ContextMenu</c>, interactive
/// <c>ToolTip</c> and <c>ComboBox</c> are banned on surfaces: menus and drop-downs are child
/// <see cref="NonActivatingWindow"/>s.
/// </para>
/// <para>CLC0001 binds to this type by its metadata name: renaming or moving it switches the rule off silently.</para>
/// </remarks>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality",
    Justification = "M1 contract stub: the windowing package implements it (docs/testing/spikes/M1-ownership.md)."
)]
public abstract class NonActivatingWindow : Window
{
    /// <summary>
    /// Creates the surface <paramref name="id"/>. <paramref name="registry"/> provides the owner anchor and the
    /// activation guard, and receives the surface once its handle exists.
    /// </summary>
    /// <param name="id">Identity of the surface.</param>
    /// <param name="registry">The registry of every surface of the process.</param>
    protected NonActivatingWindow(SurfaceId id, SurfaceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        Id = id;
        Registry = registry;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
    }

    /// <summary>Identity of the surface.</summary>
    public SurfaceId Id { get; }

    /// <summary>The surface window once its handle exists; <see cref="WindowToken.None"/> before.</summary>
    public WindowToken SurfaceWindow => throw new NotImplementedException("M1 windowing package.");

    /// <summary>The registry this surface belongs to (owner anchor and activation guard included).</summary>
    protected SurfaceRegistry Registry { get; }

    /// <summary>
    /// Shows the surface without activating it: creates the handle if needed (without showing it), then
    /// <c>SetWindowPos(HWND_TOPMOST, SWP_SHOWWINDOW | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE)</c>. Never changes
    /// <c>GetForegroundWindow</c> or the keyboard focus.
    /// </summary>
    public void ShowPassive() => throw new NotImplementedException("M1 windowing package.");

    /// <summary>Hides the surface without activating anything (<c>SWP_HIDEWINDOW | SWP_NOACTIVATE</c>).</summary>
    public void HidePassive() => throw new NotImplementedException("M1 windowing package.");

    /// <summary>
    /// Moves and resizes the surface to <paramref name="bounds"/> in physical screen pixels
    /// (<c>SWP_NOACTIVATE | SWP_NOZORDER</c>); WPF recomputes its DPI if the monitor changes (ACC-008).
    /// </summary>
    public void MovePassive(PhysicalRect bounds) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>Applies the non-activation contract before the first show; sealed so no surface can skip it.</summary>
    protected sealed override void OnSourceInitialized(EventArgs e) =>
        throw new NotImplementedException("M1 windowing package.");

    /// <summary>
    /// Called at the end of <see cref="OnSourceInitialized"/>, once the handle has every non-activation setting,
    /// for surface-specific setup such as attaching <c>Pointer.PointerInputSource</c>.
    /// </summary>
    protected virtual void OnSurfaceInitialized() { }
}
