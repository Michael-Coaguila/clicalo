using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The default look of <see cref="ShortcutTile"/>, as the prototype's tile (CUA-007, CUA-009): a
/// <see cref="TouchTargetBox"/> (the touch target, drawn at the tile's <c>Width</c> × <c>Height</c> when that is
/// smaller, REG-02) with a 12 px rounded <c>card</c> chrome and a <c>border</c> outline; inside, the icon in the
/// category tint, the bold name and the key line in JetBrains Mono and <c>muted</c>; the badge at the top right (type
/// or state, in the category tint on its wash; ACC-003: the state is never color alone) and the voice number at the
/// top left (ACC-009: <c>warn</c> fill with dark text). Every color is a theme resource, so <see cref="ThemeScope"/>
/// and <see cref="ThemeService"/> repaint it in place, including the system contrast colors (TEM-001).
/// </summary>
/// <remarks>
/// States (CUA-009): Alternar on, category wash and a 2 px tint outline; held, the same at scale 0.95; armed, wash
/// and a 2 px <c>warn</c> outline; flash, wash; sticky key locked (indeterminate), <c>accent</c> fill. Sizes, the
/// text scale and the timing of the flash come from the view. The template is sealed, so one instance serves every
/// UI thread.
/// </remarks>
public static class ShortcutTileTemplate
{
    /// <summary>Name of the chrome border inside the template.</summary>
    public const string ChromePart = "PART_Chrome";

    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Name of the text block that shows the tile's name.</summary>
    public const string LabelPart = "PART_Label";

    /// <summary>Name of the text block of the key line.</summary>
    public const string KeysPart = "PART_Keys";

    /// <summary>Name of the badge at the top right.</summary>
    public const string BadgePart = "PART_Badge";

    /// <summary>Name of the text block that shows the badge text (the accessible state or the type).</summary>
    public const string StatePart = "PART_State";

    /// <summary>Name of the voice number badge.</summary>
    public const string VoiceNumberPart = "PART_VoiceNumber";

    /// <summary>Scale of a held tile (CUA-009).</summary>
    public const double HeldScale = 0.95;

    /// <summary>Outline of an active, held or armed tile (CUA-009).</summary>
    public const double ActiveBorderThickness = 2;

    private const double Inset = 4;

    /// <summary>The default template of <see cref="ShortcutTile"/>.</summary>
    public static ControlTemplate Default { get; } = Create();

