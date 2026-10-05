using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Controls;

/// <summary>
/// The palette of one surface or window, published as resources of its root element: one frozen brush per
/// <see cref="ThemeBrushKey"/> and the border thickness under <see cref="BorderThicknessKey"/> (blueprint §8.4).
/// </summary>
/// <remarks>
/// <para>
/// TEM-001: while a Windows contrast theme is on, the effective theme is always
/// <see cref="ThemeId.SystemHighContrast"/>, whatever the preference. The switch happens in place when
/// <see cref="SystemParameters.HighContrast"/> changes: this scope only replaces resources, and once the switch is over
/// the UI Automation tree (names, control types, patterns and states) is the same as before (S3, REG-06). While it
/// happens it is not: WPF applies the window template again after each <c>WM_THEMECHANGED</c> (the default style of the
/// window comes from the system theme dictionary), which rebuilds for a moment the subtree that UI Automation walks
/// (S3, finding 3).
/// </para>
/// <para>
/// Created and used on the UI thread of <see cref="Root"/>. The system notification may arrive on another UI thread;
/// the scope re-applies itself on its own dispatcher, after the messages already queued (so the system colors of
/// <c>WM_SYSCOLORCHANGE</c> are current). Dispose it with the surface: it subscribes to a static event.
/// </para>
/// <para>
/// The theme service of milestone M3 resolves «Auto» and listens to <c>WM_SYSCOLORCHANGE</c>; it drives this scope
/// through <see cref="Preferred"/> and <see cref="Refresh"/>.
/// </para>
/// </remarks>
public sealed class ThemeScope : IDisposable
{
    private ThemeId _preferred;
    private bool _disposed;

    /// <summary>Applies <paramref name="preferred"/> (or the system contrast colors) to <paramref name="root"/>.</summary>
    /// <param name="root">The element whose resources receive the brushes, usually the surface window.</param>
    /// <param name="preferred">The theme chosen by the person (Dark, Light or HighContrast).</param>
    public ThemeScope(FrameworkElement root, ThemeId preferred)
    {
        ArgumentNullException.ThrowIfNull(root);
        root.VerifyAccess();
        Root = root;
        _preferred = preferred;
        Apply();
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
    }

    /// <summary>Resource key of the <see cref="Thickness"/> of borders and outlines in the effective theme.</summary>
    public static ResourceKey BorderThicknessKey { get; } =
        new ComponentResourceKey(typeof(ThemeScope), nameof(BorderThicknessKey));

    /// <summary>The element that owns the resources.</summary>
    public FrameworkElement Root { get; }

    /// <summary>The theme chosen by the person; changing it repaints in place.</summary>
    public ThemeId Preferred
    {
        get => _preferred;
        set
        {
            Root.VerifyAccess();
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_preferred == value)
            {
                return;
            }

            _preferred = value;
            Apply();
        }
    }

    /// <summary>The theme in use: <see cref="ThemeId.SystemHighContrast"/> while Windows has a contrast theme on.</summary>
    public ThemeId Effective { get; private set; }

    /// <summary>The palette in use (a snapshot of the system colors for <see cref="ThemeId.SystemHighContrast"/>).</summary>
    public ThemePalette Palette { get; private set; }

    /// <summary>Raised on the UI thread after the resources have been (re)applied.</summary>
    public event EventHandler? Applied;

    /// <summary>
    /// The theme to render with (TEM-001): the system contrast colors while Windows has a contrast theme on;
    /// otherwise the preference, where asking for the system colors without a system theme means Clícalo's own
    /// high-contrast palette.
    /// </summary>
    public static ThemeId Resolve(ThemeId preferred, bool systemHighContrast) =>
        systemHighContrast ? ThemeId.SystemHighContrast
        : preferred == ThemeId.SystemHighContrast ? ThemeId.HighContrast
        : preferred;

    /// <summary>
    /// Reads the system contrast state and colors again and re-applies; for <c>WM_SYSCOLORCHANGE</c>, which changes
    /// the colors of an active contrast theme without changing <see cref="SystemParameters.HighContrast"/>.
    /// </summary>
    public void Refresh()
    {
        Root.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
        Apply();
    }

    /// <summary>Stops following the system contrast state; the resources stay as they are.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        SystemParameters.StaticPropertyChanged -= OnSystemParameterChanged;
    }

    private void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (
            !string.Equals(
                e.PropertyName,
                nameof(SystemParameters.HighContrast),
                StringComparison.Ordinal
            )
        )
        {
            return;
        }

        _ = Root.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(ApplyIfLive));
    }

    private void ApplyIfLive()
    {
        if (!_disposed)
        {
            Apply();
        }
    }

    [MemberNotNull(nameof(Palette))]
    private void Apply()
    {
        var effective = Resolve(_preferred, SystemParameters.HighContrast);
        var palette = ThemeCatalog.GetPalette(effective);
        var resources = Root.Resources;
        foreach (var key in ThemeBrushKey.All)
        {
            resources[key] = palette.CreateBrush(key.Token);
        }

        resources[BorderThicknessKey] = new Thickness(palette.BorderThickness);
        Effective = effective;
        Palette = palette;
        Applied?.Invoke(this, EventArgs.Empty);
    }
}
