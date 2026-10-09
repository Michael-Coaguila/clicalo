using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls.Internal;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A slider with discrete − and + buttons (ACC-005: «Todos tienen − / + discretos, teclado y RangeValue»; prototype
/// quick settings, Opacidad): a <c>cardHi</c> − button of 40 × 40, the track (filled part in <c>accent</c>, the rest
/// in <c>line</c>, a 20 px <c>accent</c> knob) and a + button. − and + move by <see cref="RangeBase.SmallChange"/>;
/// touching the track moves to that point. Every part responds on at least 44 high (REG-02).
/// </summary>
/// <remarks>
/// UI Automation sees a Slider with the RangeValue pattern (named with <c>AutomationProperties.Name</c>) whose only
/// children are the − and + buttons, named with <see cref="DecreaseName"/> and <see cref="IncreaseName"/> (localized
/// by the view: a glyph is not a name, UIA008). See <see cref="StepSliderAutomationPeer"/>.
/// </remarks>
public sealed class StepSlider : Slider
{
    /// <summary>Identifies <see cref="DecreaseName"/>.</summary>
    public static readonly DependencyProperty DecreaseNameProperty = DependencyProperty.Register(
        nameof(DecreaseName),
        typeof(string),
        typeof(StepSlider),
        new FrameworkPropertyMetadata(string.Empty)
    );

    /// <summary>Identifies <see cref="IncreaseName"/>.</summary>
    public static readonly DependencyProperty IncreaseNameProperty = DependencyProperty.Register(
        nameof(IncreaseName),
        typeof(string),
        typeof(StepSlider),
        new FrameworkPropertyMetadata(string.Empty)
    );

    /// <summary>Name of the − button.</summary>
    public const string DecreasePart = "PART_Decrease";

    /// <summary>Name of the + button.</summary>
    public const string IncreasePart = "PART_Increase";

    /// <summary>Visual side of the − and + buttons (prototype: 40).</summary>
    public const double StepButtonSize = 40;

    /// <summary>Thickness of the track.</summary>
    public const double TrackThickness = 6;

    /// <summary>Side of the knob.</summary>
    public const double KnobSize = 20;

    private const string TrackPart = "PART_Track";

    /// <summary>
    /// How far each bar runs under the knob's touch box, to the knob's center: the track reads as one continuous line,
    /// as the prototype's range input, instead of stopping at the 44 px hit box.
    /// </summary>
    private static readonly double KnobOverlap = TouchTarget.MinimumSize / 2;
    private static readonly ControlTemplate DefaultTemplate = CreateTemplate();
    private static readonly ControlTemplate FilledBar = Bar(
        ColorToken.Accent,
        new Thickness(0, 0, -KnobOverlap, 0)
    );
    private static readonly ControlTemplate EmptyBar = Bar(
        ColorToken.Line,
        new Thickness(-KnobOverlap, 0, 0, 0)
    );
    private static readonly ControlTemplate KnobTemplate = Knob();

    static StepSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(StepSlider),
            new FrameworkPropertyMetadata(typeof(StepSlider))
        );
        TemplateProperty.OverrideMetadata(
            typeof(StepSlider),
            new FrameworkPropertyMetadata(DefaultTemplate)
        );
        FocusVisualStyleProperty.OverrideMetadata(
            typeof(StepSlider),
            new FrameworkPropertyMetadata(FocusRingStyle.Create(Radii.Control))
        );
        TouchTarget.Enforce(typeof(StepSlider));
    }

    /// <summary>Creates a slider from 0 to 10 that moves to the touched point.</summary>
    public StepSlider()
    {
        Templates.UseTheme(this, ColorToken.Card, ColorToken.Text, ColorToken.Line);
        Background = Brushes.Transparent;
        IsMoveToPointEnabled = true;
        SmallChange = 1;
        LargeChange = 1;
    }

    /// <summary>Localized UI Automation name of the − button.</summary>
    public string DecreaseName
    {
        get => (string)GetValue(DecreaseNameProperty);
        set => SetValue(DecreaseNameProperty, value);
    }

    /// <summary>Localized UI Automation name of the + button.</summary>
    public string IncreaseName
    {
        get => (string)GetValue(IncreaseNameProperty);
        set => SetValue(IncreaseNameProperty, value);
    }

    /// <summary>The − button of the applied template.</summary>
    public IconButton? DecreaseButton { get; private set; }

    /// <summary>The + button of the applied template.</summary>
    public IconButton? IncreaseButton { get; private set; }

    /// <summary>The track between − and +, where a finger slides the knob (AJR-002).</summary>
    public FrameworkElement? TrackElement { get; private set; }

    /// <summary>
    /// The value under <paramref name="screen"/> (physical screen pixels) along the track, snapped to the ticks and kept
    /// between <see cref="RangeBase.Minimum"/> and <see cref="RangeBase.Maximum"/>; null before the template applies.
    /// The panel's pointer layer consumes the finger, so the surface asks this instead of letting WPF drag the thumb.
    /// </summary>
    /// <param name="screen">The contact, in physical screen pixels.</param>
    public double? ValueAt(Point screen)
    {
        if (TrackElement is not Track track || PresentationSource.FromVisual(track) is null)
        {
            return null;
        }

        var value = track.ValueFromPoint(track.PointFromScreen(screen));
        if (TickFrequency > 0)
        {
            value = Minimum + (Math.Round((value - Minimum) / TickFrequency) * TickFrequency);
        }

        return Math.Clamp(value, Minimum, Maximum);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DecreaseButton = GetTemplateChild(DecreasePart) as IconButton;
        IncreaseButton = GetTemplateChild(IncreasePart) as IconButton;

        // The repeat buttons and the thumb of a Track are plain properties, which a template built in code cannot
        // set: each instance gets its own.
        TrackElement = GetTemplateChild(TrackPart) as Track;
        if (TrackElement is Track track)
        {
            track.DecreaseRepeatButton = new RepeatButton
            {
                Template = FilledBar,
                Command = DecreaseLarge,
                Focusable = false,
                IsTabStop = false,
            };
            track.IncreaseRepeatButton = new RepeatButton
            {
                Template = EmptyBar,
                Command = IncreaseLarge,
                Focusable = false,
                IsTabStop = false,
            };
            track.Thumb = new Thumb
            {
                Template = KnobTemplate,
                Focusable = false,
                IsTabStop = false,
            };
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new StepSliderAutomationPeer(this);

    private static ControlTemplate CreateTemplate()
    {
        var decrease = StepButton(DecreasePart, "remove", DecreaseNameProperty, DecreaseSmall)
            .With(DockPanel.DockProperty, Dock.Left);
        var increase = StepButton(IncreasePart, "add", IncreaseNameProperty, IncreaseSmall)
            .With(DockPanel.DockProperty, Dock.Right);
        var track = Templates
            .Element<Track>(TrackPart)
            .With(Track.MinimumProperty, Templates.Bind(MinimumProperty))
            .With(Track.MaximumProperty, Templates.Bind(MaximumProperty))
            .With(Track.ValueProperty, Templates.Bind(ValueProperty))
            .With(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));
        var row = Templates
            .Element<DockPanel>()
            .With(Panel.BackgroundProperty, Templates.Bind(Control.BackgroundProperty))
            .With(DockPanel.LastChildFillProperty, true)
            .Add(decrease, increase, track);
        return Templates.Seal(
            typeof(StepSlider),
            row,
            Templates.When(
                IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, ButtonChrome.DisabledOpacity)
            )
        );
    }

    private static FrameworkElementFactory StepButton(
        string name,
        string symbol,
        DependencyProperty nameProperty,
        System.Windows.Input.RoutedCommand command
    ) =>
        Templates
            .Element<IconButton>(name)
            .With(TouchButton.SymbolProperty, symbol)
            .With(TouchButton.IconSizeProperty, 20d)
            .With(TouchButton.AppearanceProperty, ButtonAppearance.Neutral)
            .With(FrameworkElement.WidthProperty, StepButtonSize)
            .With(FrameworkElement.HeightProperty, StepButtonSize)
            .With(ButtonBase.CommandProperty, command)
            .With(UIElement.FocusableProperty, false)
            .With(Control.IsTabStopProperty, false)
            .With(AutomationProperties.NameProperty, Templates.Bind(nameProperty));

    private static ControlTemplate Bar(ColorToken token, Thickness overlap)
    {
        var bar = Templates
            .Element<Border>()
            .With(FrameworkElement.HeightProperty, TrackThickness)
            .With(FrameworkElement.MarginProperty, overlap)
            .With(Border.CornerRadiusProperty, new CornerRadius(TrackThickness / 2))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Paint(Border.BackgroundProperty, token);
        var hit = Templates
            .Element<Border>()
            .With(Border.BackgroundProperty, Brushes.Transparent)
            .Add(bar);
        return Templates.Seal(typeof(RepeatButton), hit);
    }

    private static ControlTemplate Knob()
    {
        var knob = Templates
            .Element<Border>()
            .With(FrameworkElement.WidthProperty, KnobSize)
            .With(FrameworkElement.HeightProperty, KnobSize)
            .With(Border.CornerRadiusProperty, new CornerRadius(KnobSize / 2))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .Paint(Border.BackgroundProperty, ColorToken.Accent);
        var hit = Templates
            .Element<Border>()
            .With(Border.BackgroundProperty, Brushes.Transparent)
            .With(FrameworkElement.WidthProperty, TouchTarget.MinimumSize)
            .Add(knob);
        return Templates.Seal(typeof(Thumb), hit);
    }
}
