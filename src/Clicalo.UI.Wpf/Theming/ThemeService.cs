using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;
using Clicalo.Domain.Settings;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.UI.Wpf.Theming;

/// <summary>
/// The theme of one UI thread (blueprint §8.4, TEM-001, TEM-005, TEM-006, CUA-011): it resolves the person's choice
/// (Auto, Dark, Light or High contrast) against Windows, and publishes the result in <see cref="Resources"/>, one
/// dictionary that every window of the thread merges with <see cref="Attach"/>.
/// </summary>
/// <remarks>
/// <para>
/// TEM-001: a Windows contrast theme always wins (the system colors, <see cref="ThemeId.SystemHighContrast"/>);
/// otherwise «Auto» follows the light or dark mode of Windows apps, live. AJR-004: a change applies at once to every
/// attached window, in place: entries of <see cref="Resources"/> are replaced and the elements that reference them
/// dynamically repaint without rebuilding the tree, so the UI Automation tree stays the same (REG-06).
/// </para>
/// <para>
/// Every value is immutable or a frozen <see cref="System.Windows.Freezable"/>. The service and its dictionary belong
/// to the thread that created it: create one per UI thread. <see cref="ISystemThemeSource.Changed"/> may arrive on
/// any thread; the service reads the system again on its own dispatcher, after the messages already queued (so the
/// system colors of <c>WM_SYSCOLORCHANGE</c> are current). Dispose it with the thread: it listens to the source.
/// </para>
/// </remarks>
public sealed class ThemeService : IDisposable
{
    private readonly ISystemThemeSource _system;
    private readonly Dispatcher _dispatcher;
    private readonly HashSet<FrameworkElement> _roots = [];
    private ThemeChoice _preference;
    private int _textScalePercent;
    private bool _reduceMotionPreference;
    private bool _disposed;

    /// <summary>Resolves and publishes the theme on the current UI thread.</summary>
    /// <param name="system">The system state and its change notifications.</param>
    /// <param name="preference">The theme the person chose (TEM-001; Auto by default).</param>
    /// <param name="textScalePercent">The person's text scale, 100 to 150 (CUA-011).</param>
    /// <param name="reduceMotion">The app's own reduce motion setting (TEM-006).</param>
    public ThemeService(
        ISystemThemeSource system,
        ThemeChoice preference = ThemeChoice.Auto,
        int textScalePercent = 100,
        bool reduceMotion = false
    )
    {
        ArgumentNullException.ThrowIfNull(system);
        TypeScale.EnsureScale(textScalePercent);
        _system = system;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _preference = preference;
        _textScalePercent = textScalePercent;
        _reduceMotionPreference = reduceMotion;
        Resources = new ResourceDictionary();
        Apply();
        _system.Changed += OnSystemChanged;
    }

    /// <summary>
    /// The theme resources: brushes of <see cref="ThemeBrushKey"/> and <see cref="CategoryBrushKey"/>,
    /// <see cref="ThemeScope.BorderThicknessKey"/> and the keys of <see cref="ThemeKeys"/>.
    /// </summary>
    public ResourceDictionary Resources { get; }

    /// <summary>The theme the person chose; changing it repaints every attached window in place.</summary>
    public ThemeChoice Preference
    {
        get => _preference;
        set
        {
            EnsureUsable();
            if (_preference != value)
            {
                _preference = value;
                Apply();
            }
        }
    }

    /// <summary>The person's text scale, 100 to 150 % (CUA-011).</summary>
    public int TextScalePercent
    {
        get => _textScalePercent;
        set
        {
            EnsureUsable();
            TypeScale.EnsureScale(value);
            if (_textScalePercent != value)
            {
                _textScalePercent = value;
                Apply();
            }
        }
    }

    /// <summary>The app's own reduce motion setting (TEM-006).</summary>
    public bool ReduceMotionPreference
    {
        get => _reduceMotionPreference;
        set
        {
            EnsureUsable();
            if (_reduceMotionPreference != value)
            {
                _reduceMotionPreference = value;
                Apply();
            }
        }
    }

