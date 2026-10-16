using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The designed vertical scroll area of direction C (D12): a thin bar of <see cref="ComponentSize.ScrollBar"/> in the
/// theme's <c>line</c> (no arrows) in a channel reserved at the right edge whether or not there is more to see, so
/// nothing moves when the bar appears; and a fade of <see cref="ComponentSize.ScrollFade"/> at the bottom while there
/// is more below, the sign that it scrolls. It never scrolls sideways.
/// </summary>
/// <remarks>
/// <para>
/// The bar only tells where the content is: UI Automation does not see it as a control (a thin bar and nameless
/// arrows are no touch targets, REG-02, REG-06); it scrolls the area with the Scroll pattern («desplazar hacia
/// abajo»), and the wheel too.
/// </para>
/// <para>
/// With <see cref="PansWithMouse"/> (the Control Center, where touch arrives as promoted mouse, blueprint §3.6) a
/// drag that goes further than «cancelar si deslizas» scrolls and activates nothing (TAC-004): the area takes the
/// capture, so the button under the finger is released without a click. The surfaces of the panel turn it off: their
/// pointer layer scrolls them.
/// </para>
/// </remarks>
public sealed class ThemedScrollViewer : ScrollViewer
{
    private const string PresenterPart = "PART_ScrollContentPresenter";
    private const string BarPart = "PART_VerticalScrollBar";
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();
    private static readonly ControlTemplate BarTemplate = CreateBarTemplate();
    private static readonly ControlTemplate ThumbTemplate = CreateThumbTemplate();
    private static readonly ControlTemplate EmptyTemplate = Templates.Seal(
        typeof(RepeatButton),
        Templates.Element<Border>().With(Border.BackgroundProperty, Brushes.Transparent)
    );

    private ScrollContentPresenter? _presenter;
    private Point _start;
    private double _startOffset;
    private bool _pressed;
    private bool _panning;

