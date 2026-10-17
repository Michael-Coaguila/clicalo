using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>
/// The look of the «Release all» button of the panic strip (SEG-002) in direction C (D12): the danger button on the
/// danger strip, a <c>card</c> of radius <see cref="Radii.Medium"/> with the name in bold <c>dangerText</c> and the line
/// <c>line</c>, <see cref="ControlHeight.Small"/> high, never smaller than the 44 × 44 target
/// (<see cref="TouchTargetBox"/>, REG-02). Every color is a <see cref="ThemeBrushKey"/>, so the system contrast colors
/// apply in place (TEM-001). Sealed: one instance serves every UI thread.
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
        chrome.SetValue(Border.CornerRadiusProperty, new CornerRadius(Radii.Medium));
        chrome.SetValue(
            Border.PaddingProperty,
            new TemplateBindingExtension(Control.PaddingProperty)
        );
        chrome.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        chrome.SetResourceReference(Border.BorderBrushProperty, ThemeBrushKey.For(ColorToken.Line));
        chrome.SetResourceReference(Border.BorderThicknessProperty, ThemeScope.BorderThicknessKey);
        box.AppendChild(chrome);

        var label = new FrameworkElementFactory(typeof(StyledText));
        label.SetValue(
            TextBlock.TextProperty,
            new TemplateBindingExtension(ShortcutTile.AccessibleNameProperty)
        );
        label.SetValue(StyledText.TextStyleProperty, TextStyleToken.Label);
        label.SetValue(StyledText.InkProperty, ColorToken.DangerText);
        label.SetValue(StyledText.MaxLinesProperty, 1);
        label.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        label.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        chrome.AppendChild(label);

        var template = new ControlTemplate(typeof(ShortcutTile)) { VisualTree = box };
        template.Seal();
        return template;
    }
}
