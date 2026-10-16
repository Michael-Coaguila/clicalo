using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// The templates of an option that can be chosen (<see cref="OptionButton"/> and <see cref="SegmentedItem"/>), one
/// per <see cref="OptionKind"/>, with the single look of «elegido» of direction C (D12): wash, the ring of
/// <see cref="Stroke.Selected"/> inside, the icon filled in <c>accentInk</c>, and a ✓ when the option has no icon.
/// The option keeps its size in every state. Templates are sealed and shared by every UI thread.
/// </summary>
internal static class OptionChrome
{
    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Name of the ✓ shown by a chosen option without icon.</summary>
    public const string CheckPart = "PART_Check";

    /// <summary>Name of the text presenter.</summary>
    public const string TextPart = "PART_Text";

    /// <summary>Name of the caption under the title of a list row.</summary>
    public const string CaptionPart = "PART_Caption";

    /// <summary>Name of the counter beside the icon of a rail item.</summary>
    public const string CountPart = "PART_Count";

    /// <summary>
    /// Identifies the attached flag that says the option's content is a plain text: only then does a chosen option
    /// without icon show ✓ (a view's own content carries its own marks).
    /// </summary>
    public static readonly DependencyProperty IsTextContentProperty =
        DependencyProperty.RegisterAttached(
            "IsTextContent",
            typeof(bool),
            typeof(OptionChrome),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>The icon of a chosen option without its own icon.</summary>
    public const string CheckSymbol = "check";

    private static readonly ConcurrentDictionary<(Type, OptionKind), ControlTemplate> Cache = new();
    private static readonly ConcurrentDictionary<int, Style> TextStyleCache = new();

    /// <summary>The template of <paramref name="kind"/> for a control of <paramref name="owner"/> type.</summary>
    /// <param name="owner">The control type (it must own <c>Symbol</c>, <c>Caption</c> and <c>Count</c>).</param>
    /// <param name="kind">The shape.</param>
    /// <param name="selected">The property that says the option is chosen (IsChecked or IsSelected).</param>
    /// <param name="symbol">The control's <c>Symbol</c> property.</param>
    /// <param name="caption">The control's <c>Caption</c> property.</param>
    /// <param name="count">The control's <c>Count</c> property.</param>
    public static ControlTemplate For(
        Type owner,
        OptionKind kind,
        DependencyProperty selected,
        DependencyProperty symbol,
        DependencyProperty caption,
        DependencyProperty count
    ) =>
        Cache.GetOrAdd(
            (owner, kind),
            key => Create(key.Item1, key.Item2, selected, symbol, caption, count)
        );

    /// <summary>The default height of <paramref name="kind"/>; NaN for a rail item, which grows with its label.</summary>
    public static double Height(OptionKind kind) =>
        kind switch
        {
            OptionKind.Card => ControlHeight.Large,
            OptionKind.ListRow => ControlHeight.Medium,
            OptionKind.Rail => double.NaN,
            _ => ControlHeight.Small,
        };

    /// <summary>The corner radius of <paramref name="kind"/>.</summary>
    public static double Radius(OptionKind kind) =>
        kind switch
        {
            OptionKind.Segment => Radii.Small,
            OptionKind.Chip => Radii.Pill,
            _ => Radii.Medium,
        };

    /// <summary>The inner padding of <paramref name="kind"/>.</summary>
    public static Thickness Padding(OptionKind kind) =>
        kind switch
        {
            OptionKind.Segment => new Thickness(Space.S2, 0, Space.S2, 0),
            OptionKind.Card => new Thickness(Space.S2, 0, Space.S2, 0),
            OptionKind.Rail => new Thickness(Space.S1, Space.S2, Space.S1, Space.S2),
            _ => new Thickness(Space.S3, 0, Space.S3, 0),
        };

    /// <summary>Whether <paramref name="kind"/> is drawn on the card with its line at rest.</summary>
    public static bool IsFilled(OptionKind kind) =>
        kind is OptionKind.Tile or OptionKind.Card or OptionKind.Chip;

    /// <summary>Paints the rest look of <paramref name="kind"/> on <paramref name="control"/>.</summary>
    public static void Paint(Control control, OptionKind kind)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (IsFilled(kind))
        {
            control.SetResourceReference(
                Control.BackgroundProperty,
                ThemeBrushKey.For(ColorToken.Card)
            );
            control.SetResourceReference(
                Control.BorderBrushProperty,
                ThemeBrushKey.For(ColorToken.Border)
            );
        }
        else
        {
            control.Background = Brushes.Transparent;
            control.BorderBrush = Brushes.Transparent;
        }

        control.SetResourceReference(
            Control.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        ControlChrome.SetRadius(control, Radius(kind));
        control.Padding = Padding(kind);
        control.Height = Height(kind);
        if (kind == OptionKind.Rail)
        {
            control.MinHeight = ControlHeight.Large;
        }
    }

    /// <summary>
    /// The style of the text an option makes of a string: <paramref name="lines"/> lines with «…», centered or
    /// leading, in the line height of the <c>label</c> style.
    /// </summary>
    public static Style Text(int lines, bool centered, TextStyleToken style) =>
        TextStyleCache.GetOrAdd(
            (lines * 4) + (centered ? 2 : 0) + (style == TextStyleToken.LabelSmall ? 1 : 0),
            _ =>
            {
                var spec = TextStyles.Get(style);
                var result = new Style(typeof(TextBlock));
                result.Setters.Add(
                    new Setter(
                        TextBlock.TextWrappingProperty,
                        lines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap
                    )
                );
                result.Setters.Add(
                    new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis)
                );
                result.Setters.Add(
                    new Setter(
                        TextBlock.TextAlignmentProperty,
                        centered ? TextAlignment.Center : TextAlignment.Left
                    )
                );
                result.Setters.Add(
                    new Setter(
                        TextBlock.LineStackingStrategyProperty,
                        LineStackingStrategy.BlockLineHeight
                    )
                );
                result.Setters.Add(new Setter(TextBlock.LineHeightProperty, spec.LineHeight));
                result.Setters.Add(
                    new Setter(FrameworkElement.MaxHeightProperty, spec.LineHeight * lines)
                );
                result.Seal();
                return result;
            }
        );

