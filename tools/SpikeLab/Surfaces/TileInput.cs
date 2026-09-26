using Clicalo.Domain.Touch;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>A tap or a UI Automation command on a tile of a laboratory surface or window.</summary>
/// <param name="Tile">The tile definition.</param>
/// <param name="Control">The real <see cref="ShortcutTile"/> it happened on; null for another target (the search field).</param>
/// <param name="Surface">The surface («Panel#0»), or «Ventana de control».</param>
/// <param name="Group">The group of the surface under test.</param>
/// <param name="StartedAt">When the gesture completed or the UI Automation call arrived (latency start).</param>
internal sealed record TileInput(
    LabTile Tile,
    ShortcutTile? Control,
    string Surface,
    SurfaceGroup Group,
    DateTimeOffset StartedAt
)
{
    /// <summary>True for a UI Automation command; false for a tap.</summary>
    public bool IsCommand { get; init; }

    /// <summary>The pattern of a command.</summary>
    public CommandPattern Pattern { get; init; }

    /// <summary>For ExpandCollapse: true to expand, false to collapse.</summary>
    public bool Expand { get; init; }

    /// <summary>The device of a tap; null for a command.</summary>
    public PointerKind? Pointer { get; init; }

    /// <summary>«pointer» or «uia».</summary>
    public string Channel { get; init; } = "pointer";
}
