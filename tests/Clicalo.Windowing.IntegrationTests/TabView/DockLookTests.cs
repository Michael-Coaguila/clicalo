using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
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
    [InlineData(SizeId.S)]
    [InlineData(SizeId.M)]
    [InlineData(SizeId.L)]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-007")]
    [Trait("Req", "TEM-002")]
    public void The_measures_of_the_bar_are_those_of_the_design_system(SizeId size)
    {
        var metrics = PanelSizes.Get(size);

        // A side bar is two targets of 44 side by side and its padding; a top or bottom one a row of 64 and its padding.
        metrics.DockBarWidthPx.ShouldBe(
            (int)(DockBarWindow.PairWidth + (2 * DockGeometry.BarPaddingPx))
        );
        metrics.DockBarHeightPx.ShouldBe((int)ControlHeight.Large);
        metrics.DockHorizontalTileHeightPx.ShouldBe((int)ControlHeight.Large);
        new[] { ControlHeight.Small, ControlHeight.Medium, ControlHeight.Large }.ShouldContain(
            metrics.DockVerticalTileHeightPx
        );
        new[] { IconBox.Small, IconBox.Medium }.ShouldContain(metrics.DockTileIconPx);
        metrics.DockTileLabelPx.ShouldBe((int)TextStyles.LabelSmall.Size);
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

            // In S «Mantener Ctrl» beside «Copiar»: names of two lines and of one.
            var dock = Dock(time, side, language, coachStep: 0, SizeId.S);
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
    [InlineData(DockSide.Right, "es")]
    [InlineData(DockSide.Left, "en")]
    [Trait("Req", "PES-008")]
    public void The_labeled_tools_of_a_side_bar_line_up_their_icons(DockSide side, string language)
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

                // Auto/Fixed, Pinned, Sticky keys and the lock: rows whose labels have different lengths.
                var lefts = new[] { bar.Buttons[4], bar.Buttons[7], bar.Buttons[8], bar.Buttons[9] }
                    .Where(b => b.Visibility == Visibility.Visible)
                    .Select(b => FirstIcon(b).TranslatePoint(default, host.Root).X)
                    .ToList();
                lefts.Count.ShouldBeGreaterThan(2);
                lefts.Max().ShouldBe(lefts.Min(), 0.5, string.Join(", ", lefts));
            }
            finally
            {
                bar.Close();
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

    private static int LabelLines(ShortcutTile tile)
    {
        var label = (TextBlock)tile.Template.FindName(ShortcutTileTemplate.LabelPart, tile);
        var line = label.FontFamily.LineSpacing * label.FontSize;
        return (int)Math.Round(label.DesiredSize.Height / line);
    }

    private static double Top(ShortcutTile tile, string part, UIElement root) =>
        ((FrameworkElement)tile.Template.FindName(part, tile)).TranslatePoint(default, root).Y;

    private static SymbolIcon FirstIcon(DependencyObject parent) =>
        FirstIconOrNull(parent) ?? throw new InvalidOperationException("No icon.");

    private static SymbolIcon? FirstIconOrNull(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is SymbolIcon icon)
            {
                return icon;
            }

            if (FirstIconOrNull(child) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static DockBarViewModel Dock(
        TimeProvider time,
        DockSide side,
        string language,
        int coachStep,
        SizeId size = SizeId.M
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
