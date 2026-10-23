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

    /// <summary>
    /// The empty slot as wide as the ✓ on the other side of a centred text: the text stays on the option's axis whether
    /// it is chosen or not («✓ M» was 12 off the centre of S and L, D21 review).
    /// </summary>
    public const string CheckBalancePart = "PART_CheckBalance";

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

    /// <summary>
    /// The corner radius of <paramref name="kind"/>: a chip has the radius of every other control (D20), so a row of
    /// filters reads as controls and not as ovals beside the rest of the window.
    /// </summary>
    public static double Radius(OptionKind kind) =>
        kind switch
        {
            OptionKind.Segment => Radii.Small,
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
    public static Style TextStyleFor(int lines, bool centered, TextStyleToken style) =>
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
            // An option of an icon alone keeps no room for a text beside or under it: its icon is centred (it was 4 DIP
            // to the left in «Lado de la pestaña» and the icon grids, D21 review).
            Templates.When(
                ContentControl.ContentProperty,
                null,
                new Setter(FrameworkElement.MarginProperty, new Thickness(0), IconPart)
            ),
            Templates.When(
                ContentControl.ContentProperty,
                string.Empty,
                new Setter(FrameworkElement.MarginProperty, new Thickness(0), IconPart)
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
                    .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
                // The icon on the axis of the label, as the icons of the items without a counter, and the counter
                // beside it, out of the flow: it never covers the icon and is never cut.
                var head = Templates
                    .Element<RailHead>()
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
                var checkBalance = Templates
                    .Element<Border>(CheckBalancePart)
                    .With(FrameworkElement.WidthProperty, IconBox.Small + Space.S1)
                    .With(UIElement.VisibilityProperty, Visibility.Collapsed);
                // Not a horizontal stack: the text gets the width that is left, so it can wrap or end in «…»; and the
                // ✓ never takes that width from it.
                body = Templates
                    .Element<CheckRow>(ControlChrome.ContentPart)
                    .Add(check, icon, checkBalance, text);
                triggers.Add(Collapse(symbol, null, IconPart));
                var bare = new MultiTrigger();
                bare.Conditions.Add(new Condition(symbol, null));
                bare.Conditions.Add(new Condition(IsTextContentProperty, true));
                bare.Conditions.Add(new Condition(selected, true));
                bare.Setters.Add(
                    new Setter(UIElement.VisibilityProperty, Visibility.Visible, CheckPart)
                );
                var centred = new MultiTrigger();
                centred.Conditions.Add(new Condition(symbol, null));
                centred.Conditions.Add(new Condition(IsTextContentProperty, true));
                centred.Conditions.Add(new Condition(selected, true));
                centred.Conditions.Add(
                    new Condition(
                        Control.HorizontalContentAlignmentProperty,
                        HorizontalAlignment.Center
                    )
                );
                centred.Setters.Add(
                    new Setter(UIElement.VisibilityProperty, Visibility.Hidden, CheckBalancePart)
                );
                triggers.Add(
                    Templates.When(selected, true, ControlChrome.Selected([.. selectedSetters]))
                );
                triggers.Add(bare);
                triggers.Add(centred);
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

    /// <summary>
    /// The row of an option of the shapes without a column (tile, chip, segment): the ✓, the icon, the slot that
    /// balances the ✓ on the other side of a centred text, and the text, laid out as a dock panel does (the text gets
    /// the width that is left, so it can wrap or end in «…»). The ✓ and its balance never take that width from the text:
    /// where the whole label does not fit beside them (a key of 44 in the combination box, Ctrl in its row of four),
    /// both are left out and the wash, the ring and the state UI Automation reads say «elegido» alone (EDI-008), so a
    /// chosen key never reads «✓ C…» (D21 review).
    /// </summary>
    /// <remarks>Its children are, in this order, the ✓, the icon, the balance and the text.</remarks>
    private sealed class CheckRow : Panel
    {
        private double _natural;

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            if (InternalChildren.Count != 4)
            {
                return default;
            }

            var unbounded = new Size(double.PositiveInfinity, availableSize.Height);
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(unbounded);
            }

            var (check, icon, balance, text) = Parts();
            _natural = text.DesiredSize.Width;
            var used = icon.DesiredSize.Width + (Fits(availableSize.Width) ? Marks() : 0);
            text.Measure(new Size(Math.Max(0, availableSize.Width - used), availableSize.Height));
            var height = Math.Max(
                Math.Max(check.DesiredSize.Height, icon.DesiredSize.Height),
                Math.Max(balance.DesiredSize.Height, text.DesiredSize.Height)
            );
            return new Size(used + text.DesiredSize.Width, height);
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count != 4)
            {
                return finalSize;
            }

            var (check, icon, balance, text) = Parts();
            var marks = Fits(finalSize.Width);
            var x = 0d;
            if (marks)
            {
                check.Arrange(new Rect(0, 0, check.DesiredSize.Width, finalSize.Height));
                x = check.DesiredSize.Width;
            }
            else
            {
                // Left out: a slot of nothing, so it is drawn nowhere.
                check.Arrange(default);
            }

            icon.Arrange(new Rect(x, 0, icon.DesiredSize.Width, finalSize.Height));
            x += icon.DesiredSize.Width;
            var right = finalSize.Width;
            if (marks)
            {
                right = Math.Max(x, right - balance.DesiredSize.Width);
                balance.Arrange(new Rect(right, 0, balance.DesiredSize.Width, finalSize.Height));
            }
            else
            {
                balance.Arrange(default);
            }

            text.Arrange(new Rect(x, 0, Math.Max(0, right - x), finalSize.Height));
            return finalSize;
        }

        private (UIElement Check, UIElement Icon, UIElement Balance, UIElement Text) Parts() =>
            (InternalChildren[0], InternalChildren[1], InternalChildren[2], InternalChildren[3]);

        private double Marks() =>
            InternalChildren[0].DesiredSize.Width + InternalChildren[2].DesiredSize.Width;

        // A pixel of slack: layout rounding can take a fraction from a text that then ends in «…».
        private bool Fits(double width) =>
            Marks() <= 0
            || InternalChildren[1].DesiredSize.Width + Marks() + _natural <= width + Stroke.Line;
    }

    /// <summary>
    /// The head of a rail item: the icon on the axis of the label, as the icons of the items without a counter, and the
    /// counter beside it, out of the flow. A hidden twin of the counter on the other side kept the icon on the axis but
    /// made the head wider than the item of the rail, so the counter was cut flat on its right (D21 review): here the
    /// counter starts <see cref="Space.S1"/> after the icon and, where that would take it past the padding of the
    /// item, moves left just enough to stay whole inside the ring of «elegido».
    /// </summary>
    /// <remarks>Its children are, in this order, the icon and the counter. It takes the whole width it is given.</remarks>
    private sealed class RailHead : Panel
    {
        /// <summary>How far past the head the counter may reach: the padding of the item, less its ring and a line.</summary>
        private const double Reach = Space.S1 - Stroke.Selected - Stroke.Line;

        /// <inheritdoc />
        protected override Size MeasureOverride(Size availableSize)
        {
            var height = 0d;
            foreach (UIElement child in InternalChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                height = Math.Max(height, child.DesiredSize.Height);
            }

            var icon = InternalChildren.Count > 0 ? InternalChildren[0].DesiredSize.Width : 0;
            return new Size(
                double.IsInfinity(availableSize.Width) ? icon : availableSize.Width,
                height
            );
        }

        /// <inheritdoc />
        protected override Size ArrangeOverride(Size finalSize)
        {
            if (InternalChildren.Count != 2)
            {
                return finalSize;
            }

            var icon = InternalChildren[0];
            var counter = InternalChildren[1];
            var left = (finalSize.Width - icon.DesiredSize.Width) / 2;
            icon.Arrange(new Rect(left, 0, icon.DesiredSize.Width, finalSize.Height));
            var x = Math.Min(
                left + icon.DesiredSize.Width + Space.S1,
                finalSize.Width + Reach - counter.DesiredSize.Width
            );
            counter.Arrange(
                new Rect(
                    Math.Max(0, x),
                    0,
                    counter.DesiredSize.Width,
                    Math.Max(finalSize.Height, counter.DesiredSize.Height)
                )
            );
            return finalSize;
        }
    }
}
