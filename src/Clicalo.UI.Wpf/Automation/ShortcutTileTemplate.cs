using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Automation;

/// <summary>
/// The look of <see cref="ShortcutTile"/> in direction C (D12; CUA-007, CUA-009): a <see cref="TouchTargetBox"/> (the
/// touch target, drawn at the tile's <c>Width</c> × <c>Height</c> when that is smaller, REG-02) with a <c>card</c> of
/// radius <see cref="Radii.Medium"/> and its line inside, and an anatomy of reserved slots that never overlap: the
/// voice number at the top left (ACC-009: <c>warn</c> fill with dark text), the type at the top right (an icon of
/// <see cref="IconBox.Badge"/> in <c>muted</c>), the icon in the category tint in its fixed box, the bold name in one
/// line with «…» and the key line in JetBrains Mono and <c>muted</c>. The state in words (ACC-003, EJE-010) takes the
/// place of the key line, in the interface font and <c>accentInk</c>, so it never covers the icon or the voice number.
/// Every color is a theme resource, so <see cref="ThemeScope"/> and <see cref="ThemeService"/> repaint it in place
/// (TEM-001).
/// </summary>
/// <remarks>
/// States (CUA-009), all inside the same box: Alternar on, the single look of «elegido» (accent wash, a ring of
/// <see cref="Stroke.Selected"/> inside and the icon and the type filled in <c>accentInk</c>); held, the same at scale
/// 0.95; armed, <c>warnWash</c> and a ring, the type and the state in <c>warnText</c>; flash, accent wash; sticky key locked (indeterminate), <c>accent</c> fill; menu open
/// (expanded), the accent ring. The template is sealed, so one instance serves every UI thread.
/// </remarks>
public static class ShortcutTileTemplate
{
    /// <summary>Name of the layers of the tile (what a held tile scales).</summary>
    public const string LayersPart = "PART_Layers";

    /// <summary>Name of the fill of the tile.</summary>
    public const string ChromePart = "PART_Chrome";

    /// <summary>Name of the rest line.</summary>
    public const string LinePart = "PART_Line";

    /// <summary>Name of the ring of «elegido», armed and expanded.</summary>
    public const string RingPart = "PART_Ring";

    /// <summary>Name of the icon.</summary>
    public const string IconPart = "PART_Icon";

    /// <summary>Name of the text block that shows the tile's name.</summary>
    public const string LabelPart = "PART_Label";

    /// <summary>Name of the text block of the key line, which shows the state while there is one.</summary>
    public const string KeysPart = "PART_Keys";

    /// <summary>Name of the slot at the top right (the type or the state).</summary>
    public const string BadgePart = "PART_Badge";

    /// <summary>Name of the icon of the type, in the top right slot.</summary>
    public const string TypePart = "PART_Type";

    /// <summary>Name of the text block that shows the badge word of a tile without a type icon.</summary>
    public const string StatePart = "PART_State";

    /// <summary>Name of the voice number badge.</summary>
    public const string VoiceNumberPart = "PART_VoiceNumber";

    /// <summary>Scale of a held tile (CUA-009).</summary>
    public const double HeldScale = 0.95;

    /// <summary>Ring of an active, held, armed or expanded tile (CUA-009).</summary>
    public const double ActiveBorderThickness = Stroke.Selected;

    /// <summary>The default template of <see cref="ShortcutTile"/>.</summary>
    public static ControlTemplate Default { get; } = Create();

