using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The content of a theme option in direction C (D12), the same in Ajustes rápidos of the panel and of the Tab view,
/// «General y panel» and the welcome (GEN-003, BIE-008, AJR-001): the swatch of the theme, its name in one line with «…»
/// and ✓ at the end while chosen, left aligned, as the quick settings of the mockup.
/// </summary>
public static class ThemeSwatch
{
    /// <summary>
    /// The row that goes inside a <see cref="OptionKind.Tile"/> option stretched to its width: swatch, name and the ✓
    /// of «elegido» at the end.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <param name="label">Its visible name.</param>
    /// <param name="selected">Whether it is the theme in force.</param>
    /// <returns>The row.</returns>
    public static DockPanel Row(ThemeChoice theme, string label, bool selected)
    {
        var row = new DockPanel { LastChildFill = true };
        var swatch = For(theme);
        swatch.Margin = new Thickness(0, 0, Space.S2, 0);
        DockPanel.SetDock(swatch, Dock.Left);
        row.Children.Add(swatch);
        if (selected)
        {
            var check = new SymbolIcon
            {
                Symbol = "check",
                Size = IconBox.Small,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(Space.S2, 0, 0, 0),
            };
            check.SetResourceReference(
                SymbolIcon.ForegroundProperty,
                ThemeBrushKey.For(ColorToken.AccentInk)
            );
            DockPanel.SetDock(check, Dock.Right);
            row.Children.Add(check);
        }

        row.Children.Add(
            new StyledText(label, TextStyleToken.Label)
            {
                MaxLines = 1,
                VerticalAlignment = VerticalAlignment.Center,
            }
        );
        return row;
    }

    /// <summary>
    /// The swatch of a theme: a circle of <see cref="IconBox.Small"/> split between the background and the accent of
    /// its palette («Como Windows»: the light and the dark background), with the control line of the current theme.
    /// </summary>
    /// <param name="theme">The theme.</param>
    /// <returns>The swatch.</returns>
    public static Ellipse For(ThemeChoice theme)
    {
        var (first, second) = theme switch
        {
            ThemeChoice.Light => Pair(
                ThemeId.Light,
                ColorToken.Win,
                ThemeId.Light,
                ColorToken.Accent
            ),
            ThemeChoice.Dark => Pair(ThemeId.Dark, ColorToken.Win, ThemeId.Dark, ColorToken.Accent),
            ThemeChoice.HighContrast => Pair(
                ThemeId.HighContrast,
                ColorToken.Win,
                ThemeId.HighContrast,
                ColorToken.Accent
            ),
            _ => Pair(ThemeId.Light, ColorToken.Win, ThemeId.Dark, ColorToken.Win),
        };
        var fill = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
        };
        fill.GradientStops.Add(new GradientStop(first, 0));
        fill.GradientStops.Add(new GradientStop(first, 0.5));
        fill.GradientStops.Add(new GradientStop(second, 0.5));
        fill.GradientStops.Add(new GradientStop(second, 1));
        fill.Freeze();
        var swatch = new Ellipse
        {
            Width = IconBox.Small,
            Height = IconBox.Small,
            Fill = fill,
            StrokeThickness = Stroke.Line,
            VerticalAlignment = VerticalAlignment.Center,
        };
        swatch.SetResourceReference(Shape.StrokeProperty, ThemeBrushKey.For(ColorToken.Line));
        return swatch;

        static (Color, Color) Pair(ThemeId a, ColorToken at, ThemeId b, ColorToken bt) =>
            (ThemeCatalog.GetPalette(a).GetColor(at), ThemeCatalog.GetPalette(b).GetColor(bt));
    }
}
