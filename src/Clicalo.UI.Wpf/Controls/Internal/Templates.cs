using System.Windows;
using System.Windows.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls.Internal;

/// <summary>
/// Helpers to build the sealed control templates of the base controls in code. Colors are always theme resources
/// (<see cref="ThemeBrushKey"/>), so a theme change repaints in place (TEM-002).
/// </summary>
internal static class Templates
{
    /// <summary>A new element factory.</summary>
    public static FrameworkElementFactory Element<T>(string? name = null)
        where T : FrameworkElement => new(typeof(T), name);

    /// <summary>A template binding to <paramref name="property"/> of the templated control.</summary>
    public static TemplateBindingExtension Bind(DependencyProperty property) => new(property);

    /// <summary>References the brush of <paramref name="token"/> for <paramref name="property"/>.</summary>
    public static FrameworkElementFactory Paint(
        this FrameworkElementFactory element,
        DependencyProperty property,
        ColorToken token
    )
    {
        element.SetResourceReference(property, ThemeBrushKey.For(token));
        return element;
    }

    /// <summary>Sets a local value of the element.</summary>
    public static FrameworkElementFactory With(
        this FrameworkElementFactory element,
        DependencyProperty property,
        object? value
    )
    {
        element.SetValue(property, value);
        return element;
    }

    /// <summary>Appends <paramref name="children"/> to <paramref name="element"/>.</summary>
    public static FrameworkElementFactory Add(
        this FrameworkElementFactory element,
        params FrameworkElementFactory[] children
    )
    {
        foreach (var child in children)
        {
            element.AppendChild(child);
        }

        return element;
    }

    /// <summary>A rounded border that draws the control's background, border brush and theme border thickness.</summary>
    public static FrameworkElementFactory Chrome(string name, double cornerRadius) =>
        Element<Border>(name)
            .With(Border.CornerRadiusProperty, new CornerRadius(cornerRadius))
            .With(Border.BackgroundProperty, Bind(Control.BackgroundProperty))
            .With(Border.BorderBrushProperty, Bind(Control.BorderBrushProperty))
            .With(Border.BorderThicknessProperty, Bind(Control.BorderThicknessProperty))
            .With(Border.PaddingProperty, Bind(Control.PaddingProperty))
            .With(UIElement.SnapsToDevicePixelsProperty, true);

    /// <summary>A setter of <paramref name="property"/> to the brush of <paramref name="token"/>.</summary>
    public static Setter Brush(
        DependencyProperty property,
        ColorToken token,
        string? part = null
    ) =>
        part is null
            ? new Setter(property, new DynamicResourceExtension(ThemeBrushKey.For(token)))
            : new Setter(property, new DynamicResourceExtension(ThemeBrushKey.For(token)), part);

    /// <summary>A setter of <paramref name="property"/> to a theme resource.</summary>
    public static Setter Resource(DependencyProperty property, object key, string? part = null) =>
        part is null
            ? new Setter(property, new DynamicResourceExtension(key))
            : new Setter(property, new DynamicResourceExtension(key), part);

    /// <summary>A trigger on <paramref name="property"/> = <paramref name="value"/>.</summary>
    public static Trigger When(
        DependencyProperty property,
        object? value,
        params SetterBase[] setters
    )
    {
        var trigger = new Trigger { Property = property, Value = value };
        foreach (var setter in setters)
        {
            trigger.Setters.Add(setter);
        }

        return trigger;
    }

    /// <summary>A sealed template of <paramref name="root"/> with <paramref name="triggers"/>.</summary>
    public static ControlTemplate Seal(
        Type controlType,
        FrameworkElementFactory root,
        params TriggerBase[] triggers
    )
    {
        var template = new ControlTemplate(controlType) { VisualTree = root };
        foreach (var trigger in triggers)
        {
            template.Triggers.Add(trigger);
        }

        template.Seal();
        return template;
    }

    /// <summary>
    /// Gives <paramref name="control"/> the theme's brushes, border thickness and interface font as resource
    /// references (a local value set later by a view wins), so a theme change repaints it in place.
    /// </summary>
    public static void UseTheme(
        Control control,
        ColorToken background,
        ColorToken foreground,
        ColorToken borderBrush
    )
    {
        control.SetResourceReference(Control.BackgroundProperty, ThemeBrushKey.For(background));
        control.SetResourceReference(Control.ForegroundProperty, ThemeBrushKey.For(foreground));
        control.SetResourceReference(Control.BorderBrushProperty, ThemeBrushKey.For(borderBrush));
        control.SetResourceReference(
            Control.BorderThicknessProperty,
            ThemeScope.BorderThicknessKey
        );
        control.SetResourceReference(Control.FontFamilyProperty, ThemeKeys.UiFont);
    }
}
