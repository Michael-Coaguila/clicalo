using System.Globalization;
using System.Text;
using System.Windows;
using Clicalo.Application.Profiles;
using Clicalo.Application.Session;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.Presentation.Panel.Search;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.Windowing.IntegrationTests.Automation.Audit;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// AJR-001, CUA-001 and PAN-006 on a real panel on screen: opening and closing Quick settings, the menu of a shortcut,
/// the search or edit mode changes the height of the window in one step, and the panel never passes the bottom of the
/// work area. The sheet used to be sized with the rows the grid was about
/// to get and the height the grid still had, so the panel grew past the screen, shrank and grew again for about a
/// second (C:\dev\clicalo-qa\live). The window is non-activating and never takes the foreground; nothing is typed.
/// </summary>
[Trait("Requires", "Desktop")]
[Collection(DesktopCollectionDefinition.Name)]
public sealed class PanelLayerStabilityDesktopTests
{
    private static readonly TimeSpan Settle = TimeSpan.FromSeconds(1.5);

    /// <summary>What opens above the grid of the panel and closes again.</summary>
    private static readonly Dictionary<string, (Action<World> Open, Action<World> Close)> Layers =
        new(StringComparer.Ordinal)
        {
            ["quick-settings"] = (
                static w => w.Layers.QuickSettings.Open(),
                static w => w.Layers.QuickSettings.Close()
            ),
            ["tile-menu"] = (
                static w =>
                    w.Layers.Menu.Open(
                        new ShortcutId("w0"),
                        "Atajo 0",
                        "keyboard",
                        inFrequents: false
                    ),
                static w => w.Layers.Menu.Close()
            ),
            ["search"] = (
                static w => _ = w.Search.OpenAsync(SearchTrigger.Touch),
                static w => _ = w.Search.CloseAsync()
            ),
            ["edit-mode"] = (
                static w =>
                {
                    w.Layers.EditMode.Enter();
                    w.Panel.ApplyContext(
                        PanelBodyContext.Idle with
                        {
                            EditMode = true,
                            AddTile = true,
                        }
                    );
                },
                static w =>
                {
                    w.Layers.EditMode.Exit();
                    w.Panel.ApplyContext(PanelBodyContext.Idle);
                }
            ),
        };

