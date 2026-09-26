namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// A palette the UI can render with (TEM-001). "Auto" is a user preference that the theme service resolves to one
/// of these values (milestone M3); it is never a palette itself.
/// </summary>
public enum ThemeId
{
    /// <summary>The dark palette of <c>data/tokens</c>.</summary>
    Dark,

    /// <summary>The light palette of <c>data/tokens</c>.</summary>
    Light,

    /// <summary>Clícalo's own high-contrast palette (black, white and #FFE600), chosen inside the app.</summary>
    HighContrast,

    /// <summary>The colors of the active Windows contrast theme, which always win while one is on (TEM-001).</summary>
    SystemHighContrast,
}
