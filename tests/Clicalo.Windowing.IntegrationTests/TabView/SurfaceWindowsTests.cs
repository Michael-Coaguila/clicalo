using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Bubble;
using Clicalo.Presentation.Dock;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.TabView;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;
using SizeId = Clicalo.Domain.Catalog.PanelSize;

namespace Clicalo.Windowing.IntegrationTests.TabView;

/// <summary>
/// The surfaces of the bubble, the Tab view and the Compact view, headless: built but never shown, so no handle, no
/// desktop and no input. They take the shape of the prototype and show what their view models say.
/// </summary>
public sealed class SurfaceWindowsTests
{
    [Fact]
    [Trait("Req", "BUR-001")]
    [Trait("Req", "BUR-002")]
    public void The_bubble_is_a_round_keyboard_button_with_a_red_ring_while_something_is_held()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var restored = 0;
            var viewModel = new BubbleViewModel(
                PanelTestData.Localization("es"),
                () => restored++,
                () => { }
            );
            var bubble = new BubbleWindow(
                viewModel,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                bubble.Look.ShouldBe(SurfaceLook.Bubble);
                bubble.Button.Symbol.ShouldBe("keyboard");
                bubble.Button.IconSize.ShouldBe(PanelSizes.Layout.BubbleIconPx);
                bubble.Title.ShouldBe("Mostrar panel");
                bubble.BorderThickness.Left.ShouldBeLessThan(3);

                viewModel.ApplyEngine(Held());
                bubble.BorderThickness.Left.ShouldBe(3);

                bubble.Button.RaiseEvent(
                    new System.Windows.RoutedEventArgs(
                        System.Windows.Controls.Primitives.ButtonBase.ClickEvent
                    )
                );
                restored.ShouldBe(1);
            }
            finally
            {
                bubble.Close();
                theme.Dispose();
            }
        });
    }

    [Theory]
    [InlineData(DockSide.Right, 44, 116)]
    [InlineData(DockSide.Bottom, 128, 44)]
    [Trait("Req", "PES-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-002")]
    public void The_handle_is_drawn_32_deep_rounded_toward_the_screen_and_takes_a_touch_44_deep(
        DockSide side,
        double width,
        double height
    )
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var dock = Dock(time);
            var handle = new DockHandleWindow(
                side,
                dock,
                lab.Registry,
                time,
                theme,
                PanelDesktopFixture.Touch
            );
            try
            {
                handle.Button.Width.ShouldBe(width);
                handle.Button.Height.ShouldBe(height);
                var face = handle.Button.Content.ShouldBeOfType<System.Windows.Controls.Border>();
                face.CornerRadius.ShouldBe(SurfaceLook.DockHandle(side).Corners);
                (DockGeometry.IsVertical(side) ? face.Width : face.Height).ShouldBe(32);
                handle.Title.ShouldBe("Abrir barra");
            }
            finally
            {
                handle.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "PES-005")]
    [Trait("Req", "PES-007")]
    [Trait("Req", "PES-008")]
    public void The_bar_shows_its_zones_and_the_shortcuts_of_its_page()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        WpfThread.Invoke(() =>
        {
            var theme = Theme();
            var dock = Dock(time);
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
                bar.Look.ShouldBe(SurfaceLook.Panel);
                bar.TileControls.Select(static t => t.AccessibleName)
                    .ShouldBe(["Copiar", "Mantener Ctrl", "Mayús fija", "Web"]);
                bar.Buttons.Count.ShouldBe(11);

                dock.ApplyTileSpace(130);
                bar.TileControls.Count.ShouldBe(2);
            }
            finally
            {
                bar.Close();
                theme.Dispose();
            }
        });
    }

    [Fact]
    [Trait("Req", "VCO-002")]
    public void The_compact_row_shows_Frequents_and_the_profile_button()
    {
        WpfThread.Invoke(() =>
        {
            var controller = new PanelInteractionController(
                new PanelEngineInbox(),
                () => 1,
                TimeProvider.System
            );
            var panel = new PanelViewModel(
                controller,
                PanelTestData.Localization("es"),
                PanelDesktopFixture.Touch,
                _ => null
            );
            panel.Apply(PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es));
            panel.ApplyLayout(PanelLayoutSettings.Default with { Compact = true });
            var row = new CompactRowView(panel.Selector, panel.Pager, () => false);

            row.Visibility.ShouldBe(System.Windows.Visibility.Collapsed);
            row.TapTargets.ShouldBeEmpty();
            row.Show(shown: true);
            row.Visibility.ShouldBe(System.Windows.Visibility.Visible);
            row.FrequentsButton.AccessibleName.ShouldBe("Frecuentes");
            row.TapTargets.Count().ShouldBeGreaterThanOrEqualTo(2);
            row.Detach();
        });
    }

    private static ThemeService Theme() =>
        new(new FakeSystemTheme(), ThemeChoice.Dark, 100, reduceMotion: true);

    private static DockBarViewModel Dock(TimeProvider time)
    {
        var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
        var dock = new DockBarViewModel(
            controller,
            PanelTestData.Localization("es"),
            new NoIntents()
        );
        var model = PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es);
        dock.ApplyTiles(model.Tiles, []);
        dock.Apply(
            new DockBarState(
                new DockSettings
                {
                    Side = DockSide.Right,
                    HandlePositions = new DockHandlePositions(50, 50, 50, 50),
                    HandleLocked = false,
                    PinOpen = false,
                    Gutter = false,
                    PerPage = 8,
                    CoachDone = true,
                },
                PanelSizes.Get(SizeId.M),
                ShowStripRow: false,
                StickyRow: false,
                VoiceNumbers: false,
                Frequents: false,
                ProfileName: "Word",
                ProfileIcon: "description",
                IsActiveApp: false,
                IsFixed: false,
                CanRepeat: false,
                DockFlyout.None,
                CoachStep: 0,
                BarOpen: true,
                AnythingHeld: false
            )
        );
        return dock;
    }

    private static EngineSnapshot Held() =>
        EngineSnapshot.Empty with
        {
            Held = new ValueList<PressedItem>([
                new PressedItem(
                    HolderId.ForContact(4),
                    HoldOrigin.Contact,
                    PanelTestData.HoldCtrl,
                    4,
                    [],
                    MouseButtons.None,
                    0,
                    null
                ),
            ]),
            Version = 1,
        };

    private sealed class NoIntents : IDockIntents
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
