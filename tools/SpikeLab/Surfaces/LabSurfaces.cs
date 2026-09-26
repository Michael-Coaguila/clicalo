using System.Windows;
using System.Windows.Controls;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Every surface of the laboratory and where it starts on the primary monitor: the guide strip at the top, the panel
/// bottom right, the edge bar on the right edge with its side window, the bubble bottom left, and the search and
/// profile windows next to the panel. The maintainer can move the panel by its handle.
/// </summary>
internal sealed class LabSurfaces : IDisposable
{
    private const double EdgeMargin = 16;

    /// <summary>Creates the surfaces (no window is shown yet).</summary>
    public LabSurfaces(SurfaceRegistry registry, LabSurfaceContext context)
    {
        Panel = new PanelSurface(registry, context);
        Dock = new TileStripSurface(
            LabSurfaceIds.Dock,
            registry,
            context,
            SurfaceGroup.TabWithSide,
            LabTiles.Dock,
            Orientation.Vertical,
            tileWidth: 124,
            tileHeight: 64
        );
        DockSide = new TileStripSurface(
            LabSurfaceIds.DockSide,
            registry,
            context,
            SurfaceGroup.TabWithSide,
            LabTiles.Side,
            Orientation.Vertical,
            tileWidth: 132,
            tileHeight: 64
        );
        Profiles = new TileStripSurface(
            LabSurfaceIds.Profiles,
            registry,
            context,
            SurfaceGroup.Panel,
            LabTiles.Profiles,
            Orientation.Horizontal,
            tileWidth: 120,
            tileHeight: 56
        );
        Bubble = new TileStripSurface(
            LabSurfaceIds.Bubble,
            registry,
            context,
            SurfaceGroup.Bubble,
            LabTiles.Bubble,
            Orientation.Horizontal,
            tileWidth: 56,
            tileHeight: 56
        );
        Search = new SearchSurface(registry, context);
        Guide = new GuideStripSurface(registry, context);
        Place();
    }

    /// <summary>The panel.</summary>
    public PanelSurface Panel { get; }

    /// <summary>The edge bar («Pestaña») with its handle.</summary>
    public TileStripSurface Dock { get; }

    /// <summary>The side window of the edge bar.</summary>
    public TileStripSurface DockSide { get; }

    /// <summary>The profile side window.</summary>
    public TileStripSurface Profiles { get; }

    /// <summary>The bubble.</summary>
    public TileStripSurface Bubble { get; }

    /// <summary>The search (S4).</summary>
    public SearchSurface Search { get; }

    /// <summary>The guide strip.</summary>
    public GuideStripSurface Guide { get; }

    /// <summary>The surfaces under test that «Mostrar superficies» shows.</summary>
    public IEnumerable<LabSurface> UnderTest => [Panel, Dock, Bubble];

    /// <summary>Every surface.</summary>
    public IEnumerable<LabSurface> All => [Panel, Dock, DockSide, Profiles, Bubble, Search, Guide];

    /// <summary>Shows the panel, the edge bar and the bubble; false if the windowing is not integrated yet.</summary>
    public bool ShowUnderTest() =>
        UnderTest.Select(surface => surface.TryShow()).ToArray().All(shown => shown);

    /// <summary>Hides every surface except the guide strip.</summary>
    public void HideUnderTest()
    {
        foreach (var surface in All.Where(surface => surface != Guide))
        {
            surface.TryHide();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var surface in All)
        {
            try
            {
                surface.Close();
            }
            catch (InvalidOperationException)
            {
                // A surface whose handle never finished its creation (windowing pending) cannot close normally.
            }
        }
    }

    private void Place()
    {
        var area = SystemParameters.WorkArea;
        Guide.Left = area.Left + Math.Max(0, (area.Width - 1000) / 2);
        Guide.Top = area.Top + 8;

        const double PanelWidth = 4 * 112 + 44;
        const double PanelHeight = 4 * 80 + 12;
        Panel.Left = area.Right - PanelWidth - EdgeMargin;
        Panel.Top = area.Bottom - PanelHeight - EdgeMargin;
        Profiles.Left = Panel.Left;
        Profiles.Top = Panel.Top - 72;
        Search.Left = Panel.Left - 380;
        Search.Top = Panel.Top;

        Dock.Left = area.Right - 96;
        Dock.Top = area.Top + Math.Max(0, (area.Height / 2) - 300);
        DockSide.Left = Dock.Left - 136;
        DockSide.Top = Dock.Top;

        Bubble.Left = area.Left + 24;
        Bubble.Top = area.Bottom - 64 - 24;
    }
}
