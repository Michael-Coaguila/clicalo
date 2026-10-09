using Clicalo.Domain.Settings;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Dock;

namespace Clicalo.UI.Wpf.Surfaces;

/// <summary>Where <see cref="SurfaceSet"/> sends what happens on its surfaces; the composition gives them.</summary>
/// <param name="ReleaseAll">The floating «Release all» (REG-03).</param>
/// <param name="HoldEnded">The end of a hold of the bar, by contact (INV-9).</param>
/// <param name="SavePosition">The panel or the bubble ended a drag on a monitor (PAN-006).</param>
/// <param name="SaveHandle">The handle ended a drag along its edge (PES-002).</param>
/// <param name="TileUsed">A shortcut of the bar ran from a tap; true when it was in a window beside it (PES-010).</param>
/// <param name="HoldReleased">A Mantener of the bar was released (PES-012).</param>
public sealed record SurfaceCallbacks(
    Action ReleaseAll,
    Action<uint, ContactSummary, HoldEndReason> HoldEnded,
    Action<MonitorPosition> SavePosition,
    Action<DockSide, int> SaveHandle,
    Action<DockTileViewModel, bool> TileUsed,
    Action HoldReleased
);