    [DesktopTheory]
    [InlineData("quick-settings")]
    [InlineData("tile-menu")]
    [InlineData("search")]
    [InlineData("edit-mode")]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "CUA-001")]
    [Trait("Req", "PAN-006")]
    public void What_opens_above_the_grid_changes_the_panel_in_one_step_inside_the_work_area(
        string layer
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        var heights = new List<double>();
        var (window, world, theme) = WpfThread.Invoke(() =>
        {
            var theme = AuditLooks.Theme(AuditLook.Dark);
            var world = new World();
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                new DimSettings(AutoDim: false, Opacity: 1, DimTo: 1),
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            window.SizeChanged += (_, change) => heights.Add(change.NewSize.Height);
            window.Present();
            return (window, world, theme);
        });
        try
        {
            Wait();
            var before = WpfThread.Invoke(() => window.ActualHeight);
            var bottom = WpfThread.Invoke(() =>
                SystemParameters.WorkArea.Bottom - PanelSizes.Layout.PanelBottomMarginPx
            );
            var (open, close) = Layers[layer];

            heights.Clear();
            WpfThread.Invoke(() => open(world));
            Wait();
            var opened = WpfThread.Invoke(() => (window.Top, window.ActualHeight));
            var openSteps = Describe(heights);
            heights.Count.ShouldBeLessThanOrEqualTo(
                1,
                "the panel takes its new height at once: " + openSteps
            );
            (opened.Top + opened.ActualHeight).ShouldBeLessThanOrEqualTo(bottom + 1, openSteps);

            heights.Clear();
            WpfThread.Invoke(() => close(world));
            Wait();
            var closeSteps = Describe(heights);
            heights.Count.ShouldBeLessThanOrEqualTo(
                1,
                "the panel goes back at once: " + closeSteps
            );
            WpfThread.Invoke(() => window.ActualHeight).ShouldBe(before, 1, closeSteps);
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                window.Close();
                theme.Dispose();
            });
        }
    }

    [DesktopFact]
    [Trait("Req", "CUA-001")]
    [Trait("Req", "PAN-009")]
    public void With_one_page_opening_the_search_never_takes_a_row_of_the_grid_away()
    {
        // D21 review: with one page the pager row takes no room; the search keeps its place, and on a panel that just
        // fits the screen the grid lost a row as the search opened. The rows are counted as if the pager row were there.
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        var (window, world, theme) = WpfThread.Invoke(() =>
        {
            var theme = AuditLooks.Theme(AuditLook.Dark);
            var world = new World(shortcuts: 6);
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                new DimSettings(AutoDim: false, Opacity: 1, DimTo: 1),
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            window.Present();
            return (window, world, theme);
        });
        try
        {
            Wait();
            WpfThread.Invoke(() =>
            {
                world.Panel.Pager.PageCount.ShouldBe(1);

                // The work area ends right under the panel.
                window.WorkAreaBottom =
                    window.Top + window.ActualHeight + PanelSizes.Layout.PanelBottomMarginPx;
                window.InvalidateMeasure();
            });
            Wait();
            var rows = WpfThread.Invoke(() => world.Panel.Shape.Rows);

            _ = WpfThread.Invoke(() => world.Search.OpenAsync(SearchTrigger.Touch));
            Wait();
            WpfThread.Invoke(() => world.Panel.Shape.Rows).ShouldBe(rows, "the search opened");

            _ = WpfThread.Invoke(() => world.Search.CloseAsync());
            Wait();
            WpfThread.Invoke(() => world.Panel.Shape.Rows).ShouldBe(rows, "the search closed");
        }
        finally
        {
            WpfThread.Invoke(() =>
            {
                window.Close();
                theme.Dispose();
            });
        }
    }

    private static void Wait()
    {
        var until = DateTime.UtcNow + Settle;
        while (DateTime.UtcNow < until)
        {
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            Thread.Sleep(30);
        }
    }

    private static string Describe(List<double> heights)
    {
        var text = new StringBuilder("heights");
        foreach (var height in heights)
        {
            text.Append(CultureInfo.InvariantCulture, $" {height:0.#}");
        }

        return text.ToString();
    }

    /// <summary>The view models of a whole panel over one document, as the audit builds them.</summary>
    private sealed class World
    {
        public World(int shortcuts = 14)
        {
            var interactions = new InteractionsWorld("es");
            Panel = PanelBodyTestData.Panel(
                PanelBodyTestData.Library(shortcuts, 3),
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                "es"
            );
            Panel.ApplySession(new PanelSession(PanelPresence.Visible, PanelTestData.Word, 1));
            Header = new PanelHeaderViewModel(
                new ProfileViewCoordinator(interactions.Store),
                interactions.Localization,
                new PanelHeaderActions(
                    static () => { },
                    static () => { },
                    static () => { },
                    static () => { }
                )
            );
            Header.Apply(
                PanelHeaderProjection.Project(
                    new ProfileState(new ViewTarget.Profile(SearchTestWorld.Word), false, null),
                    interactions.Store.Current.Library,
                    SearchTestWorld.Word,
                    searchingWithText: false,
                    LangCode.Es,
                    LangCode.Es
                )
            );
            Search = SearchSurfaceTests.Search();
            Suggestion = SearchSurfaceTests.Suggestion();
            Layers = new PanelLayerModels(
                interactions.QuickSettings,
                interactions.EditMode,
                interactions.Menu,
                interactions.TestMode,
                interactions.Modes,
                static () => false
            );
        }

        public PanelViewModel Panel { get; }

        public PanelHeaderViewModel Header { get; }

        public Clicalo.Presentation.Panel.Search.SearchViewModel Search { get; }

        public Clicalo.Presentation.Panel.Search.SuggestionViewModel Suggestion { get; }

        public PanelLayerModels Layers { get; }
    }
}
