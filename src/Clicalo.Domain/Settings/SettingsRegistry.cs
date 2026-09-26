using System.Collections.Frozen;
using System.Collections.Immutable;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>
/// One accessor per leaf of <see cref="UserSettings"/>: path, scope, undo, default, range, texts and how to read,
/// write and repair it. <see cref="SettingsSchema"/> is its public face.
/// </summary>
/// <remarks>
/// Scopes (DAT-006): what only changes how things look is <see cref="SettingScope.Presentation"/>; the panel's position,
/// its handle and which profile it shows or returns to (Auto/Fixed and <c>lastProfile</c>, PER-001) are
/// <see cref="SettingScope.Placement"/>; everything else is <see cref="SettingScope.Behavior"/> and undoable, except
/// the AI quota counters and the key reference, which the app updates and which undo must not rewind.
/// </remarks>
internal static class SettingsRegistry
{
    private const SettingScope Presentation = SettingScope.Presentation;
    private const SettingScope Behavior = SettingScope.Behavior;
    private const SettingScope Placement = SettingScope.Placement;

    public static ImmutableArray<SettingAccessor> Leaves { get; } = Build();

    public static ImmutableArray<SettingDescriptor> Descriptors { get; } =
    [.. Leaves.Select(leaf => leaf.Descriptor)];

    public static FrozenDictionary<string, SettingAccessor> ByPath { get; } =
        Leaves.ToFrozenDictionary(leaf => leaf.Descriptor.Path, StringComparer.Ordinal);

