using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces.Panel;

/// <summary>
/// The looks of the few controls of the panel that need the UI Automation of a <see cref="ShortcutTile"/> (a three-state
/// Toggle for the sticky keys, an ExpandCollapse over the title, Invoke for a page dot), drawn with the tokens of
/// direction C (D12): a <see cref="TouchTargetBox"/> (REG-02) with the chrome of a control (fill, rest line inside and
/// the ring of «elegido» inside, so it never changes size) and the element in <see cref="FrameworkElement.Tag"/> as its
/// content. Colors are theme resources, so a theme change repaints in place. Templates are sealed: one instance serves
/// every UI thread. It also builds the small parts every view of the panel shares (texts, icons, marks and the surface
/// of a layer).
/// </summary>
internal static class PanelChrome
{
    /// <summary>The fill of a key or a dot.</summary>
    public const string FillPart = "PART_Fill";

    /// <summary>The rest line of a key.</summary>
    public const string LinePart = "PART_Line";

    /// <summary>The ring of «elegido» of a key.</summary>
    public const string RingPart = "PART_Ring";

    /// <summary>
    /// The look of a key of the sticky row (FIJ-005): a card of <see cref="Radii.Medium"/> with its line; once (On), the
    /// single look of «elegido»; locked (Indeterminate), the accent fill; under a mouse or a touchpad, <c>cardHi</c>.
    /// </summary>
    public static ControlTemplate Key { get; } = CreateKey();

    /// <summary>
    /// The look of a segment inside a track (the row «Qué ver»): no fill at rest and, while its <c>ToggleState</c> is On,
    /// the single look of «elegido» (wash and the ring of 2 inside), whatever its UI Automation pattern; under a mouse or
    /// a touchpad, <c>cardHi</c>.
    /// </summary>
    public static ControlTemplate Segment { get; } = CreateSegment();

    /// <summary>The look of an invisible button laid over something drawn (the title of the header, SEL-006).</summary>
    public static ControlTemplate Plain { get; } = CreatePlain();

    /// <summary>The look of a page dot: a pill in the control's background (CUA-004).</summary>
    public static ControlTemplate Dot { get; } = CreateDot();

    /// <summary>
    /// The look of a control painted by its own brushes (<see cref="Paint"/>): a box of radius
    /// <see cref="Radii.Medium"/> with the control's background, border and padding. The small buttons of the Tab bar use
    /// it (<c>SurfaceParts.SmallButton</c>).
    /// </summary>
    public static ControlTemplate Button { get; } = CreateBordered();

    /// <summary>A new control drawn with <paramref name="template"/>, unfocusable (REG-01).</summary>
    /// <param name="template">One of the looks.</param>
    /// <param name="pattern">The UI Automation pattern.</param>
    public static ShortcutTile NewButton(ControlTemplate template, ShortcutTilePattern pattern) =>
        new()
        {
            Template = template,
            Pattern = pattern,
            Focusable = false,
            IsTabStop = false,
        };

