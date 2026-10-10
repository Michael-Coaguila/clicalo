using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace Clicalo.UI.Wpf.Windowing.Internal;

/// <summary>
/// The shadow of a surface with a <see cref="SurfaceLook"/> (spike S6): a window of its own, right below the surface in
/// the z-order, that draws the <see cref="ShadowRaster"/> bitmap and lets every click and touch through. With an
/// inset it surrounds what the surface draws inside its window instead of the whole window (the handle of the Tab view,
/// whose window is deeper than its drawing to take the touch on 44 px: PES-001, REG-02).
/// </summary>
/// <remarks>
/// <para>
/// Why a window of its own: a per-pixel transparent window takes the input on every pixel whose alpha is not zero, so a
/// shadow drawn inside the surface would swallow the touches around it, and <c>HTTRANSPARENT</c> only passes them to
/// windows of the same thread. <c>WS_EX_LAYERED | WS_EX_TRANSPARENT</c> makes the whole shadow window transparent to
/// hit testing, across processes (measured in S6).
/// </para>
/// <para>
/// A bare <c>HwndSource</c> (<c>WS_POPUP</c>, owned by the owner anchor, <c>WS_EX_NOACTIVATE | WS_EX_TOPMOST |
/// WS_EX_TOOLWINDOW</c>, per-pixel opacity): it is not a surface and is not registered. It follows the surface on every
/// <c>WM_WINDOWPOSCHANGED</c> (<see cref="Follow"/>), with <c>SWP_NOACTIVATE</c>, and its opacity is bound to the
/// surface's, so dimming fades both. Its own <c>WM_DPICHANGED</c> is swallowed: the bitmap is already in physical
/// pixels and WPF's answer to that message activates the window (#7561).
/// </para>
/// </remarks>
internal sealed class SurfaceShadow : IDisposable
{
    private const string WindowName = "Clicalo.SurfaceShadow";

    private readonly NonActivatingWindow _surface;
    private readonly SurfaceLook _look;
    private readonly Thickness _inset;
    private readonly HwndSource _source;
    private readonly ShadowElement _element = new();
    private (int Width, int Height, uint Dpi, Color Color) _rendered;
    private ShadowImage? _image;

    public SurfaceShadow(
        NonActivatingWindow surface,
        SurfaceLook look,
        nint owner,
        Thickness inset = default
    )
    {
        _surface = surface;
        _look = look;
        _inset = inset;
        var parameters = new HwndSourceParameters(WindowName)
        {
            WindowStyle = unchecked((int)WINDOW_STYLE.WS_POPUP),
            ExtendedWindowStyle = (int)(
                WINDOW_EX_STYLE.WS_EX_LAYERED
                | WINDOW_EX_STYLE.WS_EX_TRANSPARENT
                | WINDOW_EX_STYLE.WS_EX_NOACTIVATE
                | WINDOW_EX_STYLE.WS_EX_TOPMOST
                | WINDOW_EX_STYLE.WS_EX_TOOLWINDOW
            ),
            UsesPerPixelOpacity = true,
            ParentWindow = owner,
            Width = 1,
            Height = 1,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        _element.SetBinding(
            UIElement.OpacityProperty,
            new Binding(nameof(Window.Opacity)) { Source = surface, Mode = BindingMode.OneWay }
        );
        _source.RootVisual = _element;
    }

    /// <summary>The shadow window.</summary>
    public nint Handle => _source.Handle;

    /// <summary>The shadow in use; null while the surface has no size or the theme has no shadow.</summary>
    public ShadowImage? Image => _image;

    /// <summary>
    /// Puts the shadow right below the surface, around its bounds, shown or hidden like it. <paramref name="visible"/>
    /// overrides <c>IsWindowVisible</c> while a show or a hide is still under way.
    /// </summary>
    public void Follow(bool? visible = null)
    {
        var surface = (HWND)_surface.SurfaceWindow.Handle;
        if (surface.IsNull || !PInvoke.GetWindowRect(surface, out var bounds))
        {
            return;
        }

        var shown = visible ?? PInvoke.IsWindowVisible(surface);
        var dpi = PInvoke.GetDpiForWindow(surface);
        bounds = Inset(bounds, _inset, dpi / 96.0);
        var width = bounds.right - bounds.left;
        var height = bounds.bottom - bounds.top;
        var color = _surface.ShadowColor;
        if (width < 1 || height < 1 || color.A == 0 || dpi == 0)
        {
            _image = null;
            _rendered = default;
            Place(surface, default, SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW);
            return;
        }

        if (_image is null || _rendered != (width, height, dpi, color))
        {
            _image = ShadowRaster.Render(width, height, dpi / 96.0, _look, color);
            _rendered = (width, height, dpi, color);
            _element.Draw(_image.Bitmap);
        }

        Place(
            surface,
            new RECT
            {
                left = bounds.left - _image.Left,
                top = bounds.top - _image.Top,
                right = bounds.right + _image.Right,
                bottom = bounds.bottom + _image.Bottom,
            },
            shown ? SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW : SET_WINDOW_POS_FLAGS.SWP_HIDEWINDOW
        );
    }

    /// <summary>
    /// <paramref name="bounds"/> less <paramref name="inset"/> (device-independent pixels) at <paramref name="scale"/>:
    /// the rectangle the surface draws inside its window.
    /// </summary>
    internal static RECT Inset(RECT bounds, Thickness inset, double scale) =>
        new()
        {
            left = bounds.left + Pixels(inset.Left, scale),
            top = bounds.top + Pixels(inset.Top, scale),
            right = bounds.right - Pixels(inset.Right, scale),
            bottom = bounds.bottom - Pixels(inset.Bottom, scale),
        };

    /// <summary>Destroys the shadow window.</summary>
    public void Dispose()
    {
        BindingOperations.ClearAllBindings(_element);
        _source.Dispose();
    }

    private static int Pixels(double logical, double scale) =>
        (int)Math.Round(logical * scale, MidpointRounding.AwayFromZero);

    private void Place(HWND surface, RECT bounds, SET_WINDOW_POS_FLAGS show)
    {
        var flags =
            show | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER;
        if (bounds.right <= bounds.left)
        {
            flags |= SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
        }

        // Right below the surface: hWndInsertAfter is the surface, so both stay in the topmost band together.
        _ = PInvoke.SetWindowPos(
            (HWND)_source.Handle,
            surface,
            bounds.left,
            bounds.top,
            bounds.right - bounds.left,
            bounds.bottom - bounds.top,
            flags
        );
    }

    private unsafe nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_MOUSEACTIVATE:
                handled = true;
                return (nint)PInvoke.MA_NOACTIVATE;

            case PInvoke.WM_WINDOWPOSCHANGING:
                ((WINDOWPOS*)lParam)->flags |= SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
                break;

            case PInvoke.WM_DPICHANGED:
                handled = true;
                return 0;
        }

        return 0;
    }

    /// <summary>Draws the bitmap over its whole size; not in the UI Automation tree.</summary>
    private sealed class ShadowElement : FrameworkElement
    {
        private ImageSource? _bitmap;

        public void Draw(ImageSource bitmap)
        {
            _bitmap = bitmap;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            if (_bitmap is not null)
            {
                drawingContext.DrawImage(_bitmap, new Rect(RenderSize));
            }
        }
    }
}