    private static ImmutableArray<SettingAccessor> Build() =>
        [
            Leaf(
                SettingPaths.Language,
                Presentation,
                L.Lang,
                null,
                s => s.Language,
                (s, v) => s with { Language = v },
                Language
            ),
            Leaf(
                SettingPaths.Theme,
                Presentation,
                L.Theme,
                null,
                s => s.Theme,
                (s, v) => s with { Theme = v },
                Defined
            ),
            Leaf(
                SettingPaths.Density,
                Presentation,
                L.View,
                null,
                s => s.Density,
                (s, v) => s with { Density = v },
                Defined
            ),
            Leaf(
                SettingPaths.Size,
                Presentation,
                L.Size,
                null,
                s => s.Size,
                (s, v) => s with { Size = v },
                Defined
            ),
            Leaf(
                SettingPaths.Columns,
                Presentation,
                L.Columns,
                null,
                s => s.Columns,
                (s, v) => s with { Columns = v },
                In(SettingsSchema.Columns),
                SettingsSchema.Columns
            ),
            Leaf(
                SettingPaths.RowsPreference,
                Presentation,
                L.RowsVis,
                L.RowsVisD,
                s => s.RowsPreference,
                (s, v) => s with { RowsPreference = v },
                In(SettingsSchema.RowsPreference),
                SettingsSchema.RowsPreference
            ),
            Leaf(
                SettingPaths.TextScale,
                Presentation,
                L.TextSize,
                null,
                s => s.TextScalePercent,
                (s, v) => s with { TextScalePercent = v },
                In(SettingsSchema.TextScalePercent),
                SettingsSchema.TextScalePercent
            ),
            Leaf(
                SettingPaths.Opacity,
                Presentation,
                L.Opacity,
                null,
                s => s.Opacity,
                (s, v) => s with { Opacity = v },
                InRange(SettingsSchema.Opacity),
                SettingsSchema.Opacity
            ),
            Leaf(
                SettingPaths.AutoDim,
                Presentation,
                L.AutoDim,
                L.AutoDimD,
                s => s.AutoDim,
                (s, v) => s with { AutoDim = v }
            ),
            Leaf(
                SettingPaths.DimTo,
                Presentation,
                L.DimLevel,
                null,
                s => s.DimTo,
                (s, v) => s with { DimTo = v },
                InRange(SettingsSchema.DimTo),
                SettingsSchema.DimTo
            ),
            Leaf(
                SettingPaths.ShowKeys,
                Presentation,
                L.ShowKeys,
                null,
                s => s.ShowKeys,
                (s, v) => s with { ShowKeys = v }
            ),
            Leaf(
                SettingPaths.VoiceNumbers,
                Presentation,
                L.VoiceNums,
                null,
                s => s.VoiceNumbers,
                (s, v) => s with { VoiceNumbers = v }
            ),
            Leaf(
                SettingPaths.StickyModifiersRow,
                Presentation,
                L.StickyMods,
                null,
                s => s.StickyModifiersRow,
                (s, v) => s with { StickyModifiersRow = v }
            ),
            Leaf(
                SettingPaths.ShowAlwaysVisibleRow,
                Presentation,
                L.ShowStripT,
                L.ShowStripD,
                s => s.ShowAlwaysVisibleRow,
                (s, v) => s with { ShowAlwaysVisibleRow = v }
            ),
            Leaf(
                SettingPaths.ShowProfileSelectorRow,
                Presentation,
                L.ShowTabsT,
                L.ShowTabsD,
                s => s.ShowProfileSelectorRow,
                (s, v) => s with { ShowProfileSelectorRow = v }
            ),
            Leaf(
                SettingPaths.ReduceMotion,
                Presentation,
                L.ReduceM,
                L.ReduceMD,
                s => s.ReduceMotion,
                (s, v) => s with { ReduceMotion = v }
            ),
            Leaf(
                SettingPaths.FeedbackSound,
                Presentation,
                L.FbSound,
                L.FbSoundD,
                s => s.Feedback.Sound,
                (s, v) => s with { Feedback = s.Feedback with { Sound = v } }
            ),
            Leaf(
                SettingPaths.FeedbackFlash,
                Presentation,
                L.FbFlash,
                L.FbFlashD,
                s => s.Feedback.Flash,
                (s, v) => s with { Feedback = s.Feedback with { Flash = v } }
            ),
            Leaf(
                SettingPaths.LockProfile,
                Placement,
                L.FollowApp,
                L.FollowAppD,
                s => s.LockProfile,
                (s, v) => s with { LockProfile = v }
            ),
            Leaf(
                SettingPaths.LastProfile,
                Placement,
                L.PickProfile,
                null,
                s => s.LastProfile,
                (s, v) => s with { LastProfile = v },
                LastProfile,
                nullable: true
            ),
            Leaf(
                SettingPaths.DockSide,
                Presentation,
                L.BarSide,
                null,
                s => s.Dock.Side,
                (s, v) => s with { Dock = s.Dock with { Side = v } },
                Defined
            ),
            Leaf(
                SettingPaths.DockHandleRight,
                Placement,
                L.HandlePos,
                L.HandlePosD,
                s => s.Dock.HandlePositions.Right,
                (s, v) => WithHandles(s, s.Dock.HandlePositions with { Right = v }),
                In(SettingsSchema.DockHandlePosition),
                SettingsSchema.DockHandlePosition
            ),
            Leaf(
                SettingPaths.DockHandleLeft,
                Placement,
                L.HandlePos,
                L.HandlePosD,
                s => s.Dock.HandlePositions.Left,
                (s, v) => WithHandles(s, s.Dock.HandlePositions with { Left = v }),
                In(SettingsSchema.DockHandlePosition),
                SettingsSchema.DockHandlePosition
            ),
            Leaf(
                SettingPaths.DockHandleTop,
                Placement,
                L.HandlePos,
                L.HandlePosD,
                s => s.Dock.HandlePositions.Top,
                (s, v) => WithHandles(s, s.Dock.HandlePositions with { Top = v }),
                In(SettingsSchema.DockHandlePosition),
                SettingsSchema.DockHandlePosition
            ),
            Leaf(
                SettingPaths.DockHandleBottom,
                Placement,
                L.HandlePos,
                L.HandlePosD,
                s => s.Dock.HandlePositions.Bottom,
                (s, v) => WithHandles(s, s.Dock.HandlePositions with { Bottom = v }),
                In(SettingsSchema.DockHandlePosition),
                SettingsSchema.DockHandlePosition
            ),
            Leaf(
                SettingPaths.DockHandleLocked,
                Placement,
                L.HandlePos,
                L.HandlePosD,
                s => s.Dock.HandleLocked,
                (s, v) => s with { Dock = s.Dock with { HandleLocked = v } }
            ),
            Leaf(
                SettingPaths.DockPinOpen,
                Presentation,
                L.AutoHide,
                L.AutoHideD,
                s => s.Dock.PinOpen,
                (s, v) => s with { Dock = s.Dock with { PinOpen = v } }
            ),
            Leaf(
                SettingPaths.DockGutter,
                Presentation,
                L.Gutter,
                L.GutterD,
                s => s.Dock.Gutter,
                (s, v) => s with { Dock = s.Dock with { Gutter = v } }
            ),
            Leaf(
                SettingPaths.DockPerPage,
                Presentation,
                L.DockCount,
                null,
                s => s.Dock.PerPage,
                (s, v) => s with { Dock = s.Dock with { PerPage = v } },
                OneOf(SettingsSchema.DockPerPageChoices)
            ),
            Leaf(
                SettingPaths.DockCoachDone,
                Presentation,
                L.CoachSkip,
                null,
                s => s.Dock.CoachDone,
                (s, v) => s with { Dock = s.Dock with { CoachDone = v } }
            ),
            Leaf(
                SettingPaths.PanelPositions,
                Placement,
                L.Move,
                null,
                s => s.PanelPositions,
                (s, v) => s with { PanelPositions = v },
                PanelPositions
            ),
            Leaf(
                SettingPaths.TouchPreset,
                Behavior,
                L.TouchTitle,
                L.TouchSub,
                s => s.Touch.Preset,
                (s, v) => s with { Touch = s.Touch with { Preset = v } },
                TouchPreset
            ),
            Leaf(
                SettingPaths.TouchDebounce,
                Behavior,
                L.SDeb,
                L.SDebD,
                s => s.Touch.Debounce,
                (s, v) => s with { Touch = s.Touch with { Debounce = v } },
                MillisecondsIn(SettingsSchema.TouchDebounceMs),
                SettingsSchema.TouchDebounceMs
            ),
            Leaf(
                SettingPaths.TouchHitSlop,
                Behavior,
                L.SHit,
                L.SHitD,
                s => s.Touch.HitSlopPx,
                (s, v) => s with { Touch = s.Touch with { HitSlopPx = v } },
                In(SettingsSchema.TouchHitSlopPx),
                SettingsSchema.TouchHitSlopPx
            ),
            Leaf(
                SettingPaths.TouchCancelMove,
                Behavior,
                L.SMov,
                L.SMovD,
                s => s.Touch.CancelMovePx,
                (s, v) => s with { Touch = s.Touch with { CancelMovePx = v } },
                In(SettingsSchema.TouchCancelMovePx),
                SettingsSchema.TouchCancelMovePx
            ),
            Leaf(
                SettingPaths.TouchMinContact,
                Behavior,
                L.SMin,
                L.SMinD,
                s => s.Touch.MinContact,
                (s, v) => s with { Touch = s.Touch with { MinContact = v } },
                MillisecondsIn(SettingsSchema.TouchMinContactMs),
                SettingsSchema.TouchMinContactMs
            ),
            Leaf(
                SettingPaths.MaxHold,
                Behavior,
                L.SafeMax,
                L.SafeMaxD,
                s => s.KeySafety.MaxHold,
                (s, v) => s with { KeySafety = s.KeySafety with { MaxHold = v } },
                MaxHold,
                nullable: true
            ),
            Leaf(
                SettingPaths.ReleaseOnAppSwitch,
                Behavior,
                L.SafeSwitch,
                L.SafeSwitchD,
                s => s.KeySafety.ReleaseOnAppSwitch,
                (s, v) => s with { KeySafety = s.KeySafety with { ReleaseOnAppSwitch = v } }
            ),
            Leaf(
                SettingPaths.AutoSuggestProfiles,
                Behavior,
                L.AutoSuggest,
                null,
                s => s.AutoSuggestProfiles,
                (s, v) => s with { AutoSuggestProfiles = v }
            ),
            Leaf(
                SettingPaths.KeyboardLayout,
                Behavior,
                L.KbLayout,
                null,
                s => s.Keyboard.Layout,
                (s, v) => s with { Keyboard = s.Keyboard with { Layout = v } },
                NotNull
            ),
            Leaf(
                SettingPaths.AppsLanguage,
                Behavior,
                L.KbApps,
                L.KbWhy,
                s => s.Keyboard.AppsLanguage,
                (s, v) => s with { Keyboard = s.Keyboard with { AppsLanguage = v } },
                Language
            ),
            Leaf(
                SettingPaths.KeyboardDetected,
                Behavior,
                L.Detected,
                null,
                s => s.Keyboard.Detected,
                (s, v) => s with { Keyboard = s.Keyboard with { Detected = v } }
            ),
            Leaf(
                SettingPaths.AiConsent,
                Behavior,
                L.ConsentT,
                L.ConsentD,
                s => s.Ai.Consent,
                (s, v) => s with { Ai = s.Ai with { Consent = v } }
            ),
            Leaf(
                SettingPaths.AiDisabled,
                Behavior,
                L.AiEnable,
                L.ErrAiOffD,
                s => s.Ai.Disabled,
                (s, v) => s with { Ai = s.Ai with { Disabled = v } }
            ),
            Leaf(
                SettingPaths.AiFreeLeftToday,
                Behavior,
                L.KeyUse,
                null,
                s => s.Ai.FreeLeftToday,
                (s, v) => s with { Ai = s.Ai with { FreeLeftToday = v } },
                In(SettingsSchema.AiFreeLeftToday),
                SettingsSchema.AiFreeLeftToday,
                undoable: false
            ),
            Leaf(
                SettingPaths.AiFreeResetAt,
                Behavior,
                L.KeyUse,
                null,
                s => s.Ai.FreeResetAt,
                (s, v) => s with { Ai = s.Ai with { FreeResetAt = v } },
                undoable: false,
                nullable: true
            ),
            Leaf(
                SettingPaths.AiApiKeyRef,
                Behavior,
                L.KeyUse,
                null,
                s => s.Ai.ApiKeyRef,
                (s, v) => s with { Ai = s.Ai with { ApiKeyRef = v } },
                KeyReference,
                undoable: false,
                nullable: true
            ),
            Leaf(
                SettingPaths.StartWithWindows,
                Behavior,
                L.RStart,
                L.RStartD,
                s => s.Reliability.StartWithWindows,
                (s, v) => s with { Reliability = s.Reliability with { StartWithWindows = v } }
            ),
            Leaf(
                SettingPaths.AutoBackup,
                Behavior,
                L.RAuto,
                L.RAutoD,
                s => s.Reliability.AutoBackup,
                (s, v) => s with { Reliability = s.Reliability with { AutoBackup = v } }
            ),
            Leaf(
                SettingPaths.CrashRecovery,
                Behavior,
                L.RCrash,
                L.RCrashD,
                s => s.Reliability.CrashRecovery,
                (s, v) => s with { Reliability = s.Reliability with { CrashRecovery = v } }
            ),
            Leaf(
                SettingPaths.SingleInstance,
                Behavior,
                L.RSingleD,
                null,
                s => s.Reliability.SingleInstance,
                (s, v) => s with { Reliability = s.Reliability with { SingleInstance = v } }
            ),
            Leaf(
                SettingPaths.RunAsAdmin,
                Behavior,
                L.RAdmin,
                L.RAdminD,
                s => s.Reliability.RunAsAdmin,
                (s, v) => s with { Reliability = s.Reliability with { RunAsAdmin = v } }
            ),
            Leaf(
                SettingPaths.UpdatesAutomatic,
                Behavior,
                L.UpdAuto,
                L.UpdAutoD,
                s => s.Updates.Automatic,
                (s, v) => s with { Updates = s.Updates with { Automatic = v } }
            ),
            Leaf(
                SettingPaths.UpdatesAskBefore,
                Behavior,
                L.UpdAsk,
                L.UpdAskD,
                s => s.Updates.AskBefore,
                (s, v) => s with { Updates = s.Updates with { AskBefore = v } }
            ),
            Leaf(
                SettingPaths.UpdatesBackupBefore,
                Behavior,
                L.UpdBackup,
                L.UpdBackupD,
                s => s.Updates.BackupBefore,
                (s, v) => s with { Updates = s.Updates with { BackupBefore = v } }
            ),
            Leaf(
                SettingPaths.UpdatesChannel,
                Behavior,
                L.Channel,
                L.ChannelD,
                s => s.Updates.Channel,
                (s, v) => s with { Updates = s.Updates with { Channel = v } },
                Defined
            ),
            Leaf(
                SettingPaths.NoKeyboardUser,
                Behavior,
                L.UNokb,
                null,
                s => s.NoKeyboardUser,
                (s, v) => s with { NoKeyboardUser = v }
            ),
        ];