    /// <summary>Paints a control drawn with <see cref="Button"/>; a <see langword="null"/> token is transparent.</summary>
    /// <param name="control">The control.</param>
    /// <param name="background">Its fill.</param>
    /// <param name="foreground">Its text and icons.</param>
    /// <param name="border">Its outline.</param>
    public static void Paint(
        Control control,
        ColorToken? background,
        ColorToken foreground,
        ColorToken? border
    )
    {
        ArgumentNullException.ThrowIfNull(control);
        SetBrush(control, Control.BackgroundProperty, background);
        SetBrush(control, Control.BorderBrushProperty, border);
        control.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(foreground));
        control.SetResourceReference(
            Control.BorderThicknessProperty,
            ThemeScope.BorderThicknessKey
        );
    }

    /// <summary>A brush property bound to a theme token, or transparent.</summary>
    /// <param name="element">The element.</param>
    /// <param name="property">The brush property.</param>
    /// <param name="token">The token, or <see langword="null"/> for transparent.</param>
    public static void SetBrush(
        FrameworkElement element,
        DependencyProperty property,
        ColorToken? token
    )
    {
        if (token is { } color)
        {
            element.SetResourceReference(property, ThemeBrushKey.For(color));
        }
        else
        {
            element.SetValue(property, Brushes.Transparent);
        }
    }

    /// <summary>A text of the type scale in one line with «…» (or <paramref name="lines"/>), in <paramref name="ink"/>.</summary>
    /// <param name="style">Its style.</param>
    /// <param name="ink">Its color.</param>
    /// <param name="lines">Lines before «…»; 0 for no limit.</param>
    public static StyledText Text(
        TextStyleToken style,
        ColorToken ink = ColorToken.Text,
        int lines = 1
    ) => new(string.Empty, style, ink) { MaxLines = lines };

    /// <summary>An icon in its fixed box, in <paramref name="ink"/>.</summary>
    /// <param name="symbol">The Material Symbols name; null for none yet.</param>
    /// <param name="box">The box (<see cref="IconBox"/>).</param>
    /// <param name="ink">Its color.</param>
    public static SymbolIcon Icon(string? symbol, double box, ColorToken ink)
    {
        var icon = new SymbolIcon
        {
            Symbol = symbol,
            Size = box,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        icon.SetResourceReference(SymbolIcon.ForegroundProperty, ThemeBrushKey.For(ink));
        return icon;
    }

    /// <summary>
    /// A dot of <see cref="Space.S2"/> in <paramref name="ink"/>: the mark of the app in front (SEL-001) or of a pending
    /// suggestion (SEL-005). It is drawn only; its control says the same in its item status.
    /// </summary>
    /// <param name="ink">Its color.</param>
    public static Border Mark(ColorToken ink)
    {
        var dot = new Border
        {
            Width = Space.S2,
            Height = Space.S2,
            CornerRadius = new CornerRadius(Space.S1),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
            IsHitTestVisible = false,
        };
        dot.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ink));
        return dot;
    }

    /// <summary>
    /// The surface of a layer of the panel (direction C: temporary states are layers over the content, never blocks
    /// that push it): the opaque <c>win</c> surface with the decorative line on top, at the same level as the panel, so
    /// its controls are the second level and never more than two borders nest.
    /// </summary>
    /// <param name="content">What the layer shows.</param>
    public static Border Layer(UIElement content)
    {
        var layer = new Border
        {
            Child = content,
            BorderThickness = new Thickness(0, Stroke.Line, 0, 0),
        };
        layer.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Win));
        layer.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Border)
        );
        return layer;
    }

    /// <summary>Sets the name UI Automation reads.</summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The localized name.</param>
    public static void Name(DependencyObject element, string name) =>
        AutomationProperties.SetName(element, name);

    private static FrameworkElementFactory Box()
    {
        var box = new FrameworkElementFactory(typeof(TouchTargetBox));
        box.SetValue(
            TouchTargetBox.VisualWidthProperty,
            new TemplateBindingExtension(FrameworkElement.WidthProperty)
        );
        box.SetValue(
            TouchTargetBox.VisualHeightProperty,
            new TemplateBindingExtension(FrameworkElement.HeightProperty)
        );
        return box;
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

    private static ControlTemplate CreateKey()
    {
        var box = Box();
        var layers = new FrameworkElementFactory(typeof(Grid));
        box.AppendChild(layers);

        var fill = new FrameworkElementFactory(typeof(Border), FillPart);
        fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Medium));
        fill.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        fill.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        layers.AppendChild(fill);

        var line = Outline(LinePart, ColorToken.Line);
        line.SetResourceReference(Shape.StrokeThicknessProperty, ThemeKeys.LineThickness);
        layers.AppendChild(line);

        var ring = Outline(RingPart, ColorToken.Accent);
        ring.SetValue(Shape.StrokeThicknessProperty, Stroke.Selected);
        ring.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        layers.AppendChild(ring);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(
            ContentPresenter.ContentProperty,
            new TemplateBindingExtension(FrameworkElement.TagProperty)
        );
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        layers.AppendChild(content);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Triggers.Add(Hover());
        var once = new Trigger
        {
            Property = ShortcutTile.ToggleStateProperty,
            Value = ToggleState.On,
        };
        once.Setters.Add(Brush(Border.BackgroundProperty, ColorToken.AccentWash, FillPart));
        once.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart));
        once.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, LinePart));
        template.Triggers.Add(once);
        var locked = new Trigger
        {
            Property = ShortcutTile.ToggleStateProperty,
            Value = ToggleState.Indeterminate,
        };
        locked.Setters.Add(Brush(Border.BackgroundProperty, ColorToken.Accent, FillPart));
        locked.Setters.Add(
            new Setter(UIElement.VisibilityProperty, Visibility.Collapsed, LinePart)
        );
        template.Triggers.Add(locked);
        template.Seal();
        return template;
    }

    private static ControlTemplate CreateSegment()
    {
        var box = Box();
        var layers = new FrameworkElementFactory(typeof(Grid));
        box.AppendChild(layers);

        var fill = new FrameworkElementFactory(typeof(Border), FillPart);
        fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Small));
        fill.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        fill.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        layers.AppendChild(fill);

        var ring = new FrameworkElementFactory(typeof(Rectangle), RingPart);
        ring.SetValue(Rectangle.RadiusXProperty, Radii.Small);
        ring.SetValue(Rectangle.RadiusYProperty, Radii.Small);
        ring.SetValue(Shape.StrokeThicknessProperty, Stroke.Selected);
        ring.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
        ring.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        ring.SetValue(UIElement.IsHitTestVisibleProperty, false);
        ring.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Accent));
        layers.AppendChild(ring);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(
            ContentPresenter.ContentProperty,
            new TemplateBindingExtension(FrameworkElement.TagProperty)
        );
        content.SetValue(
            FrameworkElement.MarginProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        content.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)
        );
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        layers.AppendChild(content);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Triggers.Add(Hover());
        var chosen = new Trigger
        {
            Property = ShortcutTile.ToggleStateProperty,
            Value = ToggleState.On,
        };
        chosen.Setters.Add(Brush(Border.BackgroundProperty, ColorToken.AccentWash, FillPart));
        chosen.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, RingPart));
        template.Triggers.Add(chosen);
        template.Seal();
        return template;
    }

    private static ControlTemplate CreateBordered()
    {
        var box = Box();
        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Medium));
        chrome.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Control.BackgroundProperty)
        );
        chrome.SetValue(
            Border.BorderBrushProperty,
            new TemplateBindingExtension(Control.BorderBrushProperty)
        );
        chrome.SetValue(
            Border.BorderThicknessProperty,
            new TemplateBindingExtension(Control.BorderThicknessProperty)
        );
        chrome.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        box.AppendChild(chrome);

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(
            ContentPresenter.ContentProperty,
            new TemplateBindingExtension(FrameworkElement.TagProperty)
        );
        content.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)
        );
        content.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            new TemplateBindingExtension(Control.VerticalContentAlignmentProperty)
        );
        chrome.AppendChild(content);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }

    private static ControlTemplate CreatePlain()
    {
        var box = Box();
        var fill = new FrameworkElementFactory(typeof(Border));
        fill.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        box.AppendChild(fill);
        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }

    private static ControlTemplate CreateDot()
    {
        var box = Box();
        var fill = new FrameworkElementFactory(typeof(Border), FillPart);
        fill.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Pill));
        fill.SetValue(
            Border.BackgroundProperty,
            new TemplateBindingExtension(Control.BackgroundProperty)
        );
        fill.SetValue(UIElement.SnapsToDevicePixelsProperty, true);
        box.AppendChild(fill);
        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }

    /// <summary>
    /// The fill under a mouse or a touchpad, as on the tiles and the controls around them: <c>cardHi</c>, never another
    /// line, size or shape. It comes before the states, so «elegido» and locked still paint the control under the pointer.
    /// </summary>
    private static Trigger Hover()
    {
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(Brush(Border.BackgroundProperty, ColorToken.CardHi, FillPart));
        return over;
    }

    private static Setter Brush(DependencyProperty property, ColorToken token, string part) =>
        new(property, new DynamicResourceExtension(ThemeBrushKey.For(token)), part);
}
