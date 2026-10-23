using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Library;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Automation.Audit;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// The Tab view in direction C (D12): the measures of its bar come from the scales of the design system, every control
/// is drawn at least as large as it answers, and its windows keep their size.
/// </summary>
public sealed class DockLookTests
{
    [Theory]
    [InlineData(SizeId.S, 44, 44, 44, 44)]
    [InlineData(SizeId.M, 44, 64, 52, 88)]
    [InlineData(SizeId.L, 52, 80, 72, 104)]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-007")]
    [Trait("Req", "TEM-002")]
    public void The_bar_grows_with_the_size_of_the_panel(
        SizeId size,
        int tool,
        int row,
        int sideTile,
        int rowTile
    )
    {
        var metrics = PanelSizes.Get(size);

        // D20: a side bar is two controls side by side and its padding, 44 in S and M and 52 in L; a top or bottom one a
        // row of the shortcuts of its size and its padding. S draws its shortcuts as icons of one touch target.
        metrics.DockBarWidthPx.ShouldBe(
            (2 * tool) + (int)Space.S1 + (2 * DockGeometry.BarPaddingPx)
        );
        metrics.DockBarHeightPx.ShouldBe(row);
        metrics.DockHorizontalTileHeightPx.ShouldBe(row);
        metrics.DockVerticalTileHeightPx.ShouldBe(sideTile);
        metrics.DockHorizontalTileWidthPx.ShouldBe(rowTile);
        metrics.DockTileIconPx.ShouldBe((int)(size == SizeId.L ? IconBox.Medium : IconBox.Small));
        metrics.DockTileLabelPx.ShouldBe(
            (int)(size == SizeId.L ? TextStyles.Label.Size : TextStyles.LabelSmall.Size)
        );
    }

    [Theory]
    [InlineData(DockSide.Right, "es")]
    [InlineData(DockSide.Left, "en")]
    [InlineData(DockSide.Top, "es")]
    [InlineData(DockSide.Bottom, "en")]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-008")]
    [Trait("Req", "REG-02")]
    public void Every_control_of_the_bar_is_drawn_at_least_44_on_every_side(
        DockSide side,
        string language
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, side, language, coachStep: 0);
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
                using var host = AuditHost.OfWindow(bar, theme);
                host.LayOut();
                foreach (var button in bar.Buttons.Where(b => b.Visibility == Visibility.Visible))
                {
                    button.ActualWidth.ShouldBeGreaterThanOrEqualTo(ControlHeight.Small);
                    button.ActualHeight.ShouldBeGreaterThanOrEqualTo(ControlHeight.Small);
                }

