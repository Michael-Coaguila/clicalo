using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// The look shared by <see cref="TouchButton"/> and <see cref="IconButton"/> (prototype v4, docs/07 «Formas»): a
/// <see cref="TouchTargetBox"/> (REG-02) that draws a 10 px rounded chrome at the button's <c>Width</c> ×
/// <c>Height</c>, with an optional icon before the content.
/// </summary>
internal static class ButtonChrome
{
    /// <summary>Name of the rounded border.</summary>
    public const string ChromePart = "PART_Chrome";

    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Name of the content presenter.</summary>
    public const string ContentPart = "PART_Content";

    /// <summary>Space between the icon and the content (prototype: gap 6).</summary>
    private const double IconGap = 6;

    /// <summary>Opacity of a disabled button (no contrast minimum applies to disabled controls).</summary>
    public const double DisabledOpacity = 0.45;

    /// <summary>The template of both buttons.</summary>
    public static ControlTemplate Template { get; } = Create();

    /// <summary>Paints <paramref name="control"/> with the tokens of <paramref name="appearance"/>.</summary>
    public static void Apply(Control control, ButtonAppearance appearance)
    {
        var (background, foreground, border) = Tokens(appearance);
        Templates.UseTheme(
            control,
            background ?? ColorToken.Card,
            foreground,
            border ?? ColorToken.Border
        );
        if (background is null)
        {
            control.Background = Brushes.Transparent;
        }

        if (border is null)
        {
            control.BorderBrush = Brushes.Transparent;
        }
    }

    /// <summary>Fill, text and outline tokens; null is transparent.</summary>
    public static (ColorToken? Background, ColorToken Foreground, ColorToken? Border) Tokens(
        ButtonAppearance appearance
    ) =>
        appearance switch
        {
            ButtonAppearance.Accent => (ColorToken.Accent, ColorToken.OnAccent, ColorToken.Accent),
            ButtonAppearance.Neutral => (ColorToken.CardHi, ColorToken.Text, ColorToken.CardHi),
            ButtonAppearance.Outline => (null, ColorToken.Text, ColorToken.Border),
            ButtonAppearance.Ghost => (null, ColorToken.Text, null),
            ButtonAppearance.Danger => (ColorToken.Danger, ColorToken.OnDanger, ColorToken.Danger),
            ButtonAppearance.Warn => (ColorToken.Warn, ColorToken.OnWarn, ColorToken.Warn),
            _ => throw new ArgumentOutOfRangeException(nameof(appearance), appearance, null),
        };

    private static ControlTemplate Create()
    {
        var icon = Templates
            .Element<SymbolIcon>(IconPart)
            .With(SymbolIcon.SymbolProperty, Templates.Bind(TouchButton.SymbolProperty))
            .With(SymbolIcon.SizeProperty, Templates.Bind(TouchButton.IconSizeProperty))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        var content = Templates
            .Element<ContentPresenter>(ContentPart)
            .With(FrameworkElement.MarginProperty, new Thickness(IconGap, 0, 0, 0))
            .With(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center)
            .With(ContentPresenter.RecognizesAccessKeyProperty, false);
        var row = Templates
            .Element<StackPanel>()
            .With(StackPanel.OrientationProperty, Orientation.Horizontal)
            .With(
                FrameworkElement.HorizontalAlignmentProperty,
                Templates.Bind(Control.HorizontalContentAlignmentProperty)
            )
            .With(
                FrameworkElement.VerticalAlignmentProperty,
                Templates.Bind(Control.VerticalContentAlignmentProperty)
            )
            .Add(icon, content);
        var chrome = Templates.Chrome(ChromePart, Radii.Button).Add(row);
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
            .Add(chrome);

        return Templates.Seal(
            typeof(Button),
            box,
            Templates.When(
                TouchButton.SymbolProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, IconPart),
                new Setter(FrameworkElement.MarginProperty, new Thickness(0), ContentPart)
            ),
            Templates.When(
                ContentControl.ContentProperty,
                null,
                new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, ContentPart)
            ),
            Templates.When(
                UIElement.IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, DisabledOpacity, ChromePart)
            )
        );
    }
}
