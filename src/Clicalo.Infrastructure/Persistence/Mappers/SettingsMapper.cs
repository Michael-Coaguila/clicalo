using System.Text.Json;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence.Dto;

namespace Clicalo.Infrastructure.Persistence.Mappers;

/// <summary>
/// <see cref="UserSettings"/> ↔ <see cref="SettingsDto"/> (DAT-001). Reading takes every missing or unknown value from
/// the defaults, so an older document or a newer value still loads; ranges are clamped afterwards by
/// <c>SettingsSchema.Clamp</c>. Durations are milliseconds.
/// </summary>
internal static class SettingsMapper
{
    /// <summary>The settings of <paramref name="dto"/>, completed from <paramref name="defaults"/>.</summary>
    /// <param name="dto">The persisted settings, or <see langword="null"/>.</param>
    /// <param name="defaults"><c>SettingsSchema.Defaults</c>.</param>
    public static UserSettings Decode(SettingsDto? dto, UserSettings defaults)
    {
        ArgumentNullException.ThrowIfNull(defaults);
        if (dto is null)
        {
            return defaults;
        }

        return defaults with
        {
            Language = dto.Lang is { Length: > 0 } lang ? new LangCode(lang) : defaults.Language,
            Theme = PersistedNames.Theme.ParseOr(dto.Theme, defaults.Theme),
            Density = PersistedNames.Density.ParseOr(dto.Density, defaults.Density),
            Size = PersistedNames.Size.ParseOr(dto.Size, defaults.Size),
            Columns = dto.Cols ?? defaults.Columns,
            RowsPreference = dto.RowsPref ?? defaults.RowsPreference,
            TextScalePercent = dto.TextScale ?? defaults.TextScalePercent,
            Opacity = dto.Opacity ?? defaults.Opacity,
            AutoDim = dto.AutoDim ?? defaults.AutoDim,
            DimTo = dto.DimTo ?? defaults.DimTo,
            ShowKeys = dto.ShowKeys ?? defaults.ShowKeys,
            VoiceNumbers = dto.VoiceNumbers ?? defaults.VoiceNumbers,
            StickyModifiersRow = dto.StickyModsRow ?? defaults.StickyModifiersRow,
            ShowAlwaysVisibleRow = dto.ShowStripRow ?? defaults.ShowAlwaysVisibleRow,
            ShowProfileSelectorRow = dto.ShowTabsRow ?? defaults.ShowProfileSelectorRow,
            ReduceMotion = dto.ReduceMotion ?? defaults.ReduceMotion,
            Feedback = dto.Feedback is { } feedback
                ? new FeedbackSettings(
                    feedback.Sound ?? defaults.Feedback.Sound,
                    feedback.Flash ?? defaults.Feedback.Flash
                )
                : defaults.Feedback,
            LockProfile = dto.LockProfile ?? defaults.LockProfile,
            LastProfile = dto.LastProfile is { Length: > 0 } last ? new ProfileId(last) : null,
            Dock = dto.Dock is { } dock ? DecodeDock(dock, defaults.Dock) : defaults.Dock,
            PanelPositions = dto.PanelPositions is { } positions
                ? ValueListBuilder.From(
                    positions
                        .Where(p => p.Monitor is { Length: > 0 })
                        .Select(p => new MonitorPosition(p.Monitor!, p.X ?? 0, p.Y ?? 0))
                )
                : defaults.PanelPositions,
            Touch = dto.Touch is { } touch
                ? new TouchFilterSettings(
                    touch.Preset ?? defaults.Touch.Preset,
                    Duration(touch.DebounceMs, defaults.Touch.Debounce),
                    touch.HitSlopPx ?? defaults.Touch.HitSlopPx,
                    touch.CancelMovePx ?? defaults.Touch.CancelMovePx,
                    Duration(touch.MinContactMs, defaults.Touch.MinContact)
                )
                : defaults.Touch,
            KeySafety = dto.KeySafety is { } safety
                ? new KeySafetySettings(
                    safety.MaxHoldMs switch
                    {
                        null => defaults.KeySafety.MaxHold,
                        <= 0 => null,
                        { } ms => Milliseconds(ms),
                    },
                    safety.ReleaseOnAppSwitch ?? defaults.KeySafety.ReleaseOnAppSwitch
                )
                : defaults.KeySafety,
            AutoSuggestProfiles = dto.AutoSuggestProfiles ?? defaults.AutoSuggestProfiles,
            Keyboard = dto.Keyboard is { } keyboard
                ? new KeyboardSettings(
                    keyboard.Layout ?? defaults.Keyboard.Layout,
                    keyboard.AppsLang is { Length: > 0 } apps
                        ? new LangCode(apps)
                        : defaults.Keyboard.AppsLanguage,
                    keyboard.Detected ?? defaults.Keyboard.Detected
                )
                : defaults.Keyboard,
            Ai = dto.Ai is { } ai
                ? new AiSettings
                {
                    Consent = ai.Consent ?? defaults.Ai.Consent,
                    Disabled = ai.Disabled ?? defaults.Ai.Disabled,
                    FreeLeftToday = ai.FreeLeftToday ?? defaults.Ai.FreeLeftToday,
                    FreeResetAt = ai.FreeResetAt,
                    ApiKeyRef = ai.ApiKeyRef,
                }
                : defaults.Ai,
            Reliability = dto.Reliability is { } reliability
                ? new ReliabilitySettings(
                    reliability.StartWithWindows ?? defaults.Reliability.StartWithWindows,
                    reliability.AutoBackup ?? defaults.Reliability.AutoBackup,
                    reliability.CrashRecovery ?? defaults.Reliability.CrashRecovery,
                    reliability.SingleInstance ?? defaults.Reliability.SingleInstance,
                    reliability.RunAsAdmin ?? defaults.Reliability.RunAsAdmin
                )
                : defaults.Reliability,
            Updates = dto.Updates is { } updates
                ? new UpdateSettings(
                    updates.Auto ?? defaults.Updates.Automatic,
                    updates.AskBefore ?? defaults.Updates.AskBefore,
                    updates.BackupBefore ?? defaults.Updates.BackupBefore,
                    PersistedNames.Channel.ParseOr(updates.Channel, defaults.Updates.Channel)
                )
                : defaults.Updates,
            NoKeyboardUser = dto.NoKeyboardUser ?? defaults.NoKeyboardUser,
        };
    }

