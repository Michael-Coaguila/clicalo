using System.Windows;
using Clicalo.Application.Coordinators;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
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

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static DockBarViewModel Dock(
        TimeProvider time,
        DockSide side,
        string language,
        int coachStep
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
        dock.Apply(State(side, coachStep));
        return dock;
    }

    private static DockBarState State(DockSide side, int coachStep) =>
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
            PanelSizes.Get(SizeId.M),
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