    static ThemedScrollViewer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ThemedScrollViewer),
            new FrameworkPropertyMetadata(typeof(ThemedScrollViewer))
        );
        TemplateProperty.OverrideMetadata(
            typeof(ThemedScrollViewer),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
    }

    /// <summary>Creates a vertical scroll area that never takes the focus.</summary>
    public ThemedScrollViewer()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        Focusable = false;
        PanningMode = PanningMode.None;
        ScrollChanged += (_, _) => UpdateFade();
        SizeChanged += (_, _) => UpdateFade();
    }

    /// <summary>
    /// How far, in device-independent pixels, the finger moves before the area scrolls: «cancelar si deslizas» of the
    /// touch settings (TAC-004).
    /// </summary>
    public static double PanThreshold { get; set; } = Space.S2;

    /// <summary>Whether a drag of promoted mouse scrolls the area (the Control Center); true by default.</summary>
    public bool PansWithMouse { get; set; } = true;

    /// <summary>Whether the bottom of the content fades now (there is more below).</summary>
    public bool IsFading { get; private set; }

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
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _presenter = GetTemplateChild(PresenterPart) as ScrollContentPresenter;
        if (GetTemplateChild(BarPart) is ScrollBar bar)
        {
            Theme(bar);
        }

        UpdateFade();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new Peer(this);

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        _pressed =
            PansWithMouse
            && !IsInsideInnerArea(e.OriginalSource as DependencyObject)
            && ScrollableHeight > 0;
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

    /// <summary>
    /// The thin bar: the parts of a <see cref="Track"/> are plain properties, which a template built in code cannot
    /// set, so each bar gets its own thumb and its own invisible page areas.
    /// </summary>
    private static void Theme(ScrollBar bar)
    {
        bar.Template = BarTemplate;
        if (!bar.ApplyTemplate() && bar.Track is null)
        {
            return;
        }

        if (bar.Track is { } track)
        {
            track.Thumb = new Thumb { Template = ThumbTemplate, Focusable = false };
            track.DecreaseRepeatButton = new RepeatButton
            {
                Template = EmptyTemplate,
                Command = ScrollBar.PageUpCommand,
                Focusable = false,
                IsTabStop = false,
            };
            track.IncreaseRepeatButton = new RepeatButton
            {
                Template = EmptyTemplate,
                Command = ScrollBar.PageDownCommand,
                Focusable = false,
                IsTabStop = false,
            };
        }
    }

    private void UpdateFade()
    {
        if (_presenter is not { } presenter)
        {
            return;
        }

        var height = presenter.ActualHeight;
        var fade = ScrollableHeight - VerticalOffset > 0.5 && height > ComponentSize.ScrollFade;
        IsFading = fade;
        if (!fade)
        {
            presenter.OpacityMask = null;
            return;
        }

        // The last stretch of the visible content fades to the surface behind it: there is more below. A mask only
        // reads alpha: any opaque color of the theme serves.
        var opaque = TryFindResource(ThemeBrushKey.For(ColorToken.Text)) is SolidColorBrush ink
            ? ink.Color
            : SystemColors.ControlTextColor;
        opaque.A = byte.MaxValue;
        var clear = opaque;
        clear.A = 0;
        var mask = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, height - ComponentSize.ScrollFade),
            EndPoint = new Point(0, height),
        };
        mask.GradientStops.Add(new GradientStop(opaque, 0));
        mask.GradientStops.Add(new GradientStop(clear, 1));
        mask.Freeze();
        presenter.OpacityMask = mask;
    }

    private bool IsInsideInnerArea(DependencyObject? source)
    {
        for (
            var node = source;
            node is not null && !ReferenceEquals(node, this);
            node = ParentOf(node)
        )
        {
            if (node is ThemedScrollViewer)
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

    private static ControlTemplate CreateTemplate()
    {
        var presenter = Templates
            .Element<ScrollContentPresenter>(PresenterPart)
            .With(
                ScrollContentPresenter.CanContentScrollProperty,
                Templates.Bind(CanContentScrollProperty)
            )
            .With(FrameworkElement.MarginProperty, Templates.Bind(Control.PaddingProperty));
        var bar = Templates
            .Element<ScrollBar>(BarPart)
            .With(ScrollBar.OrientationProperty, Orientation.Vertical)
            .With(RangeBase.MaximumProperty, Templates.Bind(ScrollableHeightProperty))
            .With(ScrollBar.ViewportSizeProperty, Templates.Bind(ViewportHeightProperty))
            .With(RangeBase.ValueProperty, Templates.Bind(VerticalOffsetProperty))
            .With(
                UIElement.VisibilityProperty,
                Templates.Bind(ComputedVerticalScrollBarVisibilityProperty)
            )
            .With(UIElement.FocusableProperty, false);
        // The channel of the bar is always there: nothing moves when the bar appears.
        var channel = Templates
            .Element<Border>()
            .With(FrameworkElement.WidthProperty, ComponentSize.ScrollBar)
            .With(DockPanel.DockProperty, Dock.Right)
            .Add(bar);
        var root = Templates
            .Element<DockPanel>()
            .With(DockPanel.LastChildFillProperty, true)
            .With(Panel.BackgroundProperty, Templates.Bind(Control.BackgroundProperty))
            .Add(channel, presenter);
        return Templates.Seal(typeof(ThemedScrollViewer), root);
    }

    private static ControlTemplate CreateBarTemplate()
    {
        var track = Templates
            .Element<Track>("PART_Track")
            .With(Track.IsDirectionReversedProperty, true)
            .With(Track.OrientationProperty, Orientation.Vertical)
            .With(Track.MinimumProperty, Templates.Bind(RangeBase.MinimumProperty))
            .With(Track.MaximumProperty, Templates.Bind(RangeBase.MaximumProperty))
            .With(Track.ValueProperty, Templates.Bind(RangeBase.ValueProperty))
            .With(Track.ViewportSizeProperty, Templates.Bind(ScrollBar.ViewportSizeProperty));
        return Templates.Seal(
            typeof(ScrollBar),
            Templates.Element<Grid>().With(Panel.BackgroundProperty, Brushes.Transparent).Add(track)
        );
    }

    private static ControlTemplate CreateThumbTemplate() =>
        Templates.Seal(
            typeof(Thumb),
            Templates
                .Element<Border>()
                .With(Border.CornerRadiusProperty, new CornerRadius(ComponentSize.ScrollBar / 2))
                .Paint(Border.BackgroundProperty, ColorToken.Line)
        );

    /// <summary>The area's peer, without the bar: it scrolls through the Scroll pattern.</summary>
    private sealed class Peer(ScrollViewer owner) : ScrollViewerAutomationPeer(owner)
    {
        protected override List<AutomationPeer>? GetChildrenCore() =>
            base.GetChildrenCore()?.FindAll(static child => child is not ScrollBarAutomationPeer);
    }
}
