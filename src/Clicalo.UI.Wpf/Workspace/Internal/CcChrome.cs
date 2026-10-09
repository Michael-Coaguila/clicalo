using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Workspace.Internal;

/// <summary>
/// The look of every button of the Control Center (docs/07 «Formas»): a rounded border with the fill, outline and
/// text of theme tokens, a light veil while the pointer is over it and while it is pressed, and the focus ring of
/// docs/07 (3 px accent, offset 2). Colors are resource references, so a theme change repaints in place (TEM-002).
/// </summary>
internal static class CcChrome
{
    /// <summary>Identifies the corner radius of the chrome.</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.RegisterAttached(
        "Radius",
        typeof(CornerRadius),
        typeof(CcChrome),
        new FrameworkPropertyMetadata(new CornerRadius(Radii.Button))
    );

    private const string ChromePart = "PART_Chrome";
    private const string VeilPart = "PART_Veil";
    private const double HoverOpacity = 0.08;
    private const double PressedOpacity = 0.16;
    private const double DisabledOpacity = 0.45;

    /// <summary>The template of <see cref="CcButton"/>.</summary>
    public static ControlTemplate ButtonTemplate { get; } = Create(typeof(ButtonBase));

    /// <summary>The template of <see cref="CcToggle"/>.</summary>
    public static ControlTemplate ToggleTemplate { get; } = Create(typeof(ToggleButton));

    /// <summary>Paints <paramref name="control"/>: fill, outline and text; null is transparent.</summary>
    public static void Paint(Control control, ColorToken? fill, ColorToken ink, ColorToken? stroke)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (fill is { } background)
        {
            control.SetResourceReference(Control.BackgroundProperty, ThemeBrushKey.For(background));
        }
        else
        {
            control.Background = Brushes.Transparent;
        }

        control.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(ink));
        if (stroke is { } border)
        {
            control.SetResourceReference(Control.BorderBrushProperty, ThemeBrushKey.For(border));
        }
        else
        {
            control.BorderBrush = Brushes.Transparent;
        }
    }

    private static ControlTemplate Create(Type type)
    {
        var chrome = new FrameworkElementFactory(typeof(Border), ChromePart);
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
        chrome.SetValue(Border.CornerRadiusProperty, new TemplateBindingExtension(RadiusProperty));
        chrome.SetValue(UIElement.SnapsToDevicePixelsProperty, true);

        var veil = new FrameworkElementFactory(typeof(Border), VeilPart);
        veil.SetValue(Border.CornerRadiusProperty, new TemplateBindingExtension(RadiusProperty));
        veil.SetValue(UIElement.OpacityProperty, 0d);
        veil.SetValue(UIElement.IsHitTestVisibleProperty, false);
        veil.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Text));

        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(
            FrameworkElement.MarginProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        content.SetValue(
            FrameworkElement.HorizontalAlignmentProperty,
            new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)
        );
        content.SetValue(
            FrameworkElement.VerticalAlignmentProperty,
            new TemplateBindingExtension(Control.VerticalContentAlignmentProperty)
        );
        content.SetValue(ContentPresenter.RecognizesAccessKeyProperty, false);

        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(chrome);
        root.AppendChild(veil);
        root.AppendChild(content);

        var template = new ControlTemplate(type) { VisualTree = root };
        template.Triggers.Add(
            When(
                UIElement.IsMouseOverProperty,
                true,
                new Setter(UIElement.OpacityProperty, HoverOpacity, VeilPart)
            )
        );
        template.Triggers.Add(
            When(
                ButtonBase.IsPressedProperty,
                true,
                new Setter(UIElement.OpacityProperty, PressedOpacity, VeilPart)
            )
        );
        template.Triggers.Add(
            When(
                UIElement.IsEnabledProperty,
                false,
                new Setter(UIElement.OpacityProperty, DisabledOpacity)
            )
        );
        template.Seal();
        return template;
    }

    private static Trigger When(DependencyProperty property, object value, Setter setter)
    {
        var trigger = new Trigger { Property = property, Value = value };
        trigger.Setters.Add(setter);
        return trigger;
    }
}
