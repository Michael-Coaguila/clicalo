using System.Collections.Immutable;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Timing;

namespace Clicalo.Domain.Settings;

/// <summary>
/// The schema of <see cref="UserSettings"/> (blueprint §6.3): ranges of docs/02 and the GEN-* requirements, defaults
/// and the descriptor of every setting. The single source of the defaults, the repair on load, the
/// simple rows of the UI and the undo of settings (only undoable leaves are restored, §6.4).
/// </summary>
public static class SettingsSchema
{
    /// <summary>Columns: 2, 3 or 4 (GEN-008).</summary>
    public static SettingRange Columns { get; } = new(2, 4, 1);

    /// <summary>Visible rows: 0 (automatic) to 3 (GEN-006).</summary>
    public static SettingRange RowsPreference { get; } = new(0, 3, 1);

    /// <summary>Text scale: 100 to 150 % in steps of 10 (GEN-004).</summary>
    public static SettingRange TextScalePercent { get; } = new(100, 150, 10);

    /// <summary>Opacity: 0.30 to 1.00 in steps of 0.05 (GEN-009, docs/02).</summary>
    public static SettingRange Opacity { get; } = new(0.30, 1.00, 0.05);

    /// <summary>The − and + buttons of Quick settings move the opacity by 0.10 (AJR-002).</summary>
    public static double OpacityButtonStep => 0.10;

    /// <summary>Dimmed opacity: 0.10 to 0.80 in steps of 0.05 (GEN-009).</summary>
    public static SettingRange DimTo { get; } = new(0.10, 0.80, 0.05);

    /// <summary>Handle position: ±10 inside 8 to 92 % (GEN-010); clamped, never snapped.</summary>
    public static SettingRange DockHandlePosition { get; } = new(8, 92, 10);

    /// <summary>Buttons per page of the Tab bar: 4, 5, 6 or 8 (GEN-010).</summary>
    public static ImmutableArray<int> DockPerPageChoices { get; } = [4, 5, 6, 8];

    /// <summary>Touch debounce: 0 to 1000 ms in steps of 50 (TAC-005); preset values off the step are kept.</summary>
    public static SettingRange TouchDebounceMs { get; } = new(0, 1000, 50);

    /// <summary>Touch hit slop: 0 to 40 px in steps of 2 (TAC-005).</summary>
    public static SettingRange TouchHitSlopPx { get; } = new(0, 40, 2);

    /// <summary>Touch cancel distance: 0 to 80 px in steps of 5 (TAC-005); Strong tremor's 28 is kept exact.</summary>
    public static SettingRange TouchCancelMovePx { get; } = new(0, 80, 5);

    /// <summary>Touch minimum contact: 0 to 300 ms in steps of 10 (TAC-005).</summary>
    public static SettingRange TouchMinContactMs { get; } = new(0, 300, 10);

    /// <summary>
    /// Multiplier of the confirmation window and of the duration of notices: ×1, ×2 or ×3 (ACC-006); also the choices
    /// of its control.
    /// </summary>
    public static SettingRange TimeMultiplier { get; } = new(1, 3, 1);

    /// <summary>Free AI requests left today: 0 to the daily quota (PLA-003).</summary>
    public static SettingRange AiFreeLeftToday { get; } = new(0, Timings.Ai.AiFreeDailyQuota, 1);

    /// <summary>
    /// The automatic release limits offered (GEN-012, SEG-004: 30 s, 1 min, 2 min); «Never» is <see langword="null"/>.
    /// </summary>
    public static ValueList<TimeSpan> MaxHoldChoices { get; } =
    [.. Timings.KeySafety.AutoReleaseChoices];

    /// <summary>The touch preset id of the user's own values (TAC-001, «Personal»).</summary>
    public static string PersonalTouchPreset => "personal";

    /// <summary>Stands for <see langword="null"/> in <see cref="SettingDescriptor.Default"/>.</summary>
    public static object NoValue => NoSettingValue.Instance;

    /// <summary>The settings of a new installation (GEN-*, docs/02).</summary>
    public static UserSettings Defaults => SettingsDefaults.Value;

    /// <summary>Every setting, one descriptor per leaf of <see cref="UserSettings"/>, in the order of docs/02.</summary>
    public static ImmutableArray<SettingDescriptor> All => SettingsRegistry.Descriptors;

    /// <summary>The descriptor of the setting at <paramref name="path"/>, or <see langword="null"/>.</summary>
    /// <param name="path">A <see cref="SettingPaths"/> value.</param>
    public static SettingDescriptor? Find(string path) =>
        path is not null && SettingsRegistry.ByPath.TryGetValue(path, out var leaf)
            ? leaf.Descriptor
            : null;

