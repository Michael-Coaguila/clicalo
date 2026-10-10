namespace Clicalo.Domain.Settings;

/// <summary>
/// The dotted path of every leaf of <see cref="UserSettings"/>, as persisted (docs/02 <c>settings</c>) and as used by
/// <see cref="SettingDescriptor.Path"/> and the <c>SetSetting</c> command.
/// </summary>
public static class SettingPaths
{
    public const string Language = "lang";
    public const string Theme = "theme";
    public const string Density = "density";
    public const string Size = "size";
    public const string Columns = "cols";
    public const string RowsPreference = "rowsPref";
    public const string TextScale = "textScale";
    public const string Opacity = "opacity";
    public const string AutoDim = "autoDim";
    public const string DimTo = "dimTo";
    public const string ShowKeys = "showKeys";
    public const string VoiceNumbers = "voiceNumbers";
    public const string StickyModifiersRow = "stickyModsRow";
    public const string ShowAlwaysVisibleRow = "showStripRow";
    public const string ShowProfileSelectorRow = "showTabsRow";
    public const string ReduceMotion = "reduceMotion";
    public const string FeedbackSound = "feedback.sound";
    public const string FeedbackFlash = "feedback.flash";
    public const string LockProfile = "lockProfile";
    public const string LastProfile = "lastProfile";
    public const string DockSide = "dock.side";
    public const string DockHandleRight = "dock.handlePosBySide.right";
    public const string DockHandleLeft = "dock.handlePosBySide.left";
    public const string DockHandleTop = "dock.handlePosBySide.top";
    public const string DockHandleBottom = "dock.handlePosBySide.bottom";
    public const string DockHandleLocked = "dock.handleLocked";
    public const string DockPinOpen = "dock.pinOpen";
    public const string DockGutter = "dock.gutter";
    public const string DockPerPage = "dock.perPage";
    public const string DockCoachDone = "dock.coachDone";
    public const string PanelPositions = "panelPosByMonitor";
    public const string TouchPreset = "touch.preset";
    public const string TouchDebounce = "touch.debounceMs";
    public const string TouchHitSlop = "touch.hitSlopPx";
    public const string TouchCancelMove = "touch.cancelMovePx";
    public const string TouchMinContact = "touch.minContactMs";
    public const string MaxHold = "keySafety.maxHoldSec";
    public const string ReleaseOnAppSwitch = "keySafety.releaseOnAppSwitch";
    public const string AutoSuggestProfiles = "autoSuggestProfiles";
    public const string KeyboardLayout = "keyboard.layout";
    public const string AppsLanguage = "keyboard.appsLang";
    public const string KeyboardDetected = "keyboard.detected";
    public const string AiConsent = "ai.consent";
    public const string AiDisabled = "ai.disabled";
    public const string AiFreeLeftToday = "ai.freeLeftToday";
    public const string AiFreeResetAt = "ai.freeResetAt";
    public const string AiApiKeyRef = "ai.apiKeyRef";
    public const string StartWithWindows = "reliability.startWithWindows";
    public const string AutoBackup = "reliability.autoBackup";
    public const string CrashRecovery = "reliability.crashRecovery";
    public const string SingleInstance = "reliability.singleInstance";
    public const string RunAsAdmin = "reliability.runAsAdmin";
    public const string UpdatesAutomatic = "updates.auto";
    public const string UpdatesAskBefore = "updates.askBefore";
    public const string UpdatesBackupBefore = "updates.backupBefore";
    public const string UpdatesChannel = "updates.channel";
    public const string NoKeyboardUser = "noKeyboardUser";
    public const string HandlePositionsByMonitor = "handlePosByMonitor";
    public const string ControlCenter = "controlCenter";
    public const string GlobalHotkeyEnabled = "globalHotkey.enabled";
    public const string GlobalHotkeyCombo = "globalHotkey.combo";
    public const string TimeMultiplier = "timeMultiplier";
}
