using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Settings;

/// <summary>
/// Every setting of the user document (docs/02, DAT-001), the blueprint's <c>Settings</c> (renamed so it does not
/// clash with its namespace). Ranges, defaults, clamping on load and the simple rows of the UI all come from <see cref="SettingsSchema"/>. Repeated combinations marked «It's fine» live in the document's
/// <c>DuplicatePolicy</c>, not here.
/// </summary>
public sealed record UserSettings
{
    /// <summary>Interface language (GEN-002).</summary>
    public required LangCode Language { get; init; }

    /// <summary>Theme (GEN-003).</summary>
    public required ThemeChoice Theme { get; init; }

    /// <summary>Panel view (GEN-005).</summary>
    public required PanelDensity Density { get; init; }

    /// <summary>Panel size (GEN-004).</summary>
    public required PanelSize Size { get; init; }

    /// <summary>Columns, inside <see cref="SettingsSchema.Columns"/> (GEN-008).</summary>
    public required int Columns { get; init; }

    /// <summary>Visible rows, 0 for automatic, inside <see cref="SettingsSchema.RowsPreference"/> (GEN-006).</summary>
    public required int RowsPreference { get; init; }

    /// <summary>Text scale in percent, inside <see cref="SettingsSchema.TextScalePercent"/> (GEN-004).</summary>
    public required int TextScalePercent { get; init; }

    /// <summary>Opacity, inside <see cref="SettingsSchema.Opacity"/> (GEN-009).</summary>
    public required double Opacity { get; init; }

    /// <summary>Dim when the finger or pointer leaves (GEN-009).</summary>
    public required bool AutoDim { get; init; }

    /// <summary>Dimmed opacity, inside <see cref="SettingsSchema.DimTo"/>; the effective one is min(DimTo, Opacity).</summary>
    public required double DimTo { get; init; }

    /// <summary>Show the keys under each name (GEN-008).</summary>
    public required bool ShowKeys { get; init; }

    /// <summary>Show voice numbers (ACC-*).</summary>
    public required bool VoiceNumbers { get; init; }

    /// <summary>Show the sticky modifiers row (FIJ-*).</summary>
    public required bool StickyModifiersRow { get; init; }

    /// <summary>Show the Always visible row (GEN-007, docs/02 <c>showStripRow</c>).</summary>
    public required bool ShowAlwaysVisibleRow { get; init; }

    /// <summary>Show the profile selector row (GEN-007, docs/02 <c>showTabsRow</c>).</summary>
    public required bool ShowProfileSelectorRow { get; init; }

    /// <summary>Reduce motion (TEM-006).</summary>
    public required bool ReduceMotion { get; init; }

    /// <summary>Sound and flash after a tap (GEN-011).</summary>
    public required FeedbackSettings Feedback { get; init; }

    /// <summary>Fixed (<see langword="true"/>) or Auto profile (PER-001, PER-006).</summary>
    public required bool LockProfile { get; init; }

    /// <summary>The profile the profile button returns to from Frequents (PER-004); repaired if it dangles.</summary>
    public required ProfileId? LastProfile { get; init; }

    /// <summary>Tab view (GEN-010).</summary>
    public required DockSettings Dock { get; init; }

    /// <summary>Panel position per monitor; placement, never undoable (§6.4).</summary>
    public required ValueList<MonitorPosition> PanelPositions { get; init; }

    /// <summary>Touch filter (TAC-001).</summary>
    public required TouchFilterSettings Touch { get; init; }

    /// <summary>Key safety (GEN-012).</summary>
    public required KeySafetySettings KeySafety { get; init; }

    /// <summary>Suggest a profile for apps without one (PER-009).</summary>
    public required bool AutoSuggestProfiles { get; init; }

    /// <summary>Keyboard and apps language.</summary>
    public required KeyboardSettings Keyboard { get; init; }

    /// <summary>AI (PLA-003).</summary>
    public required AiSettings Ai { get; init; }

    /// <summary>Startup and stability (SIS-*).</summary>
    public required ReliabilitySettings Reliability { get; init; }

    /// <summary>Updates (ACT-*).</summary>
    public required UpdateSettings Updates { get; init; }

    /// <summary>The user cannot use the keyboard (BIE-005): library, AI and dictation first.</summary>
    public required bool NoKeyboardUser { get; init; }
}
