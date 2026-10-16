using System.Windows;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// A button that shows only an icon (the panel header's Buscar, Editar, Ajustes rápidos and Minimizar; 🎤 beside a
/// field; the − and + of a <see cref="StepSlider"/>): a square of <see cref="ControlSize.Small"/> (44) or
/// <see cref="ControlSize.Medium"/> (52) with the icon in a box of <see cref="IconBox.Medium"/>. Tertiary by default
/// (no fill, icon in <c>text</c>, a wash under the pointer); <see cref="ButtonVariant.Secondary"/> adds the card and
/// its line. With <see cref="TouchButton.IsOn"/> it is a mode switch drawn pressed exactly as «elegido».
/// </summary>
/// <remarks>
/// An icon is not a name (UIA008): set <c>AutomationProperties.Name</c> to the localized action, or UI Automation
/// sees a nameless button. Like every <see cref="TouchButton"/>, it responds on at least 44 × 44 (REG-02).
/// </remarks>
public sealed class IconButton : TouchButton
{
    static IconButton()
    {
        VariantProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(ButtonVariant.Tertiary)
        );
        IconSizeProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(IconBox.Medium)
        );
        PaddingProperty.OverrideMetadata(
            typeof(IconButton),
            new FrameworkPropertyMetadata(new Thickness(0))
        );
    }

    /// <summary>Creates a tertiary icon button of 44 × 44.</summary>
    public IconButton() => Width = Height;
}
