namespace Clicalo.Domain.Settings;

/// <summary>Tab view settings (GEN-010, docs/02 <c>dock</c>).</summary>
public sealed record DockSettings
{
    /// <summary>Screen edge.</summary>
    public required DockSide Side { get; init; }

    /// <summary>Handle position per edge.</summary>
    public required DockHandlePositions HandlePositions { get; init; }

    /// <summary>Whether the handle position is locked (PES-003).</summary>
    public required bool HandleLocked { get; init; }

    /// <summary>Whether the bar stays open instead of hiding after an action (docs/02 <c>pinOpen</c>).</summary>
    public required bool PinOpen { get; init; }

    /// <summary>Whether a gutter keeps apps from going under the bar.</summary>
    public required bool Gutter { get; init; }

    /// <summary>Buttons per page of the bar: one of <see cref="SettingsSchema.DockPerPageChoices"/>.</summary>
    public required int PerPage { get; init; }

    /// <summary>Whether the Tab guide was completed (PES-015).</summary>
    public required bool CoachDone { get; init; }
}