    /// <summary>The system state the service last read.</summary>
    public SystemThemeState System { get; private set; }

    /// <summary>The palette in use, after resolving <see cref="Preference"/> against <see cref="System"/>.</summary>
    public ThemeId Effective { get; private set; }

    /// <summary>The colors in use (a snapshot of the system colors for <see cref="ThemeId.SystemHighContrast"/>).</summary>
    public ThemePalette Palette { get; private set; }

    /// <summary>True when transitions take 0 ms: the app's setting, or Windows animations off (TEM-006).</summary>
    public bool ReduceMotion => _reduceMotionPreference || !System.ClientAreaAnimation;

    /// <summary>Raised on the service's thread after the resources have been (re)applied.</summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The palette for a choice and a system state (TEM-001): the system contrast colors while Windows has a contrast
    /// theme on, whatever the choice; Auto follows the light or dark mode of Windows apps; the other choices are
    /// themselves.
    /// </summary>
    public static ThemeId Resolve(ThemeChoice preference, SystemThemeState system) =>
        system.HighContrast
            ? ThemeId.SystemHighContrast
            : preference switch
            {
                ThemeChoice.Dark => ThemeId.Dark,
                ThemeChoice.Light => ThemeId.Light,
                ThemeChoice.HighContrast => ThemeId.HighContrast,
                ThemeChoice.Auto => system.AppsUseLightTheme ? ThemeId.Light : ThemeId.Dark,
                _ => throw new ArgumentOutOfRangeException(
                    nameof(preference),
                    preference,
                    message: null
                ),
            };

    /// <summary>
    /// Merges <see cref="Resources"/> into <paramref name="root"/> and makes its text inherit the interface font and
    /// the text color of the theme. Call it once per window, on the service's thread.
    /// </summary>
    /// <param name="root">Usually a window; its resources must not define the theme keys themselves.</param>
    public void Attach(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        EnsureUsable();
        root.VerifyAccess();
        if (!_roots.Add(root))
        {
            return;
        }

        root.Resources.MergedDictionaries.Add(Resources);
        root.SetResourceReference(TextElement.FontFamilyProperty, ThemeKeys.UiFont);
        root.SetResourceReference(
            TextElement.ForegroundProperty,
            ThemeBrushKey.For(ColorToken.Text)
        );
    }

    /// <summary>Removes what <see cref="Attach"/> added to <paramref name="root"/>.</summary>
    public void Detach(FrameworkElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _dispatcher.VerifyAccess();
        if (!_roots.Remove(root))
        {
            return;
        }

        root.Resources.MergedDictionaries.Remove(Resources);
        root.ClearValue(TextElement.FontFamilyProperty);
        root.ClearValue(TextElement.ForegroundProperty);
    }

    /// <summary>Reads the system state again and re-applies (also after <c>WM_SYSCOLORCHANGE</c>).</summary>
    public void Refresh()
    {
        EnsureUsable();
        Apply();
    }

    /// <summary>Stops following the system; attached windows keep the last resources.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _system.Changed -= OnSystemChanged;
    }

    private void OnSystemChanged(object? sender, EventArgs e) =>
        _ = _dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(RefreshIfLive));

    private void RefreshIfLive()
    {
        if (!_disposed)
        {
            Apply();
        }
    }

    private void EnsureUsable()
    {
        _dispatcher.VerifyAccess();
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    [MemberNotNull(nameof(Palette))]
    private void Apply()
    {
        System = _system.Read();
        Effective = Resolve(_preference, System);
        Palette = ThemeCatalog.GetPalette(Effective);
        ThemeResources.WritePalette(Resources, Palette);
        ThemeResources.WriteTypography(Resources, _textScalePercent);
        ThemeResources.WriteMotion(Resources, ReduceMotion);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
