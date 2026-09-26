using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Interop;
using Clicalo.Application.Ports;
using Clicalo.Domain.Geometry;
using Clicalo.UI.Wpf.Pointer;
using Clicalo.UI.Wpf.Windowing.Internal;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

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
/// → <see cref="ActivationGuard.OnActivated"/>; <c>WM_DPICHANGED</c> is handled: WPF rescales inside an activation
/// veto and the rectangle is applied with <c>SWP_NOZORDER | SWP_NOACTIVATE</c> (#7561); <c>WM_GETDPISCALEDSIZE</c>
/// returns the surface's own size; <c>WM_NCHITTEST</c> answers <c>HTNOWHERE</c> inside <see cref="ShadowMargin"/>
/// (letting the click through to the window below is decided in spike S6).
/// </para>
/// <para>
/// Showing is only <see cref="ShowPassive"/>: <c>Show()</c>, <c>ShowDialog()</c>, <c>Activate()</c>, <c>Focus()</c>
/// and <c>ShowActivated = true</c> are compile errors in derived classes (CLC0001), and <c>Window.Activate</c> and
/// <c>UIElement.Focus</c> are banned everywhere (RS0030). <c>Popup</c>, <c>ContextMenu</c>, interactive
/// <c>ToolTip</c> and <c>ComboBox</c> are banned on surfaces: menus and drop-downs are child
/// <see cref="NonActivatingWindow"/>s. Every member runs on the thread that created the surface, except
/// <see cref="SurfaceWindow"/> and <see cref="Id"/>, which any thread may read.
/// </para>
/// <para>CLC0001 binds to this type by its metadata name: renaming or moving it switches the rule off silently.</para>
/// </remarks>
public abstract class NonActivatingWindow : Window
{
    private nint _handle;

    /// <summary>
    /// Creates the surface <paramref name="id"/>. <paramref name="registry"/> provides the owner anchor and the
    /// activation guard, and receives the surface once its handle exists.
    /// </summary>
    /// <param name="id">Identity of the surface.</param>
    /// <param name="registry">The registry of every surface of the process.</param>
    protected NonActivatingWindow(SurfaceId id, SurfaceRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);

        // Owner and owned windows on different threads share one input queue (an implicit AttachThreadInput, §3.6).
        registry.Dispatcher.VerifyAccess();
        Id = id;
        Registry = registry;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;

