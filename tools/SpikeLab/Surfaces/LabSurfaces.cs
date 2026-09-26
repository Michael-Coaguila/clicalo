using System.Windows;
using System.Windows.Controls;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Surfaces;

/// <summary>
/// Every surface of the laboratory and where it starts on the primary monitor (<see cref="LabLayout"/>, from the size
/// each one measures): the guide strip bottom left, the panel bottom right with the bubble and the profile side window
/// above it, and the edge bar on the right edge at the top with its side window and the search to its left. The
/// top-left quarter stays free for the app under test. Every surface is kept inside the work area when it appears; the
/// maintainer can drag the panel and the guide strip by their handles.
/// </summary>
internal sealed class LabSurfaces : IDisposable
{
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
    public bool ShowUnderTest()
    {
        Place();
        return UnderTest.Select(surface => surface.TryShow()).ToArray().All(shown => shown);
    }

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

    /// <summary>
    /// Places every surface that has not appeared yet from its measured size (logical pixels of the primary monitor);
    /// the ones already shown keep where the maintainer left them.
    /// </summary>
    public void Place()
    {
        var area = SystemParameters.WorkArea;
        Guide.View.Width = LabLayout.StripWidth(area, Panel.MeasureSize());
        var arrangement = LabLayout.Arrange(
            area,
            All.ToDictionary(surface => surface.Id, surface => surface.MeasureSize())
        );
        foreach (var surface in All.Where(surface => !surface.IsVisible))
        {
            var rect = arrangement[surface.Id];
            surface.Left = rect.Left;
            surface.Top = rect.Top;
        }
    }
}