    /// <summary>Reads the value at <paramref name="path"/>, boxed; <see langword="null"/> for an absent optional value.</summary>
    /// <param name="settings">The settings.</param>
    /// <param name="path">A <see cref="SettingPaths"/> value.</param>
    /// <param name="value">The value.</param>
    public static bool TryRead(UserSettings settings, string path, out object? value)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (path is not null && SettingsRegistry.ByPath.TryGetValue(path, out var leaf))
        {
            value = leaf.Read(settings);
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Writes <paramref name="value"/> at <paramref name="path"/> when it has the setting's type (an <see cref="int"/>
    /// for counts and pixels, a <see cref="double"/> for opacities, a <see cref="TimeSpan"/> for durations, the enum or
    /// record of the leaf) and is inside its range or choices. <see langword="null"/> or <see cref="NoValue"/> clears an
    /// optional setting. Writing the current value returns the same instance.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="path">A <see cref="SettingPaths"/> value.</param>
    /// <param name="value">The new value.</param>
    /// <param name="result">The new settings, or <paramref name="settings"/> when nothing was written.</param>
    public static SettingWriteStatus Write(
        UserSettings settings,
        string path,
        object? value,
        out UserSettings result
    )
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (path is null || !SettingsRegistry.ByPath.TryGetValue(path, out var leaf))
        {
            result = settings;
            return SettingWriteStatus.UnknownPath;
        }

        return leaf.TryWrite(settings, value, out result);
    }

    /// <summary>
    /// Brings every value inside its range or choices, keeping valid ones untouched (§6.5: repair on load). Reports
    /// the paths it changed so the mapper can log them. A missing group (<c>feedback</c>, <c>dock</c>…) takes its
    /// defaults. Values between the steps of their range are valid: only the − / + controls snap (TAC-005).
    /// </summary>
    /// <param name="settings">Settings read from disk or imported.</param>
    /// <param name="changedPaths">Paths of the values it changed.</param>
    public static UserSettings Clamp(UserSettings settings, out ImmutableArray<string> changedPaths)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var changed = ImmutableArray.CreateBuilder<string>();
        var repaired = RepairGroups(settings, changed);
        foreach (var leaf in SettingsRegistry.Leaves)
        {
            repaired = leaf.Repair(repaired, out var leafChanged);
            if (leafChanged)
            {
                changed.Add(leaf.Descriptor.Path);
            }
        }

        changedPaths = changed.ToImmutable();
        return changedPaths.IsEmpty ? settings : repaired;
    }

    /// <summary>
    /// <paramref name="current"/> with the undoable settings taken from <paramref name="source"/> and every other one
    /// kept: undoing a change of «Release on app switch» never rewinds the theme chosen afterwards (§6.4, DAT-006).
    /// </summary>
    /// <param name="current">The settings now.</param>
    /// <param name="source">The settings of the undo entry.</param>
    public static UserSettings WithUndoableFrom(UserSettings current, UserSettings source)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(source);
        var result = current;
        foreach (var leaf in SettingsRegistry.Leaves)
        {
            if (leaf.Descriptor.Undoable)
            {
                result = leaf.CopyFrom(result, source);
            }
        }

        return result;
    }

    private static UserSettings RepairGroups(
        UserSettings settings,
        ImmutableArray<string>.Builder changed
    )
    {
        var defaults = SettingsDefaults.Value;
        var result = settings;
        if (result.Feedback is null)
        {
            result = result with { Feedback = defaults.Feedback };
            changed.Add("feedback");
        }

        if (result.Dock is null)
        {
            result = result with { Dock = defaults.Dock };
            changed.Add("dock");
        }
        else if (result.Dock.HandlePositions is null)
        {
            result = result with
            {
                Dock = result.Dock with { HandlePositions = defaults.Dock.HandlePositions },
            };
            changed.Add("dock.handlePosBySide");
        }

        if (result.Touch is null)
        {
            result = result with { Touch = defaults.Touch };
            changed.Add("touch");
        }

        if (result.KeySafety is null)
        {
            result = result with { KeySafety = defaults.KeySafety };
            changed.Add("keySafety");
        }

        if (result.Keyboard is null)
        {
            result = result with { Keyboard = defaults.Keyboard };
            changed.Add("keyboard");
        }

        if (result.Ai is null)
        {
            result = result with { Ai = defaults.Ai };
            changed.Add("ai");
        }

        if (result.Reliability is null)
        {
            result = result with { Reliability = defaults.Reliability };
            changed.Add("reliability");
        }

        if (result.Updates is null)
        {
            result = result with { Updates = defaults.Updates };
            changed.Add("updates");
        }

        if (result.GlobalHotkey is null)
        {
            result = result with { GlobalHotkey = defaults.GlobalHotkey };
            changed.Add("globalHotkey");
        }

        return result;
    }
}
