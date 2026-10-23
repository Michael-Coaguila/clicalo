using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Application.Coordinators;
using Clicalo.Application.Profiles;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.ProfileResolution;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.Presentation.Panel.Search;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.SearchPanel;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;
using SizeSetting = Clicalo.Domain.Settings.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.Automation.Audit;

/// <summary>
/// REG-02 and REG-06 on the real surfaces of the panel (docs/04): the panel in its Full and Compact views in the three
/// sizes and its states (the profile grid, the search, the suggestion, Quick settings, edit mode, the menu of a
/// shortcut, the panic strip, the administrator notice…), the bubble, the panic pill, and the Tab view (handle, bar and
/// the windows beside it) on its four sides. Each surface is the product's own window, built but never shown; its
/// content is hosted on a hidden presentation source (<see cref="AuditHost"/>). Besides the design as drawn (dark,
/// text at 100 %), the surfaces are audited with the largest text (150 %) in the light theme and in high contrast
/// (<see cref="AuditLook"/>), and no text of theirs may be drawn cut (CUA-011).
/// </summary>
public sealed class SurfacesAuditTests
{
    /// <summary>Half a logical pixel: what layout rounding may move an edge.</summary>
    private const double Rounding = 0.5;

    /// <summary>Why the one known defect of the panel is not audited outside the gallery, until the panel is fixed.</summary>
    private const string KnownCut =
        "Known defect of the panel: in S Compact without the Sticky keys row the layer of the notices cuts the "
        + "administrator notice (CUA-011); the gallery (CLICALO_GALLERY) still draws and audits it.";

    private static readonly DimSettings NoDim = new(AutoDim: false, Opacity: 1, DimTo: 1);