                if (DockGeometry.IsVertical(side))
                {
                    host.Root.ActualWidth.ShouldBe(PanelSizes.Get(SizeId.M).DockBarWidthPx, 0.5);
                }
                else
                {
                    host.Root.ActualHeight.ShouldBe(
                        ControlHeight.Large + (2 * DockGeometry.BarPaddingPx),
                        0.5
                    );
                }
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [Trait("Req", "PES-015")]
    public void The_guide_keeps_its_size_from_one_step_to_the_next(string language)
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, DockSide.Right, language, coachStep: 0);
            var coach = new DockCoachWindow(
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                using var host = AuditHost.OfWindow(coach, theme);
                var heights = new List<double>();
                for (var step = 0; step < DockRules.CoachSteps; step++)
                {
                    dock.Apply(State(DockSide.Right, step));
                    host.LayOut();
                    heights.Add(host.Root.ActualHeight);
                }

                heights.Distinct().Count().ShouldBe(1, string.Join(", ", heights));
                coach.NextButton.ActualHeight.ShouldBe(ControlHeight.Small);
                coach.SkipButton.ActualHeight.ShouldBe(ControlHeight.Small);
            }
            finally
            {
                coach.Close();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Top, "es")]
    [InlineData(DockSide.Bottom, "es")]
    [Trait("Req", "PES-007")]
    [Trait("Req", "CUA-011")]
    public void The_icons_and_names_of_a_top_or_bottom_bar_stay_on_one_line_whatever_the_names(
        DockSide side,
        string language
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();

            // In M a long name beside «Copiar»: names of two lines and of one (S shows icons alone, D20).
            var dock = Dock(time, side, language, coachStep: 0, SizeId.M, "Mantener Control");
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
                using var host = AuditHost.OfWindow(bar, theme);
                host.LayOut();
                bar.TileControls.Count.ShouldBeGreaterThan(1);
                bar.TileControls.Select(LabelLines).Distinct().Count().ShouldBe(2);
                var icons = bar
                    .TileControls.Select(t => Top(t, ShortcutTileTemplate.IconPart, host.Root))
                    .ToList();
                var names = bar
                    .TileControls.Select(t => Top(t, ShortcutTileTemplate.LabelPart, host.Root))
                    .ToList();
                icons.Max().ShouldBe(icons.Min(), 0.5, string.Join(", ", icons));
                names.Max().ShouldBe(names.Min(), 0.5, string.Join(", ", names));
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Right, "es", SizeId.M)]
    [InlineData(DockSide.Left, "en", SizeId.M)]
    [InlineData(DockSide.Right, "es", SizeId.S)]
    [Trait("Req", "PES-008")]
    [Trait("Req", "CAB-003")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    public void The_tools_of_a_side_bar_are_named_icons_of_44_in_pairs(
        DockSide side,
        string language,
        SizeId size
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, side, language, coachStep: 0, size);
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
                using var host = AuditHost.OfWindow(bar, theme);
                host.LayOut();

                // D19: search, repeat, Pinned, Sticky keys, the lock and the quick settings are icons of 44, their
                // label only their name.
                var tools = bar
                    .Buttons.Skip(5)
                    .Where(b => b.Visibility == Visibility.Visible)
                    .ToList();
                tools.Count.ShouldBeGreaterThan(4);
                foreach (var tool in tools)
                {
                    tool.ActualWidth.ShouldBe(ControlHeight.Small);
                    tool.ActualHeight.ShouldBe(ControlHeight.Small);
                    System
                        .Windows.Automation.AutomationProperties.GetName(tool)
                        .ShouldNotBeNullOrWhiteSpace();
                    VisibleTexts(tool).ShouldBeEmpty();
                }

                // D20: the lock goes beside the quick settings, as every pair, and not beside Auto/Fixed, whose own
                // lock it was taken for.
                var lockOpen = bar.Buttons[9].TranslatePoint(default, host.Root);
                var tune = bar.Buttons[10].TranslatePoint(default, host.Root);
                tune.Y.ShouldBe(lockOpen.Y, 0.5);
                (tune.X - lockOpen.X).ShouldBe(ControlHeight.Small + Space.S1, 0.5);

                // Auto/Fixed says its state in words across the bar where the size has names, and is an icon of 44 in S.
                var auto = bar.Buttons[4];
                if (size == SizeId.S)
                {
                    auto.ActualWidth.ShouldBe(ControlHeight.Small);
                    VisibleTexts(auto).ShouldBeEmpty();

                    // ★ and the profile are two whole targets of 44 inside the bar, never cut by a frame (D20 review).
                    foreach (var segment in new[] { bar.Buttons[2], bar.Buttons[3] })
                    {
                        segment.ActualWidth.ShouldBe(ControlHeight.Small);
                        segment
                            .TranslatePoint(new Point(segment.ActualWidth, 0), host.Root)
                            .X.ShouldBeLessThanOrEqualTo(
                                host.Root.ActualWidth - DockGeometry.BarPaddingPx + 0.5
                            );
                    }

                    var star = bar.Buttons[2].TranslatePoint(default, host.Root);
                    var profile = bar.Buttons[3].TranslatePoint(default, host.Root);
                    (profile.X - star.X).ShouldBe(ControlHeight.Small + Space.S1, 0.5);
                }
                else
                {
                    auto.ActualWidth.ShouldBe((2 * ControlHeight.Small) + Space.S1, 0.5);
                    VisibleTexts(auto).ShouldNotBeEmpty();
                }
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Right, SizeId.M)]
    [InlineData(DockSide.Bottom, SizeId.M)]
    [InlineData(DockSide.Left, SizeId.L)]
    [Trait("Req", "PES-007")]
    [Trait("Req", "PAN-009")]
    public void The_last_page_keeps_the_length_of_a_full_one_so_nothing_jumps(
        DockSide side,
        SizeId size
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, side, "es", coachStep: 0, size);
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
                using var host = AuditHost.OfWindow(bar, theme);
                host.LayOut();

                // Room for three of the four shortcuts: a page of three, then a page of one.
                var vertical = DockGeometry.IsVertical(side);
                var tile = dock.TileLengthAlong(vertical);
                dock.ApplyTileSpace((3 * tile) + (2 * Space.S2));
                host.LayOut();
                dock.HasPages.ShouldBeTrue();
                var full = vertical ? host.Root.ActualHeight : host.Root.ActualWidth;

                dock.Next();
                host.LayOut();
                dock.PageTiles.Count.ShouldBeLessThan(dock.PerPage);
                (vertical ? host.Root.ActualHeight : host.Root.ActualWidth).ShouldBe(full, 0.5);
            }
            finally
            {
                bar.Close();
            }
        });
    }

    [Fact]
    [Trait("Req", "PES-007")]
    public void A_bar_of_another_edge_never_changes_how_many_shortcuts_the_bar_in_view_holds()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = Theme();
            var dock = Dock(time, DockSide.Right, "es", coachStep: 0, SizeId.S);
            var right = new DockBarWindow(
                DockSide.Right,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            DockBarWindow? top = null;
            try
            {
                // Two rows of icons of S, two across: four shortcuts.
                dock.ApplyTileSpace((2 * ControlHeight.Small) + Space.S1);
                dock.PerPage.ShouldBe(4);

                // The four bars share the view model: the top one, one icon across, must not halve the side bar (D20).
                top = new DockBarWindow(
                    DockSide.Top,
                    dock,
                    lab.Registry,
                    time,
                    theme,
                    PanelDesktopFixture.Touch,
                    static (_, _, _) => { }
                );
                dock.PerPage.ShouldBe(4);
            }
            finally
            {
                top?.Close();
                right.Close();
            }
        });
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    [Trait("Req", "PES-010")]
    [Trait("Req", "CUA-011")]
    public void The_shortcuts_of_Pinned_keep_two_lines_of_name_at_every_text_scale(int scale)
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                scale,
                reduceMotion: true
            );
            var dock = Dock(time, DockSide.Right, "es", coachStep: 0);
            var panel = PanelBodyTestData.Panel(
                PanelBodyTestData.Library(14, 3),
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                "es"
            );
            var window = new DockFlyoutWindow(
                DockFlyout.Pinned,
                dock,
                panel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch,
                static (_, _, _) => { }
            );
            try
            {
                using var host = AuditHost.OfWindow(window, theme);
                host.LayOut();
                var line = TextStyles.LabelSmall.LineHeight * scale / 100d;
                foreach (var tile in window.TileControls)
                {
                    tile.ActualHeight.ShouldBe(
                        ControlHeight.Large + (2 * line) - (2 * TextStyles.LabelSmall.LineHeight),
                        0.5
                    );
                    var room =
                        tile.ActualHeight
                        - tile.Padding.Top
                        - tile.Padding.Bottom
                        - IconBox.Small
                        - Space.S1;
                    room.ShouldBeGreaterThanOrEqualTo((2 * line) - 0.5);
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Right, SizeId.M, 100, 1)]
    [InlineData(DockSide.Left, SizeId.L, 100, 2)]
    [InlineData(DockSide.Right, SizeId.M, 150, 2)]
    [InlineData(DockSide.Top, SizeId.M, 100, 2)]
    [InlineData(DockSide.Bottom, SizeId.L, 150, 2)]
    [InlineData(DockSide.Right, SizeId.S, 150, 0)]
    [InlineData(DockSide.Top, SizeId.S, 150, 0)]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-007")]
    [Trait("Req", "CUA-011")]
    public void The_names_of_the_bar_get_two_lines_when_the_text_grows_and_the_bar_grows_with_them(
        DockSide side,
        SizeId size,
        int scale,
        int lines
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                scale,
                reduceMotion: true
            );
            var dock = Dock(time, side, "es", coachStep: 0, size);
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
                using var host = AuditHost.OfWindow(bar, theme);
                host.LayOut();
                bar.TileControls.Count.ShouldBeGreaterThan(0);
                if (lines == 0)
                {
                    // D20: the shortcuts of S are icons of one touch target at every text scale; the name stays for UI
                    // Automation and the voice numbers.
                    foreach (var tile in bar.TileControls)
                    {
                        tile.ActualWidth.ShouldBe(ControlHeight.Small, 0.5);
                        tile.ActualHeight.ShouldBe(ControlHeight.Small, 0.5);
                        (
                            (FrameworkElement)
                                tile.Template.FindName(ShortcutTileTemplate.LabelPart, tile)
                        ).Visibility.ShouldBe(Visibility.Collapsed);
                    }

                    return;
                }

                var metrics = PanelSizes.Get(size);
                var label =
                    metrics.DockTileLabelPx > TextStyles.LabelSmall.Size
                        ? TextStyles.Label
                        : TextStyles.LabelSmall;
                var line = label.LineHeight * scale / 100d;
                var icon = metrics.DockTileIconPx;
                var twoLines = (3 * Space.S1) + icon + (2 * line);
                foreach (var tile in bar.TileControls)
                {
                    tile.MaxNameLines.ShouldBe(lines);
                    var room = tile.ActualHeight - (3 * Space.S1) - icon;
                    room.ShouldBeGreaterThanOrEqualTo((lines * line) - 0.5);
                    if (lines == 2)
                    {
                        tile.ActualHeight.ShouldBe(Math.Max(twoLines, TileBase(side, size)), 0.5);
                    }
                }

                // A top or bottom bar is as thick as its shortcuts and its padding: it grows with them, and they grow
                // wider with their name, so a word that fits on a line at 100 % is never split.
                if (!DockGeometry.IsVertical(side))
                {
                    bar.TileControls[0]
                        .ActualWidth.ShouldBe(
                            PanelSizes.Get(size).DockHorizontalTileWidthPx * scale / 100d,
                            0.5
                        );
                    host.Root.ActualHeight.ShouldBe(
                        twoLines + (2 * DockGeometry.BarPaddingPx),
                        0.5
                    );
                }
                else
                {
                    dock.TileLength.ShouldBe(bar.TileControls[0].ActualHeight, 0.5);
                }
            }
            finally
            {
                bar.Close();
            }
        });

        static double TileBase(DockSide side, SizeId size) =>
            DockGeometry.IsVertical(side)
                ? PanelSizes.Get(size).DockVerticalTileHeightPx
                : PanelSizes.Get(size).DockHorizontalTileHeightPx;
    }

    private static int LabelLines(ShortcutTile tile)
    {
        var label = (TextBlock)tile.Template.FindName(ShortcutTileTemplate.LabelPart, tile);
        var line = label.FontFamily.LineSpacing * label.FontSize;
        return (int)Math.Round(label.DesiredSize.Height / line);
    }

    private static double Top(ShortcutTile tile, string part, UIElement root) =>
        ((FrameworkElement)tile.Template.FindName(part, tile)).TranslatePoint(default, root).Y;

    /// <summary>The texts under <paramref name="parent"/> that are on screen, whatever their parents hide.</summary>
    private static List<string> VisibleTexts(DependencyObject parent)
    {
        var texts = new List<string>();
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is TextBlock { IsVisible: true } text && text.Text.Length > 0)
            {
                texts.Add(text.Text);
            }

            texts.AddRange(VisibleTexts(child));
        }

        return texts;
    }

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static DockBarViewModel Dock(
        TimeProvider time,
        DockSide side,
        string language,
        int coachStep,
        SizeId size = SizeId.M,
        string? holdName = null
    )
    {
        var profile = PanelTestData.Profile();
        if (holdName is not null)
        {
            var name = LocalizedText.Same(holdName, LangCode.Es, LangCode.En);
            profile = profile with
            {
                Shortcuts = new ValueList<Shortcut>([
                    .. profile.Shortcuts.Select(s =>
                        s.Id.Equals(PanelTestData.HoldCtrl) ? s with { Name = name } : s
                    ),
                ]),
            };
        }

        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var dock = new DockBarViewModel(
            controller,
            PanelTestData.Localization(language),
            new NoDockIntents()
        );
        var model = PanelProjector.Project(profile, new LangCode(language), LangCode.Es);
        dock.ApplyTiles(model.Tiles, model.Tiles);
        dock.Apply(State(side, coachStep, size));
        return dock;
    }

    private static DockBarState State(DockSide side, int coachStep, SizeId size = SizeId.M) =>
        new(
            new DockSettings
            {
                Side = side,
                HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                HandleLocked = false,
                PinOpen = false,
                Gutter = false,
                PerPage = 8,
                CoachDone = false,
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
            DockFlyout.None,
            coachStep,
            BarOpen: true,
            AnythingHeld: false
        );

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

        public void TogglePinOpen() { }

        public void QuickSettings() { }

        public void CoachNext() { }

        public void CoachSkip() { }

        public void ReleaseAll() { }
    }
}