    private static ControlTemplate Create()
    {
        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(TouchTargetBox.VisualWidthProperty, Bind(FrameworkElement.WidthProperty));
        box.SetValue(TouchTargetBox.VisualHeightProperty, Bind(FrameworkElement.HeightProperty));

        var layers = new FrameworkElementFactory(typeof(Grid), LayersPart);
        layers.SetValue(UIElement.RenderTransformOriginProperty, new Point(0.5, 0.5));
        box.AppendChild(layers);

        var chrome = new FrameworkElementFactory(typeof(Border), ChromePart);
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Medium));
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        chrome.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        layers.AppendChild(chrome);

        var line = Outline(LinePart, ColorToken.Border);
        line.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        layers.AppendChild(line);
        var ring = Outline(RingPart, ColorToken.Accent);
        ring.SetValue(Shape.StrokeThicknessProperty, ActiveBorderThickness);
        ring.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        layers.AppendChild(ring);

        layers.AppendChild(Body());
        layers.AppendChild(TypeSlot());
        layers.AppendChild(VoiceBadge());

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        foreach (var trigger in Triggers())
        {
            template.Triggers.Add(trigger);
        }

        template.Seal();
        return template;
    }

    private static FrameworkElementFactory Outline(string name, ColorToken token)
    {
        var shape = new FrameworkElementFactory(typeof(Rectangle), name);
        shape.SetValue(Rectangle.RadiusXProperty, Radii.Medium);
        shape.SetValue(Rectangle.RadiusYProperty, Radii.Medium);
        shape.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        shape.SetValue(UIElement.IsHitTestVisibleProperty, false);
        shape.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(token));
        return shape;
    }

    private static FrameworkElementFactory Body()
    {
        var body = new FrameworkElementFactory(typeof(ShortcutTileBody));
        body.SetValue(FrameworkElement.MarginProperty, Bind(Control.PaddingProperty));
        body.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        body.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        body.SetValue(ShortcutTileBody.NameLinesProperty, Bind(ShortcutTile.MaxNameLinesProperty));
        body.SetValue(
            ShortcutTileBody.ReservesNameLinesProperty,
            Bind(ShortcutTile.ReservesNameLinesProperty)
        );

        var icon = new FrameworkElementFactory(typeof(SymbolIcon), IconPart);
        icon.SetValue(SymbolIcon.SymbolProperty, Bind(ShortcutTile.SymbolProperty));
        icon.SetValue(SymbolIcon.SizeProperty, Bind(ShortcutTile.IconSizeProperty));
        icon.SetValue(SymbolIcon.ForegroundProperty, Bind(ShortcutTile.CategoryTintProperty));
        icon.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        icon.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 0, Space.S1));
        body.AppendChild(icon);

        var label = new FrameworkElementFactory(typeof(TextBlock), LabelPart);
        label.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.AccessibleNameProperty));
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        // Names break between words only: a word longer than the tile ends in «…», it is never cut in two.
        label.SetValue(TextBlock.TextWrappingProperty, TextWrapping.WrapWithOverflow);
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        label.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
        body.AppendChild(label);

        var keys = new FrameworkElementFactory(typeof(TextBlock), KeysPart);
        keys.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.FootTextProperty));
        keys.SetValue(TextBlock.FontSizeProperty, Bind(ShortcutTile.KeysFontSizeProperty));
        keys.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
        keys.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        keys.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        keys.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        keys.SetValue(FrameworkElement.MarginProperty, new Thickness(0, Space.S1, 0, 0));
        keys.SetResourceReference(TextBlock.FontFamilyProperty, ThemeKeys.MonoFont);
        keys.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        body.AppendChild(keys);
        return body;
    }

    private static FrameworkElementFactory TypeSlot()
    {
        var slot = new FrameworkElementFactory(typeof(Grid), BadgePart);
        slot.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right);
        slot.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        slot.SetValue(FrameworkElement.MarginProperty, new Thickness(0, Space.S1, Space.S1, 0));
        slot.SetValue(UIElement.IsHitTestVisibleProperty, false);

        var type = new FrameworkElementFactory(typeof(SymbolIcon), TypePart);
        type.SetValue(SymbolIcon.SymbolProperty, Bind(ShortcutTile.TypeSymbolProperty));
        type.SetValue(SymbolIcon.SizeProperty, IconBox.Badge);
        type.SetResourceReference(
            SymbolIcon.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        slot.AppendChild(type);

        var text = new FrameworkElementFactory(typeof(TextBlock), StatePart);
        text.SetValue(TextBlock.TextProperty, Bind(ShortcutTile.BadgeTextProperty));
        text.SetValue(TextBlock.FontSizeProperty, TypeScale.Minimum);
        text.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        text.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        text.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Muted)
        );
        slot.AppendChild(text);
        return slot;
    }

    private static FrameworkElementFactory VoiceBadge()
    {
        var badge = new FrameworkElementFactory(typeof(Border), VoiceNumberPart);
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(IconBox.Small / 2));
        badge.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        badge.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Top);
        badge.SetValue(FrameworkElement.MarginProperty, new Thickness(Space.S1, Space.S1, 0, 0));
        badge.SetValue(FrameworkElement.MinWidthProperty, IconBox.Small);
        badge.SetValue(FrameworkElement.HeightProperty, IconBox.Small);
        badge.SetValue(Border.PaddingProperty, new Thickness(Space.S1, 0, Space.S1, 0));
        badge.SetValue(UIElement.IsHitTestVisibleProperty, false);
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
        number.SetValue(TextBlock.FontSizeProperty, TextStyles.LabelSmall.Size);
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
        yield return Collapse(ShortcutTile.BadgeTextProperty, string.Empty, StatePart);
        yield return Collapse(ShortcutTile.BadgeTextProperty, null, StatePart);
        // The slot at the top right holds the type icon, or the badge word of a tile without one; never the state.
        yield return Collapse(ShortcutTile.IsTypeShownProperty, false, TypePart);
        yield return Collapse(ShortcutTile.FootTextProperty, string.Empty, KeysPart);
        yield return Collapse(ShortcutTile.FootTextProperty, null, KeysPart);

        // The state in words takes the place of the key line (ACC-003, EJE-010): words, so the interface font.
        yield return Set(
            ShortcutTile.IsStateShownProperty,
            true,
            new Setter(
                TextBlock.FontFamilyProperty,
                new DynamicResourceExtension(ThemeKeys.UiFont),
                KeysPart
            ),
            new Setter(TextBlock.FontWeightProperty, FontWeights.Bold, KeysPart),
            Brush(KeysPart, TextBlock.ForegroundProperty, ColorToken.AccentInk)
        );
        yield return Collapse(ShortcutTile.SymbolProperty, null, IconPart);
        yield return Set(
            ShortcutTile.MaxNameLinesProperty,
            1,
            new Setter(TextBlock.TextWrappingProperty, TextWrapping.NoWrap, LabelPart)
        );
        yield return Set(
            ShortcutTile.IsFlashingProperty,
            true,
            Brush(ChromePart, Border.BackgroundProperty, ColorToken.AccentWash)
        );
        yield return Set(ShortcutTile.ToggleStateProperty, ToggleState.On, Chosen());
        yield return Set(
            ShortcutTile.IsArmedProperty,
            true,
            Brush(ChromePart, Border.BackgroundProperty, ColorToken.WarnWash),
            new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart),
            Brush(RingPart, Shape.StrokeProperty, ColorToken.WarnText),
            Brush(TypePart, SymbolIcon.ForegroundProperty, ColorToken.WarnText),
            Brush(KeysPart, TextBlock.ForegroundProperty, ColorToken.WarnText)
        );
        yield return Set(
            ShortcutTile.IsHeldProperty,
            true,
            [
                .. Chosen(),
                new Setter(
                    UIElement.RenderTransformProperty,
                    new ScaleTransform(HeldScale, HeldScale),
                    LayersPart
                ),
            ]
        );
        yield return Set(
            ShortcutTile.ToggleStateProperty,
            ToggleState.Indeterminate,
            Brush(ChromePart, Border.BackgroundProperty, ColorToken.Accent),
            Brush(LabelPart, TextBlock.ForegroundProperty, ColorToken.OnAccent),
            Brush(KeysPart, TextBlock.ForegroundProperty, ColorToken.OnAccent),
            Brush(IconPart, SymbolIcon.ForegroundProperty, ColorToken.OnAccent),
            Brush(TypePart, SymbolIcon.ForegroundProperty, ColorToken.OnAccent)
        );
        yield return Set(
            ShortcutTile.IsExpandedProperty,
            true,
            new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart)
        );
    }

    /// <summary>The single look of «elegido»: wash, the ring of 2 inside, the icon filled and the type in accent.</summary>
    private static Setter[] Chosen() =>
        [
            Brush(ChromePart, Border.BackgroundProperty, ColorToken.AccentWash),
            new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart),
            new Setter(SymbolIcon.IsFilledProperty, true, IconPart),
            new Setter(SymbolIcon.IsFilledProperty, true, TypePart),
            Brush(TypePart, SymbolIcon.ForegroundProperty, ColorToken.AccentInk),
        ];

    private static TemplateBindingExtension Bind(DependencyProperty property) => new(property);

    private static Setter Brush(string part, DependencyProperty property, ColorToken token) =>
        new(property, new DynamicResourceExtension(ThemeBrushKey.For(token)), part);

    private static Trigger Set(DependencyProperty property, object value, params Setter[] setters)
    {
        var trigger = new Trigger { Property = property, Value = value };
        foreach (var setter in setters)
        {
            trigger.Setters.Add(setter);
        }

        return trigger;
    }

    private static Trigger Collapse(DependencyProperty property, object? value, string part) =>
        Set(property, value!, new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, part));
}
