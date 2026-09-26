using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.App.Composition;

/// <summary>What the pieces of M2 take from the user settings (blueprint §6.3).</summary>
internal static class SettingsProjection
{
    /// <summary>The touch filter of the surfaces (TAC-001, TAC-002).</summary>
    public static TouchSettings Touch(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var touch = settings.Touch;
        return new TouchSettings(
            touch.Debounce,
            touch.HitSlopPx,
            touch.CancelMovePx,
            touch.MinContact
        );
    }

    /// <summary>What the engine obeys (SEG-004, SEG-005, NFR-004).</summary>
    public static EngineConfig Engine(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new EngineConfig(
            settings.KeySafety.MaxHold,
            settings.KeySafety.ReleaseOnAppSwitch,
            Timings.Injection.InterEventDelay,
            Touch(settings)
        );
    }

    /// <summary>The tile and gap sizes of the panel size in use (GEN-004).</summary>
    public static SizeMetrics Size(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Size switch
        {
            Clicalo.Domain.Settings.PanelSize.Small => PanelSizes.S,
            Clicalo.Domain.Settings.PanelSize.Large => PanelSizes.L,
            _ => PanelSizes.M,
        };
    }

    /// <summary>
    /// The palette of the theme chosen (TEM-001). «Auto» follows Windows through the theme service of M3; until then it
    /// is the dark palette, and a Windows contrast theme always wins inside <c>ThemeScope</c>.
    /// </summary>
    public static ThemeId Theme(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Theme switch
        {
            ThemeChoice.Light => ThemeId.Light,
            ThemeChoice.HighContrast => ThemeId.HighContrast,
            _ => ThemeId.Dark,
        };
    }
}