    private static SettingLeaf<T> Leaf<T>(
        string path,
        SettingScope scope,
        Message label,
        Message? description,
        Func<UserSettings, T> get,
        Func<UserSettings, T, UserSettings> set,
        Func<T, T, T>? repair = null,
        SettingRange? range = null,
        bool? undoable = null,
        bool nullable = false
    )
    {
        var fallback = get(SettingsDefaults.Value);
        var descriptor = new SettingDescriptor(
            path,
            scope,
            undoable ?? scope == SettingScope.Behavior,
            (object?)fallback ?? NoSettingValue.Instance,
            range,
            label.Key,
            description?.Key
        );
        return new SettingLeaf<T>(
            descriptor,
            get,
            set,
            repair is null ? static value => value : value => repair(value, fallback),
            nullable
        );
    }

    private static UserSettings WithHandles(UserSettings settings, DockHandlePositions handles) =>
        settings with
        {
            Dock = settings.Dock with { HandlePositions = handles },
        };

    private static T Defined<T>(T value, T fallback)
        where T : struct, Enum => Enum.IsDefined(value) ? value : fallback;

    private static LangCode Language(LangCode value, LangCode fallback) =>
        string.IsNullOrEmpty(value.Value) ? fallback : value;

    private static string NotNull(string value, string fallback) => value ?? fallback;

