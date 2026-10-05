using System.Windows;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A button that shows only an icon (the panel header's Buscar, Editar, Ajustes rápidos and Minimizar; the − and + of
/// a <see cref="StepSlider"/>). Without a fill by default (<see cref="ButtonAppearance.Ghost"/>), icon of 22 px.
/// </summary>
/// <remarks>
/// An icon is not a name (UIA008): set <c>AutomationProperties.Name</c> to the localized action, or UI Automation
/// sees a nameless button. Like every <see cref="TouchButton"/>, it responds on at least 44 × 44 (REG-02): a 36 × 36
/// button is drawn at 36 × 36 inside a 44 × 44 target.
/// </remarks>
public sealed class IconButton : TouchButton
{
    /// <summary>Default size of the icon (prototype header: 22 px).</summary>
    public const double DefaultIconSize = 22;

    static IconButton()
    {
        AppearanceProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(ButtonAppearance.Ghost)
        );
        IconSizeProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(DefaultIconSize)
        );
        PaddingProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(new Thickness(0))
        );
    }
}
