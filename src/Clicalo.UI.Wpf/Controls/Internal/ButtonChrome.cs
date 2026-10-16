using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// The look of <see cref="TouchButton"/> and <see cref="IconButton"/> in direction C (D12): a
/// <see cref="TouchTargetBox"/> (REG-02) around the layered <see cref="ControlChrome"/> of radius
/// <see cref="Radii.Medium"/>, with an optional icon in a box of <see cref="TouchButton.IconSize"/> before the text.
/// Pressed (<see cref="TouchButton.IsOn"/> or <see cref="TouchButton.IsActive"/>) looks exactly like «elegido»: wash, ring of 2 inside and a filled icon.
/// </summary>
internal static class ButtonChrome
{
    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Name of the text presenter.</summary>
    public const string TextPart = "PART_Text";

    /// <summary>The template of both buttons.</summary>
    public static ControlTemplate Template { get; } = Create();

    /// <summary>The style of the text a button makes of a string: one line with «…».</summary>
    public static Style OneLine { get; } = TextStyle(1);

    /// <summary>The style of the text a button makes of a string: two lines with «…».</summary>
    public static Style TwoLines { get; } = TextStyle(2);

    /// <summary>Paints <paramref name="button"/> with the tokens of <paramref name="variant"/>.</summary>
    public static void Apply(TouchButton button, ButtonVariant variant)
    {
        var (fill, ink, line) = Tokens(variant, button is IconButton);
        if (fill is { } background)
        {
            button.SetResourceReference(Control.BackgroundProperty, ThemeBrushKey.For(background));
        }
        else
        {
            button.Background = Brushes.Transparent;
        }

        button.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(ink));
        if (line is { } stroke)
        {
            button.SetResourceReference(Control.BorderBrushProperty, ThemeBrushKey.For(stroke));
        }
        else
        {
            button.BorderBrush = Brushes.Transparent;
        }
    }

    /// <summary>Fill, text and rest line of a variant; null is transparent.</summary>
    public static (ColorToken? Fill, ColorToken Ink, ColorToken? Line) Tokens(
        ButtonVariant variant,
        bool iconOnly
    ) =>
        variant switch
        {
            ButtonVariant.Primary => (ColorToken.Accent, ColorToken.OnAccent, null),
            ButtonVariant.Secondary => (ColorToken.Card, ColorToken.Text, ColorToken.Line),
            // An icon button without fill draws its icon in text (the header tools); a text one in accent ink.
            ButtonVariant.Tertiary => (null, iconOnly ? ColorToken.Text : ColorToken.AccentInk, null),
            ButtonVariant.Danger => (ColorToken.Card, ColorToken.DangerText, ColorToken.Line),
            _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, null),
        };

    /// <summary>The fill under the pointer: a step of wash, never another size or shape.</summary>
    public static ColorToken? Hover(ButtonVariant variant) =>
        variant switch
        {
            ButtonVariant.Secondary or ButtonVariant.Tertiary => ColorToken.AccentWash,
            ButtonVariant.Danger => ColorToken.DangerWash,
            _ => null,
        };

    private static ControlTemplate Create()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(TouchButton.SymbolProperty))
            .With(SymbolIcon.SizeProperty, Templates.Bind(TouchButton.IconSizeProperty))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(FrameworkElement.MarginProperty, new Thickness(-Space.S1, 0, Space.S2, 0))
            .With(DockPanel.DockProperty, Dock.Left);
        var text = Templates
            .Element<ContentPresenter>(TextPart)
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        // The text takes what the icon leaves: one line with «…» (or two where the view says so).
        var row = Templates
            .Element<DockPanel>(ControlChrome.ContentPart)
            .With(DockPanel.LastChildFillProperty, true)
            .Add(icon, text);
        var box = Templates
            .Element<TouchTargetBox>()
            .With(
                TouchTargetBox.VisualWidthProperty,
                Templates.Bind(FrameworkElement.WidthProperty)
            )
            .With(
                TouchTargetBox.VisualHeightProperty,
                Templates.Bind(FrameworkElement.HeightProperty)
            )
            .Add(ControlChrome.Layers(row));

        var triggers = new List<TriggerBase>
        {
            Templates.When(
                TouchButton.SymbolProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart)
            ),
            Templates.When(
                ContentControl.ContentProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, TextPart),
                new Setter(FrameworkElement.MarginProperty, new Thickness(0), IconPart)
            ),
        };
        foreach (var variant in Enum.GetValues<ButtonVariant>())
        {
            if (Hover(variant) is { } hover)
            {
                var over = new MultiTrigger();
                over.Conditions.Add(new Condition(TouchButton.VariantProperty, variant));
                over.Conditions.Add(new Condition(UIElement.IsMouseOverProperty, true));
                over.Setters.Add(
                    Templates.Brush(Border.BackgroundProperty, hover, ControlChrome.FillPart)
                );
                triggers.Add(over);
            }
        }

        foreach (var (property, value) in new (DependencyProperty, object)[]
        {
            (TouchButton.IsOnProperty, true),
            (TouchButton.IsActiveProperty, true),
        })
        {
            triggers.Add(
                Templates.When(
                    property,
                    value,
                    ControlChrome.Selected(
                        new Setter(SymbolIcon.IsFilledProperty, true, IconPart),
                        Templates.Brush(SymbolIcon.ForegroundProperty, ColorToken.AccentInk, IconPart),
                        Templates.Brush(
                            TextElement.ForegroundProperty,
                            ColorToken.Text,
                            ControlChrome.ContentPart
                        )
                    )
                )
            );
        }
        triggers.Add(Templates.When(UIElement.IsEnabledProperty, false, ControlChrome.Disabled()));
        return Templates.Seal(typeof(Button), box, [.. triggers]);
    }

    private static Style TextStyle(int lines)
    {
        var spec = TextStyles.Label;
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(
            new Setter(
                TextBlock.TextWrappingProperty,
                lines == 1 ? TextWrapping.NoWrap : TextWrapping.Wrap
            )
        );
        style.Setters.Add(
            new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis)
        );
        style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center));
        style.Setters.Add(
            new Setter(TextBlock.LineStackingStrategyProperty, LineStackingStrategy.BlockLineHeight)
        );
        style.Setters.Add(new Setter(TextBlock.LineHeightProperty, spec.LineHeight));
        style.Setters.Add(new Setter(FrameworkElement.MaxHeightProperty, spec.LineHeight * lines));
        style.Seal();
        return style;
    }
}
