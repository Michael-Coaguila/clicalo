namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// What Windows says about the look of apps (TEM-001, TEM-006): the light or dark mode of apps
/// (<c>AppsUseLightTheme</c>), whether a contrast theme is on (<c>SPI_GETHIGHCONTRAST</c>) and whether client area
/// animations are on (<c>SPI_GETCLIENTAREAANIMATION</c>).
/// </summary>
/// <param name="AppsUseLightTheme">True when Windows shows apps in light mode.</param>
/// <param name="HighContrast">True while a Windows contrast theme is on; it always wins (TEM-001).</param>
/// <param name="ClientAreaAnimation">False when the person turned Windows animations off (reduce motion).</param>
public readonly record struct SystemThemeState(
    bool AppsUseLightTheme,
    bool HighContrast,
    bool ClientAreaAnimation
);