    private static ControlTemplate Create(
        Type owner,
        OptionKind kind,
        DependencyProperty selected,
        DependencyProperty symbol,
        DependencyProperty caption,
        DependencyProperty count
    )
    {
        var column = kind is OptionKind.Card or OptionKind.Rail;
        var iconSize = kind is OptionKind.Tile or OptionKind.Chip or OptionKind.Segment
            ? IconBox.Small
            : IconBox.Medium;
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(symbol))
            .With(SymbolIcon.SizeProperty, iconSize)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        if (kind == OptionKind.ListRow)
        {
            icon.Paint(SymbolIcon.ForegroundProperty, ColorToken.Muted);
        }

        var text = Templates
            .Element<ContentPresenter>(TextPart)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        FrameworkElementFactory body;
        var triggers = new List<TriggerBase>
        {
            Templates.When(
                ContentControl.ContentProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, TextPart)
            ),
            Templates.When(
                UIElement.IsMouseOverProperty,
                true,
                Templates.Brush(
                    Border.BackgroundProperty,
                    ColorToken.CardHi,
                    ControlChrome.FillPart
                )
            ),
        };
        var selectedSetters = new List<SetterBase>
        {
            new Setter(SymbolIcon.IsFilledProperty, true, IconPart),
            Templates.Brush(SymbolIcon.ForegroundProperty, ColorToken.AccentInk, IconPart),
        };

        switch (kind)
        {
            case OptionKind.Card:
                icon.With(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, Space.S1));
                text.With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                body = Templates
                    .Element<StackPanel>(ControlChrome.ContentPart)
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
                    .Add(icon, text);
                triggers.Add(Collapse(symbol, null, IconPart));
                break;
            case OptionKind.Rail:
                var counter = Templates
                    .Element<Counter>(CountPart)
                    .With(Counter.CountProperty, Templates.Bind(count))
                    .With(FrameworkElement.MarginProperty, new Thickness(Space.S1, 0, 0, 0))
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
                // The counter goes beside the icon, in a slot of its own: it never covers it.
                var head = Templates
                    .Element<StackPanel>()
                    .With(StackPanel.OrientationProperty, Orientation.Horizontal)
                    .With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center)
                    .With(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, Space.S1))
                    .Add(icon, counter);
                text.With(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                body = Templates
                    .Element<StackPanel>(ControlChrome.ContentPart)
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
                    .Add(head, text);
                break;
            case OptionKind.ListRow:
                icon.With(FrameworkElement.MarginProperty, new Thickness(0, 0, Space.S3, 0))
                    .With(DockPanel.DockProperty, Dock.Left);
                var note = Templates
                    .Element<StyledText>(CaptionPart)
                    .With(TextBlock.TextProperty, Templates.Bind(caption))
                    .With(StyledText.TextStyleProperty, TextStyleToken.Caption)
                    .With(StyledText.InkProperty, ColorToken.Muted);
                var lines = Templates
                    .Element<StackPanel>()
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
                    .Add(text, note);
                body = Templates
                    .Element<DockPanel>(ControlChrome.ContentPart)
                    .With(DockPanel.LastChildFillProperty, true)
                    .Add(icon, lines);
                triggers.Add(Collapse(symbol, null, IconPart));
                triggers.Add(Collapse(caption, null, CaptionPart));
                triggers.Add(Collapse(caption, string.Empty, CaptionPart));
                break;
            default:
                var check = Templates
                    .Element<SymbolIcon>(CheckPart)
                    .With(SymbolIcon.SymbolProperty, CheckSymbol)
                    .With(SymbolIcon.SizeProperty, IconBox.Small)
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
                    .With(FrameworkElement.MarginProperty, new Thickness(0, 0, Space.S1, 0))
                    .With(UIElement.VisibilityProperty, Visibility.Collapsed)
                    .Paint(SymbolIcon.ForegroundProperty, ColorToken.AccentInk);
                icon.With(FrameworkElement.MarginProperty, new Thickness(0, 0, Space.S2, 0));
                // A dock, not a horizontal stack: the text gets the width that is left, so it can wrap or end in «…».
                check.With(DockPanel.DockProperty, Dock.Left);
                icon.With(DockPanel.DockProperty, Dock.Left);
                body = Templates
                    .Element<DockPanel>(ControlChrome.ContentPart)
                    .With(DockPanel.LastChildFillProperty, true)
                    .Add(check, icon, text);
                triggers.Add(Collapse(symbol, null, IconPart));
                var bare = new MultiTrigger();
                bare.Conditions.Add(new Condition(symbol, null));
                bare.Conditions.Add(new Condition(IsTextContentProperty, true));
                bare.Conditions.Add(new Condition(selected, true));
                bare.Setters.Add(
                    new Setter(UIElement.VisibilityProperty, Visibility.Visible, CheckPart)
                );
                triggers.Add(
                    Templates.When(selected, true, ControlChrome.Selected([.. selectedSetters]))
                );
                triggers.Add(bare);
                triggers.Add(
                    Templates.When(UIElement.IsEnabledProperty, false, ControlChrome.Disabled())
                );
                return Templates.Seal(owner, Box(body), [.. triggers]);
        }

        triggers.Add(Templates.When(selected, true, ControlChrome.Selected([.. selectedSetters])));
        triggers.Add(Templates.When(UIElement.IsEnabledProperty, false, ControlChrome.Disabled()));
        return Templates.Seal(owner, Box(body), [.. triggers]);
    }

    private static FrameworkElementFactory Box(FrameworkElementFactory body) =>
        Templates
            .Element<TouchTargetBox>()
            .With(
                TouchTargetBox.VisualWidthProperty,
                Templates.Bind(FrameworkElement.WidthProperty)
            )
            .With(
                TouchTargetBox.VisualHeightProperty,
                Templates.Bind(FrameworkElement.HeightProperty)
            )
            .Add(ControlChrome.Layers(body));

    private static Trigger Collapse(DependencyProperty property, object? value, string part) =>
        Templates.When(
            property,
            value,
            new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, part)
        );
}
