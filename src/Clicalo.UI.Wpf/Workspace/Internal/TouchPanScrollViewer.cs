using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// A vertical scroll area of the Control Center that the finger can drag (blueprint §3.6: with WPF's touch stack off,
/// touch arrives as promoted mouse, which a <see cref="ScrollViewer"/> does not pan). A drag that goes further than
/// «cancelar si deslizas» scrolls and activates nothing (TAC-004): the area takes the capture, so the button under the
/// finger is released without a click. The wheel scrolls as usual, and UI Automation's Scroll pattern (Voice access:
/// «desplazar hacia abajo») too; the bar is hidden, as in the prototype, and it never scrolls sideways (CCM-005).
/// </summary>
internal sealed class TouchPanScrollViewer : ScrollViewer
{
    private Point _start;
    private double _startOffset;
    private bool _pressed;
    private bool _panning;

    /// <summary>Creates a vertical scroll area.</summary>
    public TouchPanScrollViewer()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = false;
        PanningMode = PanningMode.None;
    }

    /// <summary>
    /// How far, in device-independent pixels, the finger moves before the area scrolls: «cancelar si deslizas» of the
    /// touch settings (TAC-004); set by the composition root.
    /// </summary>
    public static double PanThreshold { get; set; } = 10;

    /// <summary>A drag of a tile took the gesture (ATJ-009): the area does not scroll with it.</summary>
    public void CancelPan()
    {
        _pressed = false;
        if (_panning)
        {
            _panning = false;
            ReleaseMouseCapture();
        }
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _pressed = !IsInsideInnerArea(e.OriginalSource as DependencyObject) && ScrollableHeight > 0;
        _panning = false;
        _start = e.GetPosition(this);
        _startOffset = VerticalOffset;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (!_pressed || e.LeftButton != MouseButtonState.Pressed)
        {
            _pressed = false;
            return;
        }

        var dy = e.GetPosition(this).Y - _start.Y;
        if (!_panning)
        {
            if (Math.Abs(dy) < PanThreshold)
            {
                return;
            }

            _panning = CaptureMouse();
            _start = e.GetPosition(this);
            _startOffset = VerticalOffset;
            dy = 0;
        }

        ScrollToVerticalOffset(_startOffset - dy);
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_panning)
        {
            e.Handled = true;
            ReleaseMouseCapture();
        }

        _pressed = false;
        _panning = false;
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _panning = false;
        _pressed = false;
    }

    private bool IsInsideInnerArea(DependencyObject? source)
    {
        for (
            var node = source;
            node is not null && !ReferenceEquals(node, this);
            node = ParentOf(node)
        )
        {
            if (node is TouchPanScrollViewer)
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? ParentOf(DependencyObject node) =>
        node is Visual or System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(node)
            : LogicalTreeHelper.GetParent(node);
}
