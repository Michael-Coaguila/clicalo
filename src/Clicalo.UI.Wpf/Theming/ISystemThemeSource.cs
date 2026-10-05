namespace Clicalo.UI.Wpf.Theming;

/// <summary>The system side of <see cref="ThemeService"/>: reads <see cref="SystemThemeState"/> and says when it may have changed.</summary>
public interface ISystemThemeSource
{
    /// <summary>
    /// Raised, on any thread, when the system state may have changed (<c>WM_SETTINGCHANGE</c>,
    /// <c>WM_SYSCOLORCHANGE</c>, a contrast theme turned on or off). The listener reads <see cref="Read"/> again.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>The current system state.</summary>
    SystemThemeState Read();
}