    /// <summary>The persisted form of <paramref name="settings"/>, with the unknown members of a later minor.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="extra">Members to keep.</param>
    public static SettingsDto Encode(
        UserSettings settings,
        IReadOnlyDictionary<string, JsonElement>? extra
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        return new SettingsDto
        {
            Lang = settings.Language.Value,
            Theme = PersistedNames.Theme.Name(settings.Theme),
            Density = PersistedNames.Density.Name(settings.Density),
            Size = PersistedNames.Size.Name(settings.Size),
            Cols = settings.Columns,
            RowsPref = settings.RowsPreference,
            TextScale = settings.TextScalePercent,
            Opacity = settings.Opacity,
            AutoDim = settings.AutoDim,
            DimTo = settings.DimTo,
            ShowKeys = settings.ShowKeys,
            VoiceNumbers = settings.VoiceNumbers,
            StickyModsRow = settings.StickyModifiersRow,
            ShowStripRow = settings.ShowAlwaysVisibleRow,
            ShowTabsRow = settings.ShowProfileSelectorRow,
            ReduceMotion = settings.ReduceMotion,
            Feedback = new FeedbackDto
            {
                Sound = settings.Feedback.Sound,
                Flash = settings.Feedback.Flash,
            },
            LockProfile = settings.LockProfile,
            LastProfile = settings.LastProfile?.Value,
            Dock = new DockDto
            {
                Side = PersistedNames.DockSide.Name(settings.Dock.Side),
                HandlePosBySide = new HandlePositionsDto
                {
                    Right = settings.Dock.HandlePositions.Right,
                    Left = settings.Dock.HandlePositions.Left,
                    Top = settings.Dock.HandlePositions.Top,
                    Bottom = settings.Dock.HandlePositions.Bottom,
                },
                HandleLocked = settings.Dock.HandleLocked,
                PinOpen = settings.Dock.PinOpen,
                Gutter = settings.Dock.Gutter,
                PerPage = settings.Dock.PerPage,
                CoachDone = settings.Dock.CoachDone,
            },
            PanelPositions =
            [
                .. settings.PanelPositions.Select(p => new MonitorPositionDto
                {
                    Monitor = p.MonitorId,
                    X = p.X,
                    Y = p.Y,
                }),
            ],
            Touch = new TouchDto
            {
                Preset = settings.Touch.Preset,
                DebounceMs = settings.Touch.Debounce.TotalMilliseconds,
                HitSlopPx = settings.Touch.HitSlopPx,
                CancelMovePx = settings.Touch.CancelMovePx,
                MinContactMs = settings.Touch.MinContact.TotalMilliseconds,
            },
            KeySafety = new KeySafetyDto
            {
                MaxHoldMs = settings.KeySafety.MaxHold?.TotalMilliseconds ?? 0,
                ReleaseOnAppSwitch = settings.KeySafety.ReleaseOnAppSwitch,
            },
            AutoSuggestProfiles = settings.AutoSuggestProfiles,
            Keyboard = new KeyboardDto
            {
                Layout = settings.Keyboard.Layout,
                AppsLang = settings.Keyboard.AppsLanguage.Value,
                Detected = settings.Keyboard.Detected,
            },
            Ai = new AiDto
            {
                Consent = settings.Ai.Consent,
                Disabled = settings.Ai.Disabled,
                FreeLeftToday = settings.Ai.FreeLeftToday,
                FreeResetAt = settings.Ai.FreeResetAt,
                ApiKeyRef = settings.Ai.ApiKeyRef,
            },
            Reliability = new ReliabilityDto
            {
                StartWithWindows = settings.Reliability.StartWithWindows,
                AutoBackup = settings.Reliability.AutoBackup,
                CrashRecovery = settings.Reliability.CrashRecovery,
                SingleInstance = settings.Reliability.SingleInstance,
                RunAsAdmin = settings.Reliability.RunAsAdmin,
            },
            Updates = new UpdatesDto
            {
                Auto = settings.Updates.Automatic,
                AskBefore = settings.Updates.AskBefore,
                BackupBefore = settings.Updates.BackupBefore,
                Channel = PersistedNames.Channel.Name(settings.Updates.Channel),
            },
            NoKeyboardUser = settings.NoKeyboardUser,
            Extra = extra is null
                ? null
                : new Dictionary<string, JsonElement>(extra, StringComparer.Ordinal),
        };
    }