    private static ControlTemplate Create()
    {
        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(TouchTargetBox.VisualWidthProperty, Bind(FrameworkElement.WidthProperty));
        box.SetValue(TouchTargetBox.VisualHeightProperty, Bind(FrameworkElement.HeightProperty));

        var chrome = new FrameworkElementFactory(typeof(Border), ChromePart);
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Tile));
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        chrome.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
        chrome.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        chrome.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Border)
        );
        chrome.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        box.AppendChild(chrome);

        var layout = new FrameworkElementFactory(typeof(Grid));
        chrome.AppendChild(layout);
        layout.AppendChild(Body());
        layout.AppendChild(TypeBadge());
        layout.AppendChild(VoiceBadge());

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        foreach (var trigger in Triggers())
        {
            template.Triggers.Add(trigger);
        }

        template.Seal();
        return template;
    }

    private static FrameworkElementFactory Body()
    {
        var body = new FrameworkElementFactory(typeof(ShortcutTileBody));
        body.SetValue(FrameworkElement.MarginProperty, Bind(Control.PaddingProperty));
        body.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        body.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);

        var icon = new FrameworkElementFactory(typeof(SymbolIcon), IconPart);
        icon.SetValue(SymbolIcon.SymbolProperty, Bind(ShortcutTile.SymbolProperty));
        icon.SetValue(SymbolIcon.SizeProperty, Bind(ShortcutTile.IconSizeProperty));
        icon.SetValue(SymbolIcon.ForegroundProperty, Bind(ShortcutTile.CategoryTintProperty));
        icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, Inset));
        body.AppendChild(icon);

        var label = new FrameworkElementFactory(typeof(TextBlock), LabelPart);
        label.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.AccessibleNameProperty));
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        label.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        body.AppendChild(label);

        var keys = new FrameworkElementFactory(typeof(TextBlock), KeysPart);
        keys.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.KeysProperty));
        keys.SetValue(TextBlock.FontSizeProperty, Bind(ShortcutTile.KeysFontSizeProperty));
        keys.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        keys.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        keys.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        keys.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        keys.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
        keys.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
        keys.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        body.AppendChild(keys);
        return body;
    }

    private static FrameworkElementFactory TypeBadge()
    {
        var badge = new FrameworkElementFactory(typeof(Border), BadgePart);
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        badge.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        badge.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        badge.SetValue(FrameworkElement.MarginProperty, new Thickness(0, Inset, Inset, 0));
        badge.SetValue(Border.PaddingProperty, new Thickness(3, 0, 3, 0));
        badge.SetValue(Border.BackgroundProperty, Bind(ShortcutTile.CategoryWashProperty));

        var text = new FrameworkElementFactory(typeof(TextBlock), StatePart);
        text.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.BadgeTextProperty));
        text.SetValue(TextBlock.FontSizeProperty, TypeScale.Minimum);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        text.SetValue(TextBlock.ForegroundProperty, Bind(ShortcutTile.CategoryTintProperty));
        badge.AppendChild(text);
        return badge;
    }

    private static FrameworkElementFactory VoiceBadge()
    {
        var badge = new FrameworkElementFactory(typeof(Border), VoiceNumberPart);
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Compact));
        badge.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        badge.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        badge.SetValue(FrameworkElement.MarginProperty, new Thickness(Inset, Inset, 0, 0));
        badge.SetValue(FrameworkElement.MinWidthProperty, 20d);
        badge.SetValue(FrameworkElement.HeightProperty, 20d);
        badge.SetValue(Border.PaddingProperty, new Thickness(4, 0, 4, 0));
        badge.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Warn));

        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetBinding(
            TextBlock.TextProperty,
            new Binding(nameof(ShortcutTile.VoiceNumber))
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay,
            }
        );
        number.SetValue(TextBlock.FontSizeProperty, 12d);
        number.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        number.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        number.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        number.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnWarn)
        );
        badge.AppendChild(number);
        return badge;
    }

    private static IEnumerable<TriggerBase> Triggers()
    {
        yield return Collapse(ShortcutTile.VoiceNumberProperty, null, VoiceNumberPart);
        yield return Collapse(ShortcutTile.BadgeTextProperty, string.Empty, BadgePart);
        yield return Collapse(ShortcutTile.BadgeTextProperty, null, BadgePart);
        yield return Collapse(ShortcutTile.KeysProperty, string.Empty, KeysPart);
        yield return Collapse(ShortcutTile.KeysProperty, null, KeysPart);
        yield return Collapse(ShortcutTile.SymbolProperty, null, IconPart);
        yield return Washed(ShortcutTile.IsFlashingProperty, true);
        yield return Washed(
            ShortcutTile.ToggleStateProperty,
            ToggleState.On,
            Outline(FromTile(ShortcutTile.CategoryTintProperty))
        );
        yield return Washed(
            ShortcutTile.IsArmedProperty,
            true,
            Outline(new DynamicResourceExtension(ThemeBrushKey.For(ColorToken.Warn)))
        );
        yield return Washed(
            ShortcutTile.IsHeldProperty,
            true,
            [
                .. Outline(FromTile(ShortcutTile.CategoryTintProperty)),
                new Setter(
                    UIElement.RenderTransformProperty,
                    new ScaleTransform(HeldScale, HeldScale),
                    ChromePart
                ),
            ]
        );
        yield return Paint(
            ShortcutTile.ToggleStateProperty,
            ToggleState.Indeterminate,
            (ChromePart, Border.BackgroundProperty, ColorToken.Accent),
            (ChromePart, Border.BorderBrushProperty, ColorToken.Accent),
            (LabelPart, TextBlock.ForegroundProperty, ColorToken.OnAccent),
            (KeysPart, TextBlock.ForegroundProperty, ColorToken.OnAccent),
            (IconPart, SymbolIcon.ForegroundProperty, ColorToken.OnAccent)
        );
        yield return Paint(
            ShortcutTile.IsExpandedProperty,
            true,
            (ChromePart, Border.BorderBrushProperty, ColorToken.Accent)
        );
    }

    private static TemplateBindingExtension Bind(DependencyProperty property) => new(property);

    /// <summary>A binding to a tile property for a trigger setter (where a template binding is not allowed).</summary>
    private static Binding FromTile(DependencyProperty property) =>
        new()
        {
            RelativeSource = RelativeSource.TemplatedParent,
            Path = new PropertyPath(property),
        };

    private static Setter[] Outline(object brush) =>
        [
            new Setter(Border.BorderBrushProperty, brush, ChromePart),
            new Setter(
                Border.BorderThicknessProperty,
                new Thickness(ActiveBorderThickness),
                ChromePart
            ),
        ];

    private static Trigger Washed(DependencyProperty property, object value, params Setter[] extra)
    {
        var trigger = new Trigger { Property = property, Value = value };
        trigger.Setters.Add(
            new Setter(
                Border.BackgroundProperty,
                FromTile(ShortcutTile.CategoryWashProperty),
                ChromePart
            )
        );
        foreach (var setter in extra)
        {
            trigger.Setters.Add(setter);
        }

        return trigger;
    }

    private static Trigger Collapse(DependencyProperty property, object? value, string part)
    {
        var trigger = new Trigger { Property = property, Value = value };
        trigger.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, part));
        return trigger;
    }

    private static Trigger Paint(
        DependencyProperty property,
        object value,
        params (string Part, DependencyProperty Property, ColorToken Token)[] setters
    )
    {
        var trigger = new Trigger { Property = property, Value = value };
        foreach (var (part, target, token) in setters)
        {
            trigger.Setters.Add(
                new Setter(target, new DynamicResourceExtension(ThemeBrushKey.For(token)), part)
            );
        }

        return trigger;
    }
}
