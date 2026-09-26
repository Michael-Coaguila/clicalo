using System.Windows.Automation.Peers;
using System.Windows.Threading;
using Clicalo.Application.Ports;
using Clicalo.Domain.Touch;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.Tools.SpikeLab.Composition;
using Clicalo.Tools.SpikeLab.Measurement;
using Clicalo.Tools.SpikeLab.Scripting;
using Clicalo.Tools.SpikeLab.Surfaces;
using Clicalo.Tools.SpikeLab.Tiles;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Windowing;

namespace Clicalo.Tools.SpikeLab.Tests.Views;

/// <summary>
/// The lab surfaces in process, created but never shown: real <see cref="NonActivatingWindow"/>s with real tiles.
/// </summary>
public sealed class LabSurfacesTests
{
    [Fact]
    public void The_surfaces_are_non_activating_windows_with_the_ids_of_blueprint_8_1() =>
        WpfThread.Invoke(() =>
        {
            var surfaces = Create(out _);

            surfaces.All.ShouldAllBe(surface => surface is NonActivatingWindow);
            surfaces.All.ShouldAllBe(surface =>
                !surface.ShowActivated && surface.Topmost && !surface.ShowInTaskbar
            );
            surfaces
                .All.Select(surface => surface.Id)
                .ShouldBe([
                    new SurfaceId(SurfaceKind.Panel, 0),
                    new SurfaceId(SurfaceKind.Dock, 0),
                    new SurfaceId(SurfaceKind.SideWindow, 1),
                    new SurfaceId(SurfaceKind.SideWindow, 3),
                    new SurfaceId(SurfaceKind.Bubble, 0),
                    new SurfaceId(SurfaceKind.SideWindow, 2),
                    new SurfaceId(SurfaceKind.Notice, 9),
                ]);
            surfaces
                .UnderTest.Select(surface => surface.Group)
                .ShouldBe([SurfaceGroup.Panel, SurfaceGroup.TabWithSide, SurfaceGroup.Bubble]);
        });

    [Fact]
    public void The_panel_has_its_fourteen_tiles_in_voice_number_order_with_real_patterns() =>
        WpfThread.Invoke(() =>
        {
            var panel = Create(out _).Panel;

            var tiles = panel.Tiles.ToArray();
            tiles.Select(tile => tile.Tile.Id).ShouldBe(LabTiles.Panel.Select(tile => tile.Id));
            tiles.ShouldAllBe(tile => tile.Control.Width >= 44 && tile.Control.MinHeight >= 44);
            var shift = panel.Find(LabTiles.Shift).ShouldNotBeNull();
            shift.Pattern.ShouldBe(ShortcutTilePattern.Toggle);
            UIElementAutomationPeer
                .CreatePeerForElement(shift)
                .GetPattern(PatternInterface.Toggle)
                .ShouldNotBeNull();
            panel
                .Find(LabTiles.Profile)
                .ShouldNotBeNull()
                .Pattern.ShouldBe(ShortcutTilePattern.ExpandCollapse);
        });

    [Fact]
    public void Voice_numbers_start_the_accessible_name() =>
        WpfThread.Invoke(() =>
        {
            var copy = Create(out _).Panel.Find(LabTiles.Copy).ShouldNotBeNull();

            copy.VoiceNumber = LabTiles.VoiceNumberOf(LabTiles.Copy);

            UIElementAutomationPeer.CreatePeerForElement(copy).GetName().ShouldBe("4 Copiar");
        });

    [Fact]
    public void A_UI_Automation_command_reaches_the_session_as_a_command_of_its_surface() =>
        WpfThread.Invoke(() =>
        {
            var surfaces = Create(out var sink);
            var bold = surfaces.Panel.Find(LabTiles.Bold).ShouldNotBeNull();

            (
                (System.Windows.Automation.Provider.IInvokeProvider)
                    UIElementAutomationPeer
                        .CreatePeerForElement(bold)
                        .GetPattern(PatternInterface.Invoke)
            ).Invoke();

            // Invoke returns at once and the tile raises Invoked on the next dispatcher turn (UI Automation, S3).
            Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.Background);

            var input = sink.Inputs.ShouldHaveSingleItem();
            input.IsCommand.ShouldBeTrue();
            input.Channel.ShouldBe("uia");
            input.Surface.ShouldBe("Panel#0");
            input.Group.ShouldBe(SurfaceGroup.Panel);
            input.Tile.Id.ShouldBe(LabTiles.Bold);
        });

    [Fact]
    public void The_search_field_is_labeled_and_has_its_dictation_button() =>
        WpfThread.Invoke(() =>
        {
            var search = Create(out _).Search;

            search
                .Tiles.Select(tile => tile.Tile.Name)
                .ShouldBe(["Dictar", "Resultado Negrita", "Cerrar búsqueda"]);
            search.FieldLength.ShouldBe(0);
        });

    private static LabSurfaces Create(out RecordingSink sink)
    {
        sink = new RecordingSink();
        var guard = new ActivationGuard(new ArbiterRelay(), TimeProvider.System);
        var registry = new SurfaceRegistry(new OwnerAnchor(), guard);
        return new LabSurfaces(
            registry,
            new LabSurfaceContext(
                TimeProvider.System,
                sink,
                new ComponentBoard(),
                new SurfaceDirectory(),
                new LabEventLog(TimeProvider.System)
            )
        );
    }

    private sealed class RecordingSink : ILabInputSink
    {
        public List<TileInput> Inputs { get; } = [];

        public void OnTile(TileInput input) => Inputs.Add(input);

        public void OnHandleDrag(string surface, SurfaceGroup group, PointerKind? pointer) { }

        public void OnIgnoredTouch(string surface, IgnoreReason reason) { }

        public void OnActivationMessage(string surface, string message) { }
    }
}
