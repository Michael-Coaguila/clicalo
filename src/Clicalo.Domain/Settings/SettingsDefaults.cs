using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>
/// The settings of a new installation (docs/02, GEN-*, the prototype's initial state and the catalog decisions): the
/// value behind <see cref="SettingsSchema.Defaults"/> and every <see cref="SettingDescriptor.Default"/>.
/// </summary>
internal static class SettingsDefaults
{
    public static UserSettings Value { get; } =
        new()
        {
            // The first run replaces it with the Windows language (BIE-004); Spanish is the source language.
            Language = LangCode.Es,
            Theme = ThemeChoice.Auto,
            Density = PanelDensity.Full,
            Size = PanelSize.Medium,
            Columns = 3,
            RowsPreference = 0,
            TextScalePercent = 100,
            // The prototype's value, kept exact although it is not on the 0.05 grid (like the touch presets, TAC-005).
            Opacity = 0.92,
            AutoDim = true,
            DimTo = 0.35,
            ShowKeys = true,
            VoiceNumbers = false,
            StickyModifiersRow = false,
            ShowAlwaysVisibleRow = true,
            ShowProfileSelectorRow = true,
            ReduceMotion = false,
            Feedback = new FeedbackSettings(Sound: true, Flash: true),
            LockProfile = false,
            LastProfile = null,
            Dock = new DockSettings
            {
                Side = DockSide.Right,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = false,
                PerPage = 5,
                CoachDone = false,
            },
            PanelPositions = [],
            Touch = new TouchFilterSettings(
                CatalogMirror.DefaultTouchPreset,
                CatalogMirror.DefaultTouchDebounce,
                CatalogMirror.DefaultTouchHitSlopPx,
                CatalogMirror.DefaultTouchCancelMovePx,
                CatalogMirror.DefaultTouchMinContact
            ),
            KeySafety = new KeySafetySettings(
                CatalogMirror.AutoReleaseDefault,
                ReleaseOnAppSwitch: true
            ),
            AutoSuggestProfiles = true,
            // Detected on the first run (PLA-009); empty until then.
            Keyboard = new KeyboardSettings(string.Empty, LangCode.Es, Detected: false),
            Ai = new AiSettings
            {
                Consent = false,
                Disabled = false,
                FreeLeftToday = CatalogMirror.AiFreeDailyQuota,
                FreeResetAt = null,
                ApiKeyRef = null,
            },
            Reliability = new ReliabilitySettings(
                StartWithWindows: true,
                AutoBackup: true,
                CrashRecovery: true,
                SingleInstance: true,
                RunAsAdmin: false
            ),
            Updates = new UpdateSettings(
                Automatic: true,
                AskBefore: true,
                BackupBefore: true,
                UpdateChannel.Stable
            ),
            // Nothing is preselected for a new user (BIE-005, PQ-39).
            NoKeyboardUser = false,
        };
}
