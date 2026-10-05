using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The look of the «Release all» button of the panic strip (SEG-002): white with red text on the danger strip, never
/// smaller than the 44 × 44 target (<see cref="TouchTargetBox"/>, REG-02). Every color is a <see cref="ThemeBrushKey"/>,
/// so the system contrast colors apply in place (TEM-001). Sealed: one instance serves every UI thread.
/// </summary>
internal static class PanicButtonTemplate
{
    /// <summary>The template.</summary>
    public static ControlTemplate Default { get; } = Create();

    private static ControlTemplate Create()
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

        var chrome = new FrameworkElementFactory(typeof(Border));
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Button));
        chrome.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        chrome.SetResourceReference(
            Border.BackgroundProperty,
            ThemeBrushKey.For(ColorToken.OnDanger)
        );
        chrome.SetResourceReference(
            Border.BorderBrushProperty,
            ThemeBrushKey.For(ColorToken.Danger)
        );
        chrome.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        box.AppendChild(chrome);

        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(
            TextBlock.TextProperty,
            new TemplateBindingExtension(ShortcutTile.AccessibleNameProperty)
        );
        label.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        label.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetResourceReference(
            TextBlock.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Danger)
        );
        chrome.AppendChild(label);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }
}