    /// <summary>A duration persisted in milliseconds, exact to the tick.</summary>
    /// <param name="milliseconds">Milliseconds.</param>
    public static TimeSpan Milliseconds(double milliseconds) =>
        TimeSpan.FromTicks((long)Math.Round(milliseconds * TimeSpan.TicksPerMillisecond));

    private static TimeSpan Duration(double? milliseconds, TimeSpan fallback) =>
        milliseconds is { } ms && double.IsFinite(ms) && ms >= 0 ? Milliseconds(ms) : fallback;

    private static DockSettings DecodeDock(DockDto dock, DockSettings defaults) =>
        defaults with
        {
            Side = PersistedNames.DockSide.ParseOr(dock.Side, defaults.Side),
            HandlePositions = dock.HandlePosBySide is { } handle
                ? new DockHandlePositions(
                    handle.Right ?? defaults.HandlePositions.Right,
                    handle.Left ?? defaults.HandlePositions.Left,
                    handle.Top ?? defaults.HandlePositions.Top,
                    handle.Bottom ?? defaults.HandlePositions.Bottom
                )
                : defaults.HandlePositions,
            HandleLocked = dock.HandleLocked ?? defaults.HandleLocked,
            PinOpen = dock.PinOpen ?? defaults.PinOpen,
            Gutter = dock.Gutter ?? defaults.Gutter,
            PerPage = dock.PerPage ?? defaults.PerPage,
            CoachDone = dock.CoachDone ?? defaults.CoachDone,
        };
}