    private static string TouchPreset(string value, string fallback) =>
        string.IsNullOrEmpty(value) ? SettingsSchema.PersonalTouchPreset : value;

    private static ProfileId? LastProfile(ProfileId? value, ProfileId? fallback) =>
        value is { } profile && string.IsNullOrEmpty(profile.Value) ? fallback : value;

    private static string? KeyReference(string? value, string? fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value;

    private static Func<int, int, int> In(SettingRange range) =>
        (value, _) => (int)range.Clamp(value);

    private static Func<double, double, double> InRange(SettingRange range) =>
        (value, fallback) => double.IsFinite(value) ? range.Clamp(value) : fallback;

    private static Func<TimeSpan, TimeSpan, TimeSpan> MillisecondsIn(SettingRange range) =>
        (value, _) =>
        {
            var min = TimeSpan.FromMilliseconds(range.Min);
            var max = TimeSpan.FromMilliseconds(range.Max);
            return value < min ? min
                : value > max ? max
                : value;
        };

    private static Func<int, int, int> OneOf(ImmutableArray<int> choices) =>
        (value, _) =>
        {
            var best = choices[0];
            foreach (var choice in choices)
            {
                if (Math.Abs((long)choice - value) < Math.Abs((long)best - value))
                {
                    best = choice;
                }
            }

            return best;
        };

    private static TimeSpan? MaxHold(TimeSpan? value, TimeSpan? fallback)
    {
        if (value is not { } limit || limit <= TimeSpan.Zero)
        {
            // docs/02: maxHoldSec 0 is «Never».
            return null;
        }

        var best = SettingsSchema.MaxHoldChoices[0];
        foreach (var choice in SettingsSchema.MaxHoldChoices)
        {
            if ((choice - limit).Duration() < (best - limit).Duration())
            {
                best = choice;
            }
        }

        return best;
    }

    private static ValueList<MonitorPosition> PanelPositions(
        ValueList<MonitorPosition> value,
        ValueList<MonitorPosition> fallback
    )
    {
        var monitors = new HashSet<string>(StringComparer.Ordinal);
        var kept = ImmutableArray.CreateBuilder<MonitorPosition>();
        foreach (var position in value)
        {
            if (position is { MonitorId.Length: > 0 } && monitors.Add(position.MonitorId))
            {
                kept.Add(position);
            }
        }

        return kept.Count == value.Count
            ? value
            : new ValueList<MonitorPosition>(kept.ToImmutable());
    }
}