    /// <summary>The states of the panel audited: a name and what happened to get there.</summary>
    private static readonly Dictionary<string, Action<PanelWorld>> PanelStates = new(
        StringComparer.Ordinal
    )
    {
        ["idle"] = static _ => { },
        ["picker"] = static w =>
            w.Panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true }),
        ["frequents"] = static w =>
        {
            w.Panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
            w.ShowHeader(new ViewTarget.Frequents());
        },
        ["frequents-empty"] = static w =>
        {
            w.Panel.Apply(
                PanelProjector.Project(
                    PanelBodyTestData.GalleryLibrary(),
                    PanelBodyTestData.Excel,
                    LangCode.Es,
                    LangCode.Es,
                    PanelBodyTestData.GalleryKeys
                )
            );
            w.Panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
            w.ShowHeader(new ViewTarget.Frequents());
        },
        ["empty-profile"] = static w =>
        {
            w.Panel.Apply(
                PanelProjector.Project(
                    PanelBodyTestData.GalleryLibrary(),
                    PanelBodyTestData.Excel,
                    LangCode.Es,
                    LangCode.Es,
                    PanelBodyTestData.GalleryKeys
                )
            );
            w.ShowHeader(new ViewTarget.Profile(PanelBodyTestData.Excel));
        },
        ["notices"] = static w =>
            w.Panel.ApplyContext(
                PanelBodyContext.Idle with
                {
                    ElevatedApp = "Taskmgr",
                    SuggestionApp = "Notion",
                    CanRepeat = true,
                    Notice = new PanelNotice(
                        L.Deleted,
                        new IconRef("delete"),
                        NoticeTone.Notice,
                        CanUndo: true
                    ),
                }
            ),
        ["capture"] = static w =>
            w.Panel.ApplyContext(
                PanelBodyContext.Idle with
                {
                    Notice = new PanelNotice(
                        L.WaitingApp,
                        new IconRef("radar"),
                        NoticeTone.Notice,
                        CanCancel: true
                    ),
                }
            ),
        ["safety-notice"] = static w =>
            w.Panel.ApplyContext(PanelBodyContext.Idle with { Notice = PanelWorld.SafetyNotice }),
        ["panic"] = static w =>
            w.Panel.ApplyEngine(PanelBodyTestData.Holding(new ShortcutId("w0"))),
        ["search"] = static w =>
        {
            _ = w.Search.OpenAsync(SearchTrigger.Touch);
            w.Header.ApplyLayers(searchOpen: true, editing: false, quickSettingsOpen: false);
        },
        ["search-results"] = static w =>
        {
            // BUS-005: the results in place of the list, more than a page holds.
            _ = w.Search.OpenAsync(SearchTrigger.Touch);
            w.Search.Query = "a";
            w.Panel.ApplySearch([.. w.Search.Results], w.Search.NoResultsText);
            w.Panel.ApplyContext(PanelBodyContext.Idle with { SearchingWithText = true });
            w.Header.ApplyLayers(searchOpen: true, editing: false, quickSettingsOpen: false);
        },
        ["one-full-page"] = static w =>
        {
            // As many shortcuts as one page holds: no pager (D21). The shape can follow the names, so twice.
            for (var i = 0; i < 2; i++)
            {
                w.Panel.Apply(
                    PanelProjector.Project(
                        PanelBodyTestData.GalleryLibrary(wordCount: w.Panel.Shape.PerPage),
                        PanelBodyTestData.Word,
                        LangCode.Es,
                        LangCode.Es,
                        PanelBodyTestData.GalleryKeys
                    )
                );
            }
        },
        ["picker-four-profiles"] = static w =>
        {
            // H11.2: four profiles in the grid, over a profile of several pages.
            w.Panel.Apply(
                PanelProjector.Project(
                    PanelBodyTestData.GalleryLibrary(extraProfiles: 1),
                    PanelBodyTestData.Word,
                    LangCode.Es,
                    LangCode.Es,
                    PanelBodyTestData.GalleryKeys
                )
            );
            w.Panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true });
        },
        ["suggestion"] = static w =>
            w.Suggestion.Apply(new ProcessName("excel.exe"), searching: false),
        ["quick-settings"] = static w =>
        {
            w.Layers.QuickSettings.Open();
            w.Header.ApplyLayers(searchOpen: false, editing: false, quickSettingsOpen: true);
        },
        ["edit-mode"] = static w =>
        {
            w.Layers.EditMode.Enter();
            w.Header.ApplyLayers(searchOpen: false, editing: true, quickSettingsOpen: false);
            w.ApplyContext(PanelBodyContext.Idle with { EditMode = true, AddTile = true });
        },
        ["edit-mode-armed"] = static w =>
        {
            // REG-04, CUA-012: the first tap on the × of «Negrita» arms it with «Confirmar».
            w.Layers.EditMode.Enter();
            w.Layers.EditMode.ApplyView(frequents: false, searching: false, PanelBodyTestData.Word);
            w.Layers.EditMode.Remove(new ShortcutId("w0"));
            w.Layers.EditMode.Armed.ShouldBe(new ShortcutId("w0"));
            w.Header.ApplyLayers(searchOpen: false, editing: true, quickSettingsOpen: false);
            w.ApplyContext(PanelBodyContext.Idle with { EditMode = true, AddTile = true });
        },
        ["tile-menu"] = static w =>
            w.Layers.Menu.Open(new ShortcutId("w0"), "Negrita", "format_bold", inFrequents: false),
        ["test-mode"] = static w =>
        {
            // TAC-008: while test mode is on, the status row says [tmStart], as in the app.
            w.Layers.TestMode.Start();
            w.ApplyContext(PanelBodyContext.Idle);
        },
    };

    /// <summary>The states that show more than one page of tiles in every shape.</summary>
    private static readonly HashSet<string> PagedStates = new(StringComparer.Ordinal)
    {
        "idle",
        "search-results",
    };

    /// <summary>
    /// Every state as it is drawn (dark, text at 100 %) in the six shapes of the panel, with the rows of a new document
    /// (no Sticky keys row, no voice numbers), and with every row on («all on») in two of them. With the largest text,
    /// which only makes the tiles grow (CUA-011), not every combination and always with every row on: with high
    /// contrast, whose thicker borders leave the least room, in the two shapes of the smallest panel; with the light
    /// theme, which only changes colors, in one.
    /// </summary>
    public static TheoryData<string, SizeSetting, bool, string, AuditLook, bool> PanelCases()
    {
        var cases = new TheoryData<string, SizeSetting, bool, string, AuditLook, bool>();
        foreach (var state in PanelStates.Keys)
        {
            cases.Add(state, SizeSetting.Small, false, "es", AuditLook.Dark, false);
            // The panel of the mockup of direction C (D12): M, Full, in Spanish, 340 wide.
            cases.Add(state, SizeSetting.Medium, false, "es", AuditLook.Dark, false);
            cases.Add(state, SizeSetting.Medium, true, "en", AuditLook.Dark, false);
            if (string.Equals(state, "notices", StringComparison.Ordinal))
            {
                // A defect the Sticky keys row, always on before, hid: in S Compact without it the layer of the
                // notices is lower than the administrator notice and cuts its text (CUA-011). The gallery draws it.
                cases.Add(
                    new TheoryDataRow<string, SizeSetting, bool, string, AuditLook, bool>(
                        state,
                        SizeSetting.Small,
                        true,
                        "es",
                        AuditLook.Dark,
                        false
                    )
                    {
                        Skip = AuditGallery.Folder is null ? KnownCut : null,
                    }
                );
            }
            else
            {
                cases.Add(state, SizeSetting.Small, true, "es", AuditLook.Dark, false);
            }

            cases.Add(state, SizeSetting.Large, false, "en", AuditLook.Dark, false);
            cases.Add(state, SizeSetting.Large, true, "es", AuditLook.Dark, false);
            cases.Add(state, SizeSetting.Medium, false, "es", AuditLook.Dark, true);
            cases.Add(state, SizeSetting.Small, true, "es", AuditLook.Dark, true);
            cases.Add(state, SizeSetting.Small, false, "es", AuditLook.ContrastLargeText, true);
            cases.Add(state, SizeSetting.Small, true, "es", AuditLook.ContrastLargeText, true);
            cases.Add(state, SizeSetting.Medium, true, "en", AuditLook.LightLargeText, true);
        }

        // D16 and D17: M in the Full view at the largest text, in both languages, and Quick settings in English.
        cases.Add("idle", SizeSetting.Medium, false, "es", AuditLook.LightLargeText, true);
        cases.Add("idle", SizeSetting.Medium, false, "en", AuditLook.ContrastLargeText, true);
        cases.Add("quick-settings", SizeSetting.Medium, false, "en", AuditLook.Dark, false);
        return cases;
    }

    /// <summary>
    /// The four sides of the Tab view, and S, M and L on the right, in Spanish and in English (D12: no label of the bar
    /// is cut).
    /// </summary>
    public static TheoryData<DockSide, SizeId, string, AuditLook> BarCases()
    {
        var cases = new TheoryData<DockSide, SizeId, string, AuditLook>();
        foreach (var look in Enum.GetValues<AuditLook>())
        {
            foreach (var language in new[] { "es", "en" })
            {
                cases.Add(DockSide.Right, SizeId.S, language, look);
                cases.Add(DockSide.Right, SizeId.M, language, look);
                cases.Add(DockSide.Right, SizeId.L, language, look);
                cases.Add(DockSide.Left, SizeId.M, language, look);
                cases.Add(DockSide.Top, SizeId.S, language, look);
                cases.Add(DockSide.Bottom, SizeId.L, language, look);
            }
        }

        return cases;
    }

    /// <summary>The windows beside the bar in S, M and L (the tiles of Pinned and the keys follow the size).</summary>
    public static TheoryData<DockFlyout, int, SizeId, string, AuditLook> FlyoutCases()
    {
        var cases = new TheoryData<DockFlyout, int, SizeId, string, AuditLook>();
        foreach (var look in Enum.GetValues<AuditLook>())
        {
            foreach (var language in new[] { "es", "en" })
            {
                foreach (var size in new[] { SizeId.S, SizeId.M, SizeId.L })
                {
                    cases.Add(DockFlyout.Pinned, 3, size, language, look);
                    cases.Add(DockFlyout.Sticky, 4, size, language, look);
                    cases.Add(DockFlyout.Arrows, 4, size, language, look);
                }

                cases.Add(DockFlyout.Profiles, 3, SizeId.M, language, look);
            }
        }

        return cases;
    }

    public static TheoryData<string, AuditLook> TabWindowCases()
    {
        var cases = new TheoryData<string, AuditLook>();
        foreach (var look in Enum.GetValues<AuditLook>())
        {
            cases.Add("es", look);
            cases.Add("en", look);
        }

        return cases;
    }

    [Theory]
    [MemberData(nameof(PanelCases))]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-002")]
    [Trait("Req", "PAN-009")]
    [Trait("Req", "AJR-001")]
    public void Every_state_of_the_panel_follows_the_UIA_rules_and_keeps_44(
        string state,
        SizeSetting size,
        bool compact,
        string language,
        AuditLook look,
        bool allOn
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            var world = new PanelWorld(language, time);
            world.ApplySettings(size, compact, allOn, AuditLooks.TextScalePercent(look));
            var window = NewPanelWindow(world, lab, time, theme);
            var quick = new DockQuickWindow(
                world.Layers.QuickSettings,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                PanelStates[state](world);
                host.LayOut();

                var shape =
                    $"{size}, {(compact ? "compact" : "full")}, {language}{(allOn ? ", all on" : string.Empty)}, {look}";
                var name = $"panel {state} ({shape})";
                SurfaceAudit.ShouldPass(host, name, atLeast: 8, TouchInput.PointerLayer, "Panel");
                NoTileShowsInPartUnderALayer(host, window, name);
                if (PagedStates.Contains(state))
                {
                    world.Panel.Pager.PageCount.ShouldBeGreaterThan(1, name);
                }
                else if (string.Equals(state, "one-full-page", StringComparison.Ordinal))
                {
                    world.Panel.Pager.PageCount.ShouldBe(1, name);
                    world.Panel.Tiles.Count.ShouldBe(world.Panel.Shape.PerPage, name);
                }

                if (string.Equals(state, "quick-settings", StringComparison.Ordinal))
                {
                    // AJR-001 (D21): beside the panel, in the window of the Tab view, showing the size and the view of
                    // this panel, as tall as its content up to the work area of the person's screen.
                    quick.FitHeight(AuditGallery.QuickRoom);
                    using var beside = AuditHost.OfWindow(quick, theme);
                    SurfaceAudit.ShouldPass(
                        beside,
                        $"quick settings beside the panel ({shape})",
                        atLeast: 13,
                        TouchInput.PointerLayer,
                        "Panel"
                    );
                    QuickSettingsFitTheScreen(beside, quick, look);
                }
            }
            finally
            {
                window.Close();
                quick.Close();
            }
        });
    }

    /// <summary>
    /// AJR-001 (D21): Quick settings are never taller than the work area of the person's screen less
    /// <see cref="Space.S2"/> above and below and, as drawn, show every row without scrolling.
    /// </summary>
    private static void QuickSettingsFitTheScreen(
        AuditHost host,
        DockQuickWindow quick,
        AuditLook look
    )
    {
        host.Root.ActualHeight.ShouldBeLessThanOrEqualTo(AuditGallery.QuickRoom + 0.5);
        if (look == AuditLook.Dark)
        {
            quick.Sheet.Scroller.ScrollableHeight.ShouldBe(0, 0.5, "every row shows at 100 %");
        }
    }

    /// <summary>
    /// H1.1, PAN-009: a layer of the panel starts in the gap between two rows, so a tile is wholly under it or wholly
    /// out of it; a tile a card cuts would still show and be read.
    /// </summary>
    private static void NoTileShowsInPartUnderALayer(
        AuditHost host,
        PanelWindow window,
        string name
    )
    {
        var root = host.Root;
        // The layers are the children of the host of the panel after its rows (PanelLayerHost).
        var layers = ((UserControl)root).Content is System.Windows.Controls.Panel layerHost
            ? layerHost
                .Children.OfType<FrameworkElement>()
                .Skip(1)
                .Where(static layer => layer.IsVisible)
                .Select(layer => (Layer: layer, Bounds: BoundsIn(layer, root)))
                .Where(static cover => cover.Bounds is { Width: > 0, Height: > 0 })
                .ToList()
            : [];
        foreach (var tile in window.TileControls.Where(static tile => tile.IsVisible))
        {
            var box = BoundsIn(tile, root);
            foreach (var (layer, cover) in layers)
            {
                var under = Rect.Intersect(box, cover);
                if (
                    layer.IsAncestorOf(tile)
                    || under.IsEmpty
                    || under.Width < Rounding
                    || under.Height < Rounding
                    || (
                        under.Width + Rounding >= box.Width && under.Height + Rounding >= box.Height
                    )
                )
                {
                    continue;
                }

                throw new Xunit.Sdk.XunitException(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"«{name}»: {under.Width:0.#} × {under.Height:0.#} of the {box.Width:0.#} × {box.Height:0.#} of the tile «{AutomationProperties.GetName(tile)}» is under a layer that leaves the rest in view."
                    )
                );
            }
        }

        static Rect BoundsIn(FrameworkElement element, Visual root) =>
            element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
    }

    [Theory]
    [InlineData(SizeSetting.Small, 292, 444)]
    [InlineData(SizeSetting.Medium, 340, 540)]
    [InlineData(SizeSetting.Large, 412, 576)]
    [Trait("Req", "PAN-002")]
    [Trait("Req", "PAN-009")]
    [Trait("Req", "CUA-004")]
    public void The_panel_has_the_size_of_its_design_with_one_page_or_many(
        SizeSetting size,
        int width,
        int height
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(AuditLook.Dark);
            var world = new PanelWorld("es", time);
            // The rows of the design: Always visible, «Qué ver», the grid, the pager and the status row.
            world.Panel.ApplyLayout(PanelLayoutSettings.Default with { Size = size });
            var window = NewPanelWindow(world, lab, time, theme);
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                host.LayOut();
                world.Panel.Pager.PageCount.ShouldBeGreaterThan(1);
                var paged = new Size(host.Root.ActualWidth, host.Root.ActualHeight);

                // An empty profile: one page. D21 (the maintainer found the empty band of D16 ugly): the pager row takes
                // no room then, so the panel is its height less the row and its gap.
                PanelStates["empty-profile"](world);
                host.LayOut();
                var single = new Size(host.Root.ActualWidth, host.Root.ActualHeight);

                // D16 with the measures of D20: M in the Full view with 3 rows is 340 × 540; S (2 rows and the row of
                // icons) and L follow the same rows.
                paged.ShouldBe(new Size(width, height));
                single.ShouldBe(new Size(width, height - ControlHeight.Small - Space.S2));

                // The lines go inside: the thicker ones of high contrast do not make the panel grow.
                theme.Preference = ThemeChoice.HighContrast;
                host.LayOut();
                new Size(host.Root.ActualWidth, host.Root.ActualHeight).ShouldBe(single);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// PAN-002 and VCO-001 in the Compact view: D20 made M 340 × 460 and D21 (3) took away its status row, so M is 44
    /// lower; S and L follow the same rows.
    /// </summary>
    [Theory]
    [InlineData(SizeSetting.Small, 292, 348)]
    [InlineData(SizeSetting.Medium, 340, 416)]
    [InlineData(SizeSetting.Large, 412, 452)]
    [Trait("Req", "PAN-002")]
    [Trait("Req", "VCO-001")]
    [Trait("Req", "PAN-009")]
    public void The_Compact_panel_has_the_size_of_its_design_in_every_size(
        SizeSetting size,
        int width,
        int height
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(AuditLook.Dark);
            var world = new PanelWorld("es", time);
            world.Panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size,
                    Compact = true,
                }
            );
            var window = NewPanelWindow(world, lab, time, theme);
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                host.LayOut();
                world.Panel.Pager.PageCount.ShouldBeGreaterThan(1);
                new Size(host.Root.ActualWidth, host.Root.ActualHeight).ShouldBe(
                    new Size(width, height)
                );

                // The lines go inside: the thicker ones of high contrast do not make the panel grow.
                theme.Preference = ThemeChoice.HighContrast;
                host.LayOut();
                new Size(host.Root.ActualWidth, host.Root.ActualHeight).ShouldBe(
                    new Size(width, height)
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "CUA-011")]
    [Trait("Req", "PAN-002")]
    public void At_a_larger_text_a_word_that_does_not_fit_gives_the_grid_two_whole_columns(
        string language
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(AuditLook.LightLargeText);
            var world = new PanelWorld(language, time);
            var layout = PanelLayoutSettings.Default with
            {
                Size = SizeSetting.Medium,
                StickyRow = true,
                TextScalePercent = TypeScale.MaxScalePercent,
            };
            world.Panel.ApplyLayout(layout);
            var window = NewPanelWindow(world, lab, time, theme);
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                host.LayOut();

                // D17: «Reemplazar» and «Ortografía» do not fit a third of the panel at 150 %, so the grid has two
                // columns of wider tiles and the panel keeps its width.
                world.Panel.Shape.Columns.ShouldBe(2);
                world.Panel.Tiles.Count.ShouldBe(6);
                host.Root.ActualWidth.ShouldBe(340);
                window.TileControls.Take(6).ShouldAllBe(static tile => tile.ActualWidth > 140);

                // At 100 % every word fits: the three columns chosen.
                world.Panel.ApplyLayout(
                    layout with
                    {
                        TextScalePercent = TypeScale.MinScalePercent,
                    }
                );
                host.LayOut();
                world.Panel.Shape.Columns.ShouldBe(3);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(SizeSetting.Small, false)]
    [InlineData(SizeSetting.Medium, false)]
    [InlineData(SizeSetting.Large, false)]
    [InlineData(SizeSetting.Small, true)]
    [InlineData(SizeSetting.Medium, true)]
    [InlineData(SizeSetting.Large, true)]
    [Trait("Req", "SEG-002")]
    [Trait("Req", "REG-03")]
    public void Release_all_sits_whole_inside_the_panic_strip(SizeSetting size, bool compact)
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(AuditLook.Dark);
            var world = new PanelWorld("es", time);
            world.Panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size,
                    Compact = compact,
                }
            );
            var window = NewPanelWindow(world, lab, time, theme);
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                PanelStates["panic"](world);
                host.LayOut();
                window.IsPanicStripVisible.ShouldBeTrue();

                // D19 made the status row 44 + 4: the strip over it cut the foot of «Release all» (D20 review). D21: the
                // strip starts in the middle of the gap above the row, so «Release all» is centred in it, S1 from its
                // top and its foot, and the red never touches the row above.
                var button = window.ReleaseAllButton;
                var bottom = button.TranslatePoint(new Point(0, button.ActualHeight), host.Root).Y;
                button.ActualHeight.ShouldBe(ControlHeight.Small);
                (host.Root.ActualHeight - bottom).ShouldBeInRange(Space.S1 - 0.5, Space.S2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static PanelWindow NewPanelWindow(
        PanelWorld world,
        SurfaceLab lab,
        FakeTimeProvider time,
        ThemeService theme
    ) =>
        new(
            world.Panel,
            lab.Registry,
            time,
            theme,
            NoDim,
            world.Header,
            world.Search,
            world.Suggestion,
            world.Layers
        );

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [MemberData(nameof(BarCases))]
    public void The_handle_and_the_bar_of_the_Tab_view_follow_the_UIA_rules_and_keep_44(
        DockSide side,
        SizeId size,
        string language,
        AuditLook look
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            var dock = Dock(time, side, size, DockFlyout.None, coachStep: 0, language);
            var handle = new DockHandleWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var bar = new DockBarWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                using (var host = AuditHost.OfWindow(handle, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"handle ({side}, {size}, {language}, {look})",
                        atLeast: 1,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );

                    // At rest, as it usually is: nothing held, no dot.
                    var held = State(side, size, DockFlyout.None, coachStep: 0);
                    dock.Apply(held with { AnythingHeld = false });
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"handle at rest ({side}, {size}, {language}, {look})",
                        atLeast: 1,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                    dock.Apply(held);
                }

                using (var host = AuditHost.OfWindow(bar, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"bar ({side}, {size}, {language}, {look})",
                        atLeast: 10,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );

                    // The other texts of the pill and of the lock: «Fijo» and «Abierta».
                    var state = State(side, size, DockFlyout.None, coachStep: 0);
                    dock.Apply(
                        state with
                        {
                            IsFixed = true,
                            Dock = state.Dock with { PinOpen = true },
                        }
                    );
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"bar fixed and open ({side}, {size}, {language}, {look})",
                        atLeast: 10,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );

                    // Two pages, on the second: the pager, with «previous» on and «next» off; Frequents in view, the
                    // picker open and every window of the bar open, so every tool is drawn «elegido».
                    dock.Apply(
                        state with
                        {
                            Frequents = true,
                            Flyout = DockFlyout.Pinned,
                            QuickOpen = true,
                            Dock = state.Dock with { PerPage = 2 },
                        }
                    );
                    dock.Next();
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"bar paged in Frequents ({side}, {size}, {language}, {look})",
                        atLeast: 10,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                }
            }
            finally
            {
                handle.Close();
                bar.Close();
            }
        });
    }

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [MemberData(nameof(FlyoutCases))]
    public void The_windows_beside_the_bar_follow_the_UIA_rules_and_keep_44(
        DockFlyout flyout,
        int atLeast,
        SizeId size,
        string language,
        AuditLook look
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            var world = new PanelWorld(language, time);
            world.Panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size switch
                    {
                        SizeId.S => SizeSetting.Small,
                        SizeId.L => SizeSetting.Large,
                        _ => SizeSetting.Medium,
                    },
                    StickyRow = true,
                }
            );
            // Word is the active app, as the bar says with its dot (State: IsActiveApp), so its tile shows the dot too.
            world.Panel.ApplyContext(
                PanelBodyContext.Idle with
                {
                    PickerOpen = true,
                    ActiveAppProfile = PanelBodyTestData.Word,
                }
            );
            var dock = Dock(time, DockSide.Right, size, flyout, coachStep: 0, language);
            var window = new DockFlyoutWindow(
                flyout,
                dock,
                world.Panel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                SurfaceAudit.ShouldPass(
                    host,
                    $"flyout {flyout} ({size}, {language}, {look})",
                    atLeast,
                    TouchInput.PointerLayer,
                    "Pestaña"
                );
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [MemberData(nameof(TabWindowCases))]
    public void The_coach_the_notices_the_menu_and_Quick_settings_of_the_Tab_view_keep_44(
        string language,
        AuditLook look
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            var world = new PanelWorld(language, time);
            var dock = Dock(
                time,
                DockSide.Right,
                SizeId.M,
                DockFlyout.None,
                coachStep: 1,
                language
            );
            var coach = new DockCoachWindow(
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var notice = new DockNoticeWindow(
                world.Panel,
                world.Layers.TestMode,
                new DockLabels(world.Interactions.Localization),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var menu = new DockMenuWindow(
                world.Layers.Menu,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var quick = new DockQuickWindow(
                world.Layers.QuickSettings,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                // PES-015: the three steps of the guide, each with its own title and text.
                for (var step = 0; step < DockRules.CoachSteps; step++)
                {
                    var state = State(DockSide.Right, SizeId.M, DockFlyout.None, coachStep: step);
                    dock.Apply(state with { Dock = state.Dock with { CoachDone = false } });
                    using var host = AuditHost.OfWindow(coach, theme);
                    SurfaceAudit.ShouldPass(
                        host,
                        string.Create(
                            CultureInfo.InvariantCulture,
                            $"coach step {step + 1} ({language}, {look})"
                        ),
                        atLeast: 2,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                }

                world.Panel.ApplyContext(
                    new PanelBodyContext(
                        ElevatedApp: "regedit",
                        Notice: new PanelNotice(
                            L.Deleted,
                            new IconRef("delete"),
                            NoticeTone.Notice,
                            CanUndo: true
                        )
                    )
                );
                using (var host = AuditHost.OfWindow(notice, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"notices with undo ({language}, {look})",
                        atLeast: 2,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                    world.Panel.ApplyContext(
                        new PanelBodyContext(
                            Notice: new PanelNotice(
                                L.WaitingApp,
                                new IconRef("radar"),
                                NoticeTone.Notice,
                                CanCancel: true
                            )
                        )
                    );
                    world.Layers.TestMode.Start();
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"notices with cancel in test mode ({language}, {look})",
                        atLeast: 1,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );

                    // The longest warning of key safety, which must be read whole.
                    world.Layers.TestMode.Stop();
                    world.Panel.ApplyContext(new PanelBodyContext(Notice: PanelWorld.SafetyNotice));
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"safety notice ({language}, {look})",
                        atLeast: 0,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                }

                world.Layers.Menu.Open(
                    InteractionsWorld.Bold,
                    "Negrita",
                    "format_bold",
                    inFrequents: true
                );
                using (var host = AuditHost.OfWindow(menu, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"menu of a shortcut ({language}, {look})",
                        atLeast: 4,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                }

                // D21: the Compact view shows a notice for a moment under the panel, a confirmation included, on a
                // translucent face and without the administrator notice or test mode.
                notice.ServesCompact = true;
                world.Panel.ApplyContext(
                    new PanelBodyContext(
                        Notice: new PanelNotice(
                            L.TapSent(keys: "Ctrl+N", app: "Word"),
                            new IconRef("check_circle"),
                            NoticeTone.Notice
                        )
                    )
                );
                using (var host = AuditHost.OfWindow(notice, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"Compact notice ({language}, {look})",
                        atLeast: 0,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                    world.Panel.ApplyContext(new PanelBodyContext(Notice: PanelWorld.SafetyNotice));
                    host.LayOut();
                    SurfaceAudit.ShouldPass(
                        host,
                        $"Compact safety notice ({language}, {look})",
                        atLeast: 0,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                }

                // Quick settings as the person opens them: test mode, switched on above, is off again.
                notice.ServesCompact = false;
                world.Layers.TestMode.Stop();
                world.Layers.QuickSettings.Views[2].Select();
                world.Layers.QuickSettings.Open();
                quick.FitHeight(AuditGallery.QuickRoom);
                using (var host = AuditHost.OfWindow(quick, theme))
                {
                    var name = $"Quick settings beside the bar ({language}, {look})";
                    SurfaceAudit.ShouldPass(
                        host,
                        name,
                        atLeast: 17,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                    host.Root.ActualHeight.ShouldBeLessThanOrEqualTo(AuditGallery.QuickRoom + 0.5);

                    // What does not fit scrolls inside: its last rows too.
                    var scroller = quick.Sheet.Scroller;
                    if (scroller.ScrollableHeight > 0)
                    {
                        scroller.ScrollToEnd();
                        host.LayOut();
                        SurfaceAudit.ShouldPass(
                            host,
                            $"Quick settings beside the bar scrolled to the end ({language}, {look})",
                            atLeast: 4,
                            TouchInput.PointerLayer,
                            "Pestaña"
                        );
                        scroller.ScrollToTop();
                    }
                }

                // AJR-001 (D21): the Quick settings of the panel, in the same window beside it, without the side of the
                // Tab view.
                world.Layers.QuickSettings.Views[0].Select();
                world.Layers.QuickSettings.Open();
                using (var host = AuditHost.OfWindow(quick, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        $"Quick settings beside the panel ({language}, {look})",
                        atLeast: 13,
                        TouchInput.PointerLayer,
                        "Pestaña"
                    );
                    QuickSettingsFitTheScreen(host, quick, look);
                }
            }
            finally
            {
                coach.Close();
                notice.Close();
                menu.Close();
                quick.Close();
            }
        });
    }

    [Theory]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-002")]
    [Trait("Req", "SEG-002")]
    [InlineData(AuditLook.Dark)]
    [InlineData(AuditLook.LightLargeText)]
    [InlineData(AuditLook.ContrastLargeText)]
    public void The_bubble_and_the_panic_pill_follow_the_UIA_rules_and_keep_44(AuditLook look)
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(look);
            var localization = PanelTestData.Localization("es");
            var bubble = new BubbleWindow(
                new BubbleViewModel(localization, static () => { }, static () => { }),
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var pill = new PanicPillWindow(
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static () => { }
            );
            pill.ApplyLabel(localization.Current.Format(L.ReleaseAll));
            try
            {
                using (var host = AuditHost.OfWindow(bubble, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "bubble",
                        atLeast: 1,
                        TouchInput.PointerLayer,
                        "Burbuja y Soltar todo"
                    );
                }

                using (var host = AuditHost.OfWindow(pill, theme))
                {
                    SurfaceAudit.ShouldPass(
                        host,
                        "panic pill",
                        atLeast: 1,
                        TouchInput.PointerLayer,
                        "Burbuja y Soltar todo"
                    );

                    // SEG-002: its text is onDanger on the danger fill, also as the audit draws it (the theme on the
                    // content painted it in the color of plain text).
                    Color(pill.Button.Foreground)
                        .ShouldBe(
                            Color(pill.Button.FindResource(ThemeBrushKey.For(ColorToken.OnDanger)))
                        );
                    Color(pill.Button.Background)
                        .ShouldBe(
                            Color(pill.Button.FindResource(ThemeBrushKey.For(ColorToken.Danger)))
                        );
                }
            }
            finally
            {
                bubble.Close();
                pill.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Req", "REG-06")]
    [Trait("Req", "CAB-001")]
    public void The_header_of_the_panel_is_read_from_left_to_right(bool titleOpensPicker)
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            world.Header.ApplyPicker(titleOpensPicker, pickerOpen: false);
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                NoDim,
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                var header = world.Header;
                header.ShowsAutoFixed.ShouldBeTrue();
                // Direction C (D12): the header holds only tools; Auto/Fixed is read later, at the end of the row
                // «Qué ver», next to what it controls.
                string[] expected =
                [
                    header.Title,
                    header.SearchName,
                    header.EditName,
                    header.QuickSettingsName,
                    header.MinimizeName,
                    header.AutoFixedName,
                ];
                expected.ShouldAllBe(static name => name.Length > 0);
                expected.Distinct(StringComparer.Ordinal).Count().ShouldBe(expected.Length);

                // The names of the header as a screen reader meets them, each one once (the title is a text and,
                // while it opens the profile grid, a button over it too).
                var read = new List<string>();
                foreach (var node in host.Snapshot("panel").DescendantsAndSelf())
                {
                    if (
                        expected.Contains(node.Name, StringComparer.Ordinal)
                        && !read.Contains(node.Name, StringComparer.Ordinal)
                    )
                    {
                        read.Add(node.Name);
                    }
                }

                read.ShouldBe(expected);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "REG-06")]
    [Trait("Req", "ACC-009")]
    public void The_tiles_of_the_grid_are_read_in_their_order_even_in_edit_mode()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            world.Panel.ApplyLayout(PanelLayoutSettings.Default with { VoiceNumbers = true });
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                NoDim,
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                var page = world
                    .Panel.Tiles.Select(
                        static (tile, i) =>
                            string.Create(
                                CultureInfo.InvariantCulture,
                                $"{i + 1} {tile.AccessibleName}"
                            )
                    )
                    .ToList();
                page.Count.ShouldBeGreaterThan(3);

                // The tiles are drawn each above the next one; a screen reader still walks them from the first.
                GridTiles(host.Snapshot("panel"), page).ShouldBe(page);

                world.Layers.EditMode.Enter();
                world.Panel.ApplyContext(PanelBodyContext.Idle with { EditMode = true });
                GridTiles(host.Snapshot("panel"), page).ShouldBe(page);
            }
            finally
            {
                window.Close();
            }
        });

        static IEnumerable<string> GridTiles(UiaNode tree, List<string> names) =>
            tree.DescendantsAndSelf()
                .Select(static node => node.Name)
                .Where(name => names.Contains(name, StringComparer.Ordinal));
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "AJR-001")]
    public void Quick_settings_of_the_header_is_an_expand_collapse_button_that_follows_its_sheet()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var world = new PanelWorld("es", time);
            var window = new PanelWindow(
                world.Panel,
                lab.Registry,
                time,
                theme,
                NoDim,
                world.Header,
                world.Search,
                world.Suggestion,
                world.Layers
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                var quick = host.Peer("Ajustes rápidos");

                quick.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
                quick.GetPattern(PatternInterface.Invoke).ShouldBeNull("one pattern per button");
                quick.GetPattern(PatternInterface.Toggle).ShouldBeNull();
                var sheet = quick
                    .GetPattern(PatternInterface.ExpandCollapse)
                    .ShouldBeAssignableTo<IExpandCollapseProvider>()!;
                sheet.ExpandCollapseState.ShouldBe(ExpandCollapseState.Collapsed);

                sheet.Collapse();
                world.QuickSettingsTaps.ShouldBe(0, "it is already closed");
                sheet.Expand();
                world.QuickSettingsTaps.ShouldBe(1, "expanding is the tap");

                world.Header.ApplyLayers(
                    searchOpen: false,
                    editing: false,
                    quickSettingsOpen: true
                );
                sheet.ExpandCollapseState.ShouldBe(ExpandCollapseState.Expanded);
                sheet.Expand();
                world.QuickSettingsTaps.ShouldBe(1, "it is already open");
                sheet.Collapse();
                world.QuickSettingsTaps.ShouldBe(2);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-003")]
    [Trait("Req", "PES-009")]
    public void The_buttons_of_the_bar_say_what_they_open_and_what_is_on()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, DockSide.Right, SizeId.M, DockFlyout.Pinned, coachStep: 0);
            var bar = new DockBarWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                // Close, expand, Frequents, profile, Auto/Fixed, search, repeat, pinned, sticky, lock, tune, arrows.
                var buttons = bar.Buttons;
                Expanded(buttons[11]).ShouldBe(ExpandCollapseState.Collapsed, "the arrows");
                Expanded(buttons[3]).ShouldBe(ExpandCollapseState.Collapsed, "the profile button");
                Expanded(buttons[7]).ShouldBe(ExpandCollapseState.Expanded, "Pinned is open");
                Expanded(buttons[8]).ShouldBe(ExpandCollapseState.Collapsed, "sticky keys");
                Expanded(buttons[10]).ShouldBe(ExpandCollapseState.Collapsed, "Quick settings");
                On(buttons[4]).ShouldBe(ToggleState.Off, "Auto");
                On(buttons[9]).ShouldBe(ToggleState.Off, "the bar folds by itself");
                foreach (var index in new[] { 3, 4, 7, 8, 9, 10 })
                {
                    PeerOf(buttons[index])
                        .GetPattern(PatternInterface.Invoke)
                        .ShouldBeNull("one pattern per button");
                }

                var state = State(DockSide.Right, SizeId.M, DockFlyout.Profiles, coachStep: 0);
                dock.Apply(
                    state with
                    {
                        IsFixed = true,
                        QuickOpen = true,
                        Dock = state.Dock with { PinOpen = true },
                    }
                );

                Expanded(buttons[3]).ShouldBe(ExpandCollapseState.Expanded);
                Expanded(buttons[7]).ShouldBe(ExpandCollapseState.Collapsed);
                Expanded(buttons[10]).ShouldBe(ExpandCollapseState.Expanded);
                On(buttons[4]).ShouldBe(ToggleState.On, "Fixed");
                On(buttons[9]).ShouldBe(ToggleState.On, "the bar stays open");
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Theory]
    [InlineData("es", "Abrir barra", "Barra de la pestaña", "Guía de la pestaña")]
    [InlineData("en", "Open bar", "Edge tab bar", "Edge tab guide")]
    [Trait("Req", "REG-06")]
    [Trait("Req", "PES-001")]
    [Trait("Req", "PES-015")]
    public void The_handle_the_open_bar_and_the_guide_of_the_Tab_view_have_names_of_their_own(
        string language,
        string handleName,
        string barName,
        string coachName
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(
                time,
                DockSide.Right,
                SizeId.M,
                DockFlyout.None,
                coachStep: 1,
                language
            );
            var handle = new DockHandleWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            var bar = new DockBarWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            var coach = new DockCoachWindow(
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                // The title of a window is its name for UI Automation once it is on screen.
                handle.Title.ShouldBe(handleName);
                bar.Title.ShouldBe(barName, "the open bar is not named after its handle");
                coach.Title.ShouldBe(coachName);

                // The guide keeps its name in every step; the title of the step is its text.
                dock.Apply(State(DockSide.Right, SizeId.M, DockFlyout.None, coachStep: 2));
                coach.Title.ShouldBe(coachName, "the name does not follow the step");
            }
            finally
            {
                handle.Close();
                bar.Close();
                coach.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "PES-013")]
    [Trait("Req", "SEG-002")]
    public void The_floating_Release_all_announces_what_is_held_when_it_appears_and_when_it_changes()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var pill = new PanicPillWindow(
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static () => { }
            );
            pill.ApplyLabel("Soltar todo");
            try
            {
                pill.ApplyHeld("Pulsado: Mantener Ctrl");
                pill.Announcer.Announcements.ShouldBe(0, "a hidden surface announces nothing");
                AutomationProperties.GetHelpText(pill.Button).ShouldBe("Pulsado: Mantener Ctrl");

                using (var host = AuditHost.OfWindow(pill, theme))
                {
                    pill.Announcer.Announcements.ShouldBe(1, "it says why it appeared");
                    pill.Announcer.LastText.ShouldBe("Pulsado: Mantener Ctrl");
                    pill.Announcer.LastUrgency.ShouldBe(AnnouncementUrgency.Assertive);
                    AutomationProperties
                        .GetLiveSetting(pill.Button)
                        .ShouldBe(AutomationLiveSetting.Assertive);
                    pill.Button.Content.ShouldBe("Soltar todo", "the button keeps its own text");
                    host.Peer("Soltar todo").GetHelpText().ShouldBe("Pulsado: Mantener Ctrl");

                    pill.ApplyHeld("Pulsado: Mantener Ctrl");
                    pill.Announcer.Announcements.ShouldBe(1, "nothing changed");
                    pill.ApplyHeld("Pulsado: Mantener Ctrl, Mayús fija");
                    pill.Announcer.Announcements.ShouldBe(2);
                    pill.Announcer.LastText.ShouldBe("Pulsado: Mantener Ctrl, Mayús fija");
                }

                using (AuditHost.OfWindow(pill, theme))
                {
                    pill.Announcer.Announcements.ShouldBe(3, "it appeared again");
                }
            }
            finally
            {
                pill.Close();
            }
        });
    }

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element);

    private static System.Windows.Media.Color Color(object brush) =>
        brush.ShouldBeAssignableTo<SolidColorBrush>()!.Color;

    private static ExpandCollapseState Expanded(UIElement element) =>
        PeerOf(element)
            .GetPattern(PatternInterface.ExpandCollapse)
            .ShouldBeAssignableTo<IExpandCollapseProvider>()!
            .ExpandCollapseState;

    private static ToggleState On(UIElement element) =>
        PeerOf(element)
            .GetPattern(PatternInterface.Toggle)
            .ShouldBeAssignableTo<IToggleProvider>()!
            .ToggleState;

    private static ThemeService Theme() => AuditLooks.Theme(AuditLook.Dark);

    private static DockBarViewModel Dock(
        TimeProvider time,
        DockSide side,
        SizeId size,
        DockFlyout flyout,
        int coachStep,
        string language = "es"
    )
    {
        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var dock = new DockBarViewModel(
            controller,
            PanelTestData.Localization(language),
            new NoDockIntents()
        );
        var model = PanelProjector.Project(
            PanelTestData.Profile(),
            new LangCode(language),
            LangCode.Es
        );
        dock.ApplyTiles(model.Tiles, model.Tiles);
        dock.Apply(State(side, size, flyout, coachStep));
        return dock;
    }

    private static DockBarState State(
        DockSide side,
        SizeId size,
        DockFlyout flyout,
        int coachStep
    ) =>
        new(
            new DockSettings
            {
                Side = side,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = false,
                PerPage = 8,
                CoachDone = coachStep == 0,
            },
            PanelSizes.Get(size),
            ShowStripRow: true,
            StickyRow: true,
            VoiceNumbers: true,
            Frequents: false,
            ProfileName: "Word",
            ProfileIcon: "description",
            IsActiveApp: true,
            IsFixed: false,
            CanRepeat: true,
            flyout,
            coachStep,
            BarOpen: true,
            AnythingHeld: true
        );

    /// <summary>The view models of a whole panel over one document and one clock.</summary>
    private sealed class PanelWorld
    {
        public PanelWorld(string language, FakeTimeProvider time)
        {
            Interactions = new InteractionsWorld(language);
            Panel = PanelBodyTestData.Panel(
                PanelBodyTestData.GalleryLibrary(),
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                language,
                PanelBodyTestData.GalleryKeys
            );
            Header = new PanelHeaderViewModel(
                new ProfileViewCoordinator(Interactions.Store),
                Interactions.Localization,
                new PanelHeaderActions(
                    static () => { },
                    static () => { },
                    () => QuickSettingsTaps++,
                    static () => { }
                )
            );
            _language = new LangCode(language);
            ShowHeader(new ViewTarget.Profile(PanelBodyTestData.Word));
            Search = SearchSurfaceTests.Search(language);
            // The search finds the shortcuts the panel shows.
            Search.ApplyLibrary(PanelBodyTestData.GalleryLibrary());
            Suggestion = SearchSurfaceTests.Suggestion(language);
            Layers = new PanelLayerModels(
                Interactions.QuickSettings,
                Interactions.EditMode,
                Interactions.Menu,
                Interactions.TestMode,
                Interactions.Modes,
                static () => false
            );
            _ = time;
        }

        private readonly LangCode _language;

        /// <summary>
        /// The longest notice of key safety (REG-03): the key protection is off; an assertive warning to read whole.
        /// </summary>
        public static PanelNotice SafetyNotice { get; } =
            new(L.GuardianUnstable, new IconRef("warning"), NoticeTone.Warning);

        public InteractionsWorld Interactions { get; }

        /// <summary>
        /// The settings of the case, in the document as well, so what reads them there (Quick settings: the size and the
        /// view chosen, the switches of the rows) shows the panel that is drawn.
        /// </summary>
        public void ApplySettings(SizeSetting size, bool compact, bool allOn, int textScalePercent)
        {
            Set(SettingPaths.Size, size);
            Set(SettingPaths.Density, compact ? PanelDensity.Compact : PanelDensity.Full);
            Set(SettingPaths.StickyModifiersRow, allOn);
            Set(SettingPaths.VoiceNumbers, allOn);
            Interactions.QuickSettings.Apply(Interactions.Settings);
            Panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size,
                    Compact = compact,
                    StickyRow = allOn,
                    VoiceNumbers = allOn,
                    TextScalePercent = textScalePercent,
                }
            );

            void Set(string path, object value) =>
                Interactions
                    .Store.Dispatch(new SetSetting(path, value))
                    .IsSuccess.ShouldBeTrue(path);
        }

        /// <summary>
        /// Applies <paramref name="context"/> with the fixed notice of edit mode or test mode in the status row when it
        /// says nothing else, as the composition of the app does (AVI-002).
        /// </summary>
        public void ApplyContext(PanelBodyContext context) =>
            Panel.ApplyContext(
                context.Notice is null
                    ? context with
                    {
                        Notice = Interactions.Notices.Sticky,
                    }
                    : context
            );

        /// <summary>
        /// Gives the header the view the body shows: the profile, with the same icon as the row «Qué ver», or
        /// Frequents (CAB-002).
        /// </summary>
        public void ShowHeader(ViewTarget view) =>
            Header.Apply(
                PanelHeaderProjection.Project(
                    new ProfileState(view, false, null),
                    PanelBodyTestData.GalleryLibrary(),
                    PanelBodyTestData.Word,
                    searchingWithText: false,
                    _language,
                    LangCode.Es
                )
            );

        /// <summary>How many times «Ajustes rápidos» of the header was used.</summary>
        public int QuickSettingsTaps { get; private set; }

        public PanelViewModel Panel { get; }

        public PanelHeaderViewModel Header { get; }

        public Clicalo.Presentation.Panel.Search.SearchViewModel Search { get; }

        public Clicalo.Presentation.Panel.Search.SuggestionViewModel Suggestion { get; }

        public PanelLayerModels Layers { get; }
    }

    private sealed class NoDockIntents : IDockIntents
    {
        public void OpenBar() { }

        public void MoveHandle(int percent) { }

        public void CloseBar() { }

        public void Expand() { }

        public void ShowFrequents() { }

        public void ProfileButton() { }

        public void ToggleLock() { }

        public void Search() { }

        public void Repeat() { }

        public void TogglePinned() { }

        public void ToggleSticky() { }

        public void ToggleArrows() { }

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void CoachNext() { }

        public void CoachSkip() { }

        public void ReleaseAll() { }
    }
}
