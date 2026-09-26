using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The default look of <see cref="ShortcutTile"/>: a <see cref="TouchTargetBox"/> (the touch target, drawn at the
/// tile's <c>Width</c> × <c>Height</c> when that is smaller, REG-02), a rounded chrome, the localized name, the
/// accessible state as text (ACC-003: the state is never color alone) and the voice number badge (ACC-009: warn
/// fill with dark text). Every color is a <see cref="ThemeBrushKey"/> resource, so <see cref="ThemeScope"/> repaints
/// it in place, including the system contrast colors (TEM-001).
/// </summary>
/// <remarks>
/// The final look of the tiles (icon, keys, categories, sizes S/M/L) arrives with the panel in milestone M3 and its
/// render snapshots; the parts that accessibility depends on are fixed here. The template is sealed, so one instance
/// serves every UI thread.
/// </remarks>
public static class ShortcutTileTemplate
{
    /// <summary>Name of the chrome border inside the template.</summary>
    public const string ChromePart = "PART_Chrome";

    /// <summary>Name of the text block that shows the tile's name.</summary>
    public const string LabelPart = "PART_Label";

    /// <summary>Name of the text block that shows the accessible state.</summary>
    public const string StatePart = "PART_State";

    /// <summary>Name of the voice number badge.</summary>
    public const string VoiceNumberPart = "PART_VoiceNumber";

    /// <summary>The default template of <see cref="ShortcutTile"/>.</summary>
    public static ControlTemplate Default { get; } = Create();

    private static ControlTemplate Create()
    {
        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(TouchTargetBox.VisualWidthProperty, Bind(FrameworkElement.WidthProperty));
        box.SetValue(TouchTargetBox.VisualHeightProperty, Bind(FrameworkElement.HeightProperty));

        var chrome = new FrameworkElementFactory(typeof(Border), ChromePart);
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Tile));
        chrome.SetValue(Border.PaddingProperty, Bind(Control.PaddingProperty));
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        chrome.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        chrome.SetResourceReference(Border.BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        chrome.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        box.AppendChild(chrome);

        var layout = new FrameworkElementFactory(typeof(Grid));
        chrome.AppendChild(layout);

        var label = new FrameworkElementFactory(typeof(TextBlock), LabelPart);
        label.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.AccessibleNameProperty));
        label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        label.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        layout.AppendChild(label);

        var state = new FrameworkElementFactory(typeof(TextBlock), StatePart);
        state.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.AccessibleStateProperty));
        state.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        state.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Bottom);
        state.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        layout.AppendChild(state);

        var badge = new FrameworkElementFactory(typeof(Border), VoiceNumberPart);
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Compact));
        badge.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        badge.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        badge.SetValue(
            Border.PaddingProperty,
            new Thickness(Radii.Compact / 2, 0, Radii.Compact / 2, 0)
        );
        badge.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Warn));
        layout.AppendChild(badge);

        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetBinding(
            TextBlock.TextProperty,
            new Binding(nameof(ShortcutTile.VoiceNumber))
            {
                RelativeSource = RelativeSource.TemplatedParent,
                Mode = BindingMode.OneWay,
            }
        );
        number.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.OnWarn)
        );
        badge.AppendChild(number);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Triggers.Add(Collapse(ShortcutTile.VoiceNumberProperty, null, VoiceNumberPart));
        template.Triggers.Add(
            Collapse(ShortcutTile.AccessibleStateProperty, string.Empty, StatePart)
        );
        template.Triggers.Add(Collapse(ShortcutTile.AccessibleStateProperty, null, StatePart));
        template.Triggers.Add(
            Paint(
                ShortcutTile.ToggleStateProperty,
                ToggleState.On,
                (ChromePart, Border.BackgroundProperty, ColorToken.AccentWash),
                (ChromePart, Border.BorderBrushProperty, ColorToken.Accent)
            )
        );
        template.Triggers.Add(
            Paint(
                ShortcutTile.ToggleStateProperty,
                ToggleState.Indeterminate,
                (ChromePart, Border.BackgroundProperty, ColorToken.Accent),
                (ChromePart, Border.BorderBrushProperty, ColorToken.Accent),
                (LabelPart, TextBlock.ForegroundProperty, ColorToken.OnAccent),
                (StatePart, TextBlock.ForegroundProperty, ColorToken.OnAccent)
            )
        );
        template.Triggers.Add(
            Paint(
                ShortcutTile.IsExpandedProperty,
                true,
                (ChromePart, Border.BorderBrushProperty, ColorToken.Accent)
            )
        );
        template.Seal();
        return template;
    }

    private static TemplateBindingExtension Bind(DependencyProperty property) => new(property);

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
