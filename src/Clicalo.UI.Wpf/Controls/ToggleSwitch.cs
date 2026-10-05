using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The switch row of the prototype (docs/07 «Formas»: «Interruptores de 48×28 (knob de 20). Toda la fila es
/// tocable»): an optional icon in <c>muted</c>, the label and a 48 × 28 track whose 20 px knob slides 20 px in
/// 150 ms (0 with reduce motion, TEM-006). The whole row is the touch target, at least 44 high (REG-02). On, the track
/// is <c>accent</c> and the knob <c>onAccent</c>; off, <c>cardHi</c> and <c>text</c>: the knob's side also tells the
/// state, not only the color (ACC-003). UI Automation sees a Button with the Toggle pattern whose name is the label
/// (<see cref="TouchToggleAutomationPeer"/>, ACC-001).
/// </summary>
public sealed class ToggleSwitch : ToggleButton
{
    /// <summary>Identifies <see cref="Symbol"/>.</summary>
    public static readonly DependencyProperty SymbolProperty = TouchButton.SymbolProperty.AddOwner(
        typeof(ToggleSwitch)
    );

    /// <summary>Width of the track.</summary>
    public const double TrackWidth = 48;

    /// <summary>Height of the track.</summary>
    public const double TrackHeight = 28;

    /// <summary>Side of the knob.</summary>
    public const double KnobSize = 20;

    /// <summary>Distance from the knob to the inner edge of the track.</summary>
    public const double KnobInset = 3;

    /// <summary>How far the knob slides between off and on (prototype: 3 → 23 px).</summary>
    public const double KnobTravel = 20;

    /// <summary>Name of the track.</summary>
    public const string TrackPart = "PART_Track";

    /// <summary>Name of the knob.</summary>
    public const string KnobPart = "PART_Knob";

    private const string IconPart = "PART_Icon";
    private const string ContentPart = "PART_Content";
    private const double Gap = 10;

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
            new FrameworkPropertyMetadata(FocusRingStyle.Create(Radii.Control))
        );
        TouchTarget.Enforce(typeof(ToggleSwitch));
    }

    /// <summary>Creates a switch that is off.</summary>
    public ToggleSwitch()
    {
        Templates.UseTheme(this, ColorToken.Card, ColorToken.Text, ColorToken.Border);
        Background = Brushes.Transparent;
        FontSize = 14;
        Padding = new Thickness(2, 0, 2, 0);
        IsChecked = false;
    }

    /// <summary>Material Symbols name of the icon before the label; null for none.</summary>
    public string? Symbol
    {
        get => (string?)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    /// <summary>Where the knob is now, from 0 (off) to <see cref="KnobTravel"/> (on); for tests and diagnostics.</summary>
    public double KnobOffset => _knobShift?.X ?? 0;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _knobShift = new TranslateTransform(Target(), 0);
        if (GetTemplateChild(KnobPart) is FrameworkElement knob)
        {
            knob.RenderTransform = _knobShift;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new TouchToggleAutomationPeer(this);

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
            .With(SymbolIcon.SizeProperty, 20d)
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, Gap, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(DockPanel.DockProperty, Dock.Left)
            .Paint(SymbolIcon.ForegroundProperty, ColorToken.Muted);
        var knob = Templates
            .Element<Border>(KnobPart)
            .With(FrameworkElement.WidthProperty, KnobSize)
            .With(FrameworkElement.HeightProperty, KnobSize)
            .With(Border.CornerRadiusProperty, new CornerRadius(KnobSize / 2))
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(FrameworkElement.MarginProperty, new Thickness(KnobInset, 0, 0, 0))
            .Paint(Border.BackgroundProperty, ColorToken.Text);
        var track = Templates
            .Element<Border>(TrackPart)
            .With(FrameworkElement.WidthProperty, TrackWidth)
            .With(FrameworkElement.HeightProperty, TrackHeight)
            .With(Border.CornerRadiusProperty, new CornerRadius(TrackHeight / 2))
            .With(Border.BorderBrushProperty, Templates.Bind(Control.BorderBrushProperty))
            .With(Border.BorderThicknessProperty, Templates.Bind(Control.BorderThicknessProperty))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(DockPanel.DockProperty, Dock.Right)
            .With(UIElement.SnapsToDevicePixelsProperty, true)
            .Paint(Border.BackgroundProperty, ColorToken.CardHi)
            .Add(knob);
        var content = Templates
            .Element<ContentPresenter>(ContentPart)
            .With(FrameworkElement.MarginProperty, new Thickness(0, 0, Gap, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var row = Templates
            .Element<DockPanel>()
            .With(Panel.BackgroundProperty, Templates.Bind(Control.BackgroundProperty))
            .With(FrameworkElement.MarginProperty, Templates.Bind(Control.PaddingProperty))
            .With(DockPanel.LastChildFillProperty, true)
            .Add(icon, track, content);
        return Templates.Seal(
            typeof(ToggleSwitch),
            row,
            Templates.When(
                SymbolProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart)
            ),
            Templates.When(
                IsCheckedProperty,
                true,
                Templates.Brush(Border.BackgroundProperty, ColorToken.Accent, TrackPart),
                Templates.Brush(Border.BackgroundProperty, ColorToken.OnAccent, KnobPart)
            ),
            Templates.When(
                IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, ButtonChrome.DisabledOpacity)
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
