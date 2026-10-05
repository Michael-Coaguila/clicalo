using Clicalo.Domain.Catalog;
using Clicalo.Domain.CommonActions;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Timing;
using Clicalo.Domain.Touch;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.App.Composition;

/// <summary>What the pieces of the app take from the user settings (blueprint §6.3).</summary>
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

    /// <summary>
    /// What the engine obeys (SEG-004, SEG-005, NFR-004), with the programs language that picks variants (CAT-005)
    /// and the common actions it adapts to the app in front (decision D4, EJE-018).
    /// </summary>
    public static EngineConfig Engine(UserSettings settings, CommonActionTable commonActions)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(commonActions);
        return new EngineConfig(
            settings.KeySafety.MaxHold,
            settings.KeySafety.ReleaseOnAppSwitch,
            Timings.Injection.InterEventDelay,
            Touch(settings)
        )
        {
            AppsLanguage = settings.Keyboard.AppsLanguage,
            CommonActions = commonActions,
        };
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
    /// Gives the theme service the theme chosen, the text scale and reduce motion (TEM-001, CUA-011, TEM-006). «Auto»
    /// follows Windows inside the service, and a Windows contrast theme always wins there.
    /// </summary>
    public static void Theme(ThemeService theme, UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(theme);
        ArgumentNullException.ThrowIfNull(settings);
        theme.Preference = settings.Theme;
        theme.TextScalePercent = settings.TextScalePercent;
        theme.ReduceMotionPreference = settings.ReduceMotion;
    }

    /// <summary>The opacity and the automatic dimming of the surfaces (GEN-009).</summary>
    public static DimSettings Dim(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new DimSettings(settings.AutoDim, settings.Opacity, settings.DimTo);
    }
}
