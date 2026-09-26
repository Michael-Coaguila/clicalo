using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>Access to the generated palettes by <see cref="ThemeId"/>.</summary>
public static class ThemeCatalog
{
    /// <summary>
    /// The palette of <paramref name="theme"/>. Generated palettes are shared immutable instances; for
    /// <see cref="ThemeId.SystemHighContrast"/> the result is a snapshot of the current Windows contrast colors,
    /// so a new one must be taken after <c>WM_SYSCOLORCHANGE</c>.
    /// </summary>
    public static ThemePalette GetPalette(ThemeId theme) =>
        theme switch
        {
            ThemeId.Dark => ThemePalettes.Dark,
            ThemeId.Light => ThemePalettes.Light,
            ThemeId.HighContrast => ThemePalettes.HighContrast,
            ThemeId.SystemHighContrast => SystemHighContrastPalette.Capture(),
            _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, message: null),
        };
}
