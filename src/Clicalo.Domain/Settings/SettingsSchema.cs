using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Settings;

/// <summary>
/// The schema of <see cref="UserSettings"/> (blueprint §6.3): ranges of docs/02 and the GEN-* requirements, defaults
/// and the descriptor of every setting.
/// </summary>
[SuppressMessage(
    "Design",
    "MA0025:Implement the functionality instead of throwing NotImplementedException",
    Justification = "M2 contract; the domain package implements it (docs/testing/spikes/M2-ownership.md)."
)]
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

    /// <summary>Dimmed opacity: 0.10 to 0.80 in steps of 0.05 (GEN-009).</summary>
    public static SettingRange DimTo { get; } = new(0.10, 0.80, 0.05);

    /// <summary>Handle position: ±10 inside 8 to 92 % (GEN-010); clamped, never snapped.</summary>
    public static SettingRange DockHandlePosition { get; } = new(8, 92, 10);

    /// <summary>Buttons per page of the Tab bar: 4, 5, 6 or 8 (GEN-010).</summary>
    public static ImmutableArray<int> DockPerPageChoices { get; } = [4, 5, 6, 8];

    /// <summary>The settings of a new installation (GEN-*, docs/02).</summary>
    public static UserSettings Defaults => throw new NotImplementedException();

    /// <summary>Every setting, one descriptor per leaf of <see cref="UserSettings"/>.</summary>
    public static ImmutableArray<SettingDescriptor> All => throw new NotImplementedException();

    /// <summary>
    /// Brings every value inside its range or choices, keeping valid ones untouched (§6.5: repair on load). Reports
    /// the paths it changed so the mapper can log them.
    /// </summary>
    /// <param name="settings">Settings read from disk or converted from v1.</param>
    /// <param name="changedPaths">Paths of the values it changed.</param>
    public static UserSettings Clamp(
        UserSettings settings,
        out ImmutableArray<string> changedPaths
    ) => throw new NotImplementedException();
}
