using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>What <see cref="SurfaceSet"/> shows and where (PAN-001, PAN-006, docs/04), as the composition decides it.</summary>
/// <param name="Form">The one form of the panel.</param>
/// <param name="Dock">The settings of the Tab view: edge, handle positions, gutter, lock.</param>
/// <param name="Positions">The panel positions saved per monitor.</param>
/// <param name="Metrics">The measures of the panel size (the thickness of the bar).</param>
/// <param name="Panic">Something is held: the floating «Release all» shows beside the Tab view or the bubble.</param>
/// <param name="Flyout">The window beside the open bar.</param>
/// <param name="ShowsCoach">The first-time guide shows beside the open bar.</param>
public sealed record SurfaceLayout(
    PanelForm Form,
    DockSettings Dock,
    ValueList<MonitorPosition> Positions,
    SizeMetrics Metrics,
    bool Panic,
    DockFlyout Flyout,
    bool ShowsCoach
)
{
    /// <summary>
    /// The settings the handle reads its position from, per monitor and edge (PES-016); <see langword="null"/> uses the
    /// position per edge of <see cref="Dock"/>.
    /// </summary>
    public UserSettings? Settings { get; init; }

    /// <summary>The notice surface of the Tab view shows beside the bar or the handle (PES-014).</summary>
    public bool ShowsNotice { get; init; }

    /// <summary>The menu of a shortcut shows beside the open bar (CUA-014, PES-010).</summary>
    public bool ShowsMenu { get; init; }

    /// <summary>Quick settings show beside the open bar (PES-009).</summary>
    public bool ShowsQuick { get; init; }
}
