using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// <see cref="ISystemThemeSource"/> of Windows (blueprint §8.4): the app mode from
/// <c>HKCU\…\Themes\Personalize\AppsUseLightTheme</c>, and the contrast and animation settings from
/// <see cref="SystemParameters"/>. It reports a change on <c>WM_SETTINGCHANGE</c> and <c>WM_SYSCOLORCHANGE</c>
/// (<see cref="SystemEvents.UserPreferenceChanged"/>, which also covers the «ImmersiveColorSet» notice of a light or
/// dark switch) and when <see cref="SystemParameters.HighContrast"/> or
/// <see cref="SystemParameters.ClientAreaAnimation"/> change.
/// </summary>
/// <remarks>Dispose it with the theme service: it subscribes to static events.</remarks>
public sealed class WindowsSystemThemeSource : ISystemThemeSource, IDisposable
{
    private const string PersonalizeKey =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private const string AppsUseLightThemeValue = "AppsUseLightTheme";

    private bool _disposed;

    /// <summary>Starts listening to the system notifications.</summary>
    public WindowsSystemThemeSource()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <inheritdoc />
    public SystemThemeState Read() =>
        new(
            ReadAppsUseLightTheme(),
            SystemParameters.HighContrast,
            SystemParameters.ClientAreaAnimation
        );

    /// <summary>Stops listening.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
    }

    /// <summary>
    /// The app mode of Windows. Without the value (Windows before the dark mode of apps, or a policy that removed
    /// it) Windows shows apps in light mode.
    /// </summary>
    private static bool ReadAppsUseLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(AppsUseLightThemeValue) is not int value || value != 0;
        }
        catch (Exception ex)
            when (ex
                    is System.Security.SecurityException
                        or UnauthorizedAccessException
                        or System.IO.IOException
            )
        {
            return true;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (
            e.Category
            is UserPreferenceCategory.General
                or UserPreferenceCategory.Color
                or UserPreferenceCategory.VisualStyle
                or UserPreferenceCategory.Accessibility
        )
        {
            RaiseChanged();
        }
    }

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            e.PropertyName
            is nameof(SystemParameters.HighContrast)
                or nameof(SystemParameters.ClientAreaAnimation)
        )
        {
            RaiseChanged();
        }
    }

    private void RaiseChanged()
    {
        if (!_disposed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
