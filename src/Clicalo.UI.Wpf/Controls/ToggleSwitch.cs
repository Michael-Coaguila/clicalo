using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The switch row of direction C (D12; docs/07: «toda la fila es tocable»): an optional icon in a box of
/// <see cref="IconBox.Medium"/> in <c>muted</c>, the label (<c>body</c>, up to two lines with «…») with an optional
/// <see cref="Description"/> under it, and the 48 × 28 switch at the end. Off, the track is an outline of
/// <see cref="Stroke.Selected"/> in <c>line</c> with a 16 knob in <c>muted</c>; on, the track is <c>accent</c> with a
/// 20 knob in <c>onAccent</c> that carries a ✓, so the state is told by shape and mark, not only by color (ACC-003).
/// The knob slides in 150 ms (0 with reduce motion, TEM-006). The row is at least <see cref="ControlHeight.Medium"/>
/// high and is the touch target (REG-02). UI Automation sees a Button with the Toggle pattern whose name is the label
/// (<see cref="TouchToggleAutomationPeer"/>, ACC-001).
/// </summary>
public sealed class ToggleSwitch : ToggleButton
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(ToggleSwitch)
    );

    /// <summary>Identifies <see cref="Description"/>.</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(ToggleSwitch),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Width of the track.</summary>
    public const double TrackWidth = ComponentSize.SwitchWidth;

    /// <summary>Height of the track.</summary>
    public const double TrackHeight = ComponentSize.SwitchHeight;

    /// <summary>Side of the knob while the switch is on.</summary>
    public const double KnobSize = ComponentSize.SwitchKnobOn;

    /// <summary>How far the knob slides between off and on.</summary>
    public const double KnobTravel = TrackWidth - KnobSize - (2 * Space.S1);

    /// <summary>Name of the track.</summary>
    public const string TrackPart = "PART_Track";

    /// <summary>Name of the knob.</summary>
    public const string KnobPart = "PART_Knob";

    /// <summary>Name of the fill of the track (accent while on).</summary>
    public const string TrackFillPart = "PART_TrackFill";

    private const string OutlinePart = "PART_Outline";
    private const string DotPart = "PART_Dot";
    private const string MarkPart = "PART_Mark";
    private const string IconPart = "PART_Icon";
    private const string ContentPart = "PART_Content";
    private const string DescriptionPart = "PART_Description";

    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();
    private TranslateTransform? _knobShift;

    static ToggleSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(typeof(ToggleSwitch))
        );
        TemplateProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(ToggleSwitch),
            new FrameworkPropertyMetadata(FocusRingStyle.Create(Radii.Medium))
        );
        TouchTarget.Enforce(typeof(ToggleSwitch));
    }

    /// <summary>Creates a switch that is off.</summary>
    public ToggleSwitch()
    {
        Background = Brushes.Transparent;
        SetResourceReference(ForegroundProperty, ThemeBrushKey.For(ColorToken.Text));
        SetResourceReference(FontFamilyProperty, ThemeKeys.UiFont);
        SetResourceReference(FontSizeProperty, ThemeKeys.FontSize(TextStyleToken.Body));
        MinHeight = ControlHeight.Medium;
        Padding = new Thickness(0, Space.S2, 0, Space.S2);
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        IsChecked = false;
    }

    /// <summary>Material Symbols name of the icon before the label; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>A line that explains the setting, under the label (<c>caption</c>, <c>muted</c>); null for none.</summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Where the knob is now, from 0 (off) to <see cref="KnobTravel"/> (on); for tests and diagnostics.</summary>
    public double KnobOffset => _knobShift?.X ?? 0;

    /// <summary>
    /// The switch is only the drawing of the state of a row that is the switch itself: UI Automation then sees the
    /// row and not a second, nameless switch inside it. Set it before the switch is shown.
    /// </summary>
    public bool IsDrawingOnly { get; init; }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _knobShift = new TranslateTransform(Target(), 0);
        if (GetTemplateChild(KnobPart) is FrameworkElement knob)
        {
            knob.RenderTransform = _knobShift;
        }

        if (GetTemplateChild(ContentPart) is ContentPresenter presenter)
        {
            presenter.Resources[typeof(TextBlock)] = OptionChrome.TextStyleFor(
                2,
                centered: false,
                TextStyleToken.Body
            );
        }
    }

    /// <summary>
    /// The state is the view model's (a row of settings): a tap, or Toggle from UI Automation, only raises
    /// <c>Click</c> and the view sets <see cref="ToggleButton.IsChecked"/>. Set it before the switch is shown.
    /// </summary>
    public bool FollowsViewModel { get; init; }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (FollowsViewModel)
        {
            RaiseEvent(new RoutedEventArgs(ClickEvent, this));
            return;
        }

        base.OnClick();
    }

    /// <inheritdoc />
    protected override void OnToggle()
    {
        if (FollowsViewModel)
        {
            OnClick();
            return;
        }

        base.OnToggle();
    }

    /// <inheritdoc />
    protected override AutomationPeer? OnCreateAutomationPeer() =>
        IsDrawingOnly ? null : new TouchToggleAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        Slide();
    }

    /// <inheritdoc />
    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        Slide();
    }

    private static ControlTemplate CreateTemplate()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(SymbolProperty))
            .With(SymbolIcon.SizeProperty, IconBox.Medium)
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, Space.S3, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(DockPanel.DockProperty, Dock.Left)
            .Paint(SymbolIcon.ForegroundProperty, ColorToken.Muted);
        var outline = Templates
            .Element<Rectangle>(OutlinePart)
            .With(Shape.StrokeThicknessProperty, Stroke.Selected)
            .With(Rectangle.RadiusXProperty, TrackHeight / 2)
            .With(Rectangle.RadiusYProperty, TrackHeight / 2)
            .Paint(Shape.StrokeProperty, ColorToken.Line);
        var dot = Templates
            .Element<Ellipse>(DotPart)
            .With(FrameworkElement.WidthProperty, ComponentSize.SwitchKnobOff)
            .With(FrameworkElement.HeightProperty, ComponentSize.SwitchKnobOff)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Paint(Shape.FillProperty, ColorToken.Muted);
        var mark = Templates
            .Element<SymbolIcon>(MarkPart)
            .With(SymbolIcon.SymbolProperty, OptionChrome.CheckSymbol)
            .With(SymbolIcon.SizeProperty, IconBox.Badge)
            .With(UIElement.VisibilityProperty, Visibility.Collapsed)
            .Paint(SymbolIcon.ForegroundProperty, ColorToken.Accent);
        var knob = Templates
            .Element<Grid>(KnobPart)
            .With(FrameworkElement.WidthProperty, KnobSize)
            .With(FrameworkElement.HeightProperty, KnobSize)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(FrameworkElement.MarginProperty, new Thickness(Space.S1, 0, 0, 0))
            .Add(dot, mark);
        var track = Templates
            .Element<Grid>(TrackPart)
            .With(FrameworkElement.WidthProperty, TrackWidth)
            .With(FrameworkElement.HeightProperty, TrackHeight)
            .With(FrameworkElement.MarginProperty, new Thickness(Space.S3, 0, 0, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(DockPanel.DockProperty, Dock.Right)
            .Add(
                Templates
                    .Element<Border>(TrackFillPart)
                    .With(Border.CornerRadiusProperty, new CornerRadius(TrackHeight / 2))
                    .With(Border.BackgroundProperty, Brushes.Transparent),
                outline,
                knob
            );
        var content = Templates
            .Element<ContentPresenter>(ContentPart)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var description = Templates
            .Element<StyledText>(DescriptionPart)
            .With(TextBlock.TextProperty, Templates.Bind(DescriptionProperty))
            .With(StyledText.TextStyleProperty, TextStyleToken.Caption)
            .With(StyledText.InkProperty, ColorToken.Muted)
            .With(StyledText.MaxLinesProperty, 2);
        var text = Templates
            .Element<StackPanel>()
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Add(content, description);
        var row = Templates
            .Element<DockPanel>()
            .With(Panel.BackgroundProperty, Templates.Bind(Control.BackgroundProperty))
            .With(DockPanel.LastChildFillProperty, true)
            .Add(
                icon,
                track,
                text.With(FrameworkElement.MarginProperty, Templates.Bind(Control.PaddingProperty))
            );
        return Templates.Seal(
            typeof(ToggleSwitch),
            row,
            Templates.When(
                SymbolProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart)
            ),
            Templates.When(
                DescriptionProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, DescriptionPart)
            ),
            Templates.When(
                IsCheckedProperty,
                true,
                Templates.Brush(Border.BackgroundProperty, ColorToken.Accent, TrackFillPart),
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, OutlinePart),
                new Setter(FrameworkElement.WidthProperty, KnobSize, DotPart),
                new Setter(FrameworkElement.HeightProperty, KnobSize, DotPart),
                Templates.Brush(Shape.FillProperty, ColorToken.OnAccent, DotPart),
                new Setter(UIElement.VisibilityProperty, Visibility.Visible, MarkPart)
            ),
            Templates.When(
                IsEnabledProperty,
                false,
                Templates.Brush(Control.ForegroundProperty, ColorToken.Muted)
            )
        );
    }

    private double Target() => IsChecked == true ? KnobTravel : 0;

    private void Slide()
    {
        if (_knobShift is null)
        {
            return;
        }

        var duration = Motion.Get(
            MotionToken.SwitchKnob,
            reduceMotion: TryFindResource(ThemeKeys.ReduceMotion) is true
        );
        if (duration <= TimeSpan.Zero || !IsLoaded)
        {
            _knobShift.BeginAnimation(TranslateTransform.XProperty, null);
            _knobShift.X = Target();
            return;
        }

        _knobShift.BeginAnimation(
            TranslateTransform.XProperty,
            new DoubleAnimation(Target(), new Duration(duration))
        );
    }
}