        // Owned from creation: WPF passes the owner to CreateWindowEx, so the window is never unowned.
        new WindowInteropHelper(this).Owner = registry.Anchor.EnsureCreated().Handle;
    }

    /// <summary>Identity of the surface.</summary>
    public SurfaceId Id { get; }

    /// <summary>The surface window once its handle exists; <see cref="WindowToken.None"/> before and after it.</summary>
    public WindowToken SurfaceWindow => new(Volatile.Read(ref _handle));

    /// <summary>The registry this surface belongs to (owner anchor and activation guard included).</summary>
    protected SurfaceRegistry Registry { get; }

    /// <summary>
    /// The band around the content, in logical units, where the precomputed shadow is drawn (blueprint §8.1). It never
    /// acts as the surface: <c>WM_NCHITTEST</c> answers <c>HTNOWHERE</c> there. Zero by default.
    /// </summary>
    protected internal Thickness ShadowMargin { get; set; }

    /// <summary>The bounds <see cref="MovePassive"/> is applying, which win over a <c>WM_DPICHANGED</c> suggestion.</summary>
    internal PhysicalRect? PendingMove { get; private set; }

    /// <summary>
    /// Shows the surface without activating it: creates the handle if needed (without showing it), lets WPF show it
    /// with <c>ShowActivated = false</c> (<c>SW_SHOWNA</c>) inside an activation veto, then
    /// <c>SetWindowPos(HWND_TOPMOST, SWP_SHOWWINDOW | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE)</c>. Never changes
    /// <c>GetForegroundWindow</c> or the keyboard focus.
    /// </summary>
    /// <exception cref="InvalidOperationException">The surface is maximized (WPF would show it activated).</exception>
    public void ShowPassive()
    {
        VerifyAccess();
        if (WindowState == WindowState.Maximized)
        {
            throw new InvalidOperationException(
                "A surface is never maximized: WPF shows a maximized window activated."
            );
        }

        var window = EnsureSurfaceHandle();
        var previous = Registry.Hints.Enter(ActivationCause.Show);
        try
        {
            if (!IsVisible)
            {
                // WPF's show path is long (layout, first render, topmost): whatever it calls, it cannot activate.
                var vetoed = !Registry.IsActivationAllowed(Id) && ActivationVeto.Begin(window);
                try
                {
                    ShowWithoutActivating();
                }
                finally
                {
                    if (vetoed)
                    {
                        ActivationVeto.End(window);
                    }
                }
            }

            _ = SurfaceStyles.PlaceOnTop(window, show: true);
        }
        finally
        {
            Registry.Hints.Exit(previous);
        }
    }

    /// <summary>
    /// Hides the surface without activating anything: WPF's hide (<c>ShowWindow(SW_HIDE)</c>), whose
    /// <c>SWP_HIDEWINDOW</c> gets <c>SWP_NOACTIVATE</c> from the common hook. A surface that never had a handle stays
    /// without one.
    /// </summary>
    public void HidePassive()
    {
        VerifyAccess();
        if (Volatile.Read(ref _handle) != 0)
        {
            Hide();
        }
    }

    /// <summary>
    /// Moves and resizes the surface to <paramref name="bounds"/> in physical screen pixels
    /// (<c>SWP_NOACTIVATE | SWP_NOZORDER</c>), creating the handle if needed without showing it. When the move
    /// crosses to a monitor with another DPI, <paramref name="bounds"/> replaces the rectangle Windows suggests and
    /// WPF rescales the content (ACC-008).
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="bounds"/> is empty.</exception>
    public void MovePassive(PhysicalRect bounds)
    {
        VerifyAccess();
        if (bounds.IsEmpty)
        {
            throw new ArgumentException(
                "A surface cannot be moved to an empty rectangle.",
                nameof(bounds)
            );
        }

        var window = EnsureSurfaceHandle();
        PendingMove = bounds;
        try
        {
            _ = PInvoke.SetWindowPos(
                window,
                HWND.Null,
                bounds.Left,
                bounds.Top,
                bounds.Width,
                bounds.Height,
                SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE
                    | SET_WINDOW_POS_FLAGS.SWP_NOZORDER
                    | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER
            );
        }
        finally
        {
            PendingMove = null;
        }
    }

    /// <summary>
    /// The logical size WPF lays the surface out at (its actual size, or <c>Width</c> and <c>Height</c> before the
    /// first layout); false when neither is known.
    /// </summary>
    internal bool TryGetLogicalSize(out double width, out double height)
    {
        width = ActualWidth > 0 ? ActualWidth : Width;
        height = ActualHeight > 0 ? ActualHeight : Height;
        return width > 0 && height > 0 && double.IsFinite(width) && double.IsFinite(height);
    }

    /// <summary>Puts <c>WS_EX_NOACTIVATE</c> back after a violation (<see cref="ActivationGuard"/>).</summary>
    internal void ReapplyNonActivation()
    {
        var handle = Volatile.Read(ref _handle);
        if (handle != 0)
        {
            _ = SurfaceStyles.SetNoActivate((HWND)handle, noActivate: true);
        }
    }

    /// <summary>The handle is gone (<c>WM_NCDESTROY</c>): the surface leaves the registry.</summary>
    internal void OnHandleDestroyed()
    {
        Registry.Unregister(this);
        Volatile.Write(ref _handle, 0);
    }

    /// <summary>Applies the non-activation contract before the first show; sealed so no surface can skip it.</summary>
    protected sealed override void OnSourceInitialized(EventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        var window = (HWND)helper.Handle;
        var anchor = (HWND)Registry.Anchor.EnsureCreated().Handle;
        if (PInvoke.GetWindow(window, GET_WINDOW_CMD.GW_OWNER) != anchor)
        {
            helper.Owner = anchor;
        }

        SurfaceStyles.ApplyNonActivation(window);
        _ = SurfaceStyles.PlaceOnTop(window, show: false);
        _ = PointerSetup.DisableTouchFeedback(new WindowToken(window));
        HwndSource.FromHwnd(window).AddHook(new SurfaceHook(this, Registry).WndProc);
        Volatile.Write(ref _handle, window);
        try
        {
            Registry.Register(this);
        }
        catch
        {
            // Not a live surface: another one has its id (a defect of the caller).
            Volatile.Write(ref _handle, 0);
            throw;
        }

        OnSurfaceInitialized();
        base.OnSourceInitialized(e);
    }

    /// <summary>
    /// Called at the end of <see cref="OnSourceInitialized"/>, once the handle has every non-activation setting,
    /// for surface-specific setup such as attaching <c>Pointer.PointerInputSource</c>.
    /// </summary>
    protected virtual void OnSurfaceInitialized() { }

    private HWND EnsureSurfaceHandle() => (HWND)new WindowInteropHelper(this).EnsureHandle();

    [SuppressMessage(
        "Clicalo.Windowing",
        "CLC0001",
        Justification = "The single show of a surface, called only from ShowPassive: with ShowActivated false WPF shows it with SW_SHOWNA, inside an ActivationVeto that refuses any activation of the surface for the length of the call."
    )]
    private void ShowWithoutActivating()
    {
        ShowActivated = false;
        Show();
    }
}
