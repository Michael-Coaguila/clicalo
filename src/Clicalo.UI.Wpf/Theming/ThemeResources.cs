using System.Windows;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// Writes the theme resources into a <see cref="ResourceDictionary"/> (TEM-002, TEM-003, TEM-005, CUA-011): one
/// frozen brush per <see cref="ThemeBrushKey"/> and <see cref="CategoryBrushKey"/>, the border thickness, the bundled
/// font families, the type scale and reduce motion. Shared by <see cref="ThemeScope"/> and <see cref="ThemeService"/>,
/// so both publish the same keys. Existing entries are replaced in place: elements that reference them dynamically
/// repaint without rebuilding the tree.
/// </summary>
internal static class ThemeResources
{
    /// <summary>The colors and the border thickness of <paramref name="palette"/>.</summary>
    public static void WritePalette(ResourceDictionary resources, ThemePalette palette)
    {
        foreach (var key in ThemeBrushKey.All)
        {
            resources[key] = palette.CreateBrush(key.Token);
        }

        foreach (var key in CategoryBrushKey.All)
        {
            resources[key] = key.IsWash
                ? palette.CreateCategoryWashBrush(key.Category)
                : palette.CreateCategoryTintBrush(key.Category);
        }

        resources[ThemeScope.BorderThicknessKey] = new Thickness(palette.BorderThickness);
    }

    /// <summary>The font families and the type scale, the scaled sizes at <paramref name="textScalePercent"/>.</summary>
    public static void WriteTypography(ResourceDictionary resources, int textScalePercent)
    {
        resources[ThemeKeys.UiFont] = AppFonts.Ui;
        resources[ThemeKeys.MonoFont] = AppFonts.Mono;
        resources[ThemeKeys.SymbolsFont] = AppFonts.Symbols;
        resources[ThemeKeys.SymbolsFilledFont] = AppFonts.SymbolsFilled;
        resources[ThemeKeys.TextScalePercent] = textScalePercent;
        foreach (var step in TypeScale.Steps)
        {
            resources[ThemeKeys.TextSize(step)] = step;
            resources[ThemeKeys.ScaledTextSize(step)] = TypeScale.Scale(step, textScalePercent);
        }
    }

    /// <summary>Whether transitions take 0 ms (TEM-006).</summary>
    public static void WriteMotion(ResourceDictionary resources, bool reduceMotion) =>
        resources[ThemeKeys.ReduceMotion] = reduceMotion;
}
