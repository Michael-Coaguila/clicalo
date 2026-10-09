using Clicalo.Application.Coordinators;
using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Dimming;
using Clicalo.Domain.KeySafety;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.UI.Wpf.Windowing;
using Clicalo.Windowing.IntegrationTests.Theming;
using Clicalo.Windowing.IntegrationTests.Windowing.Support;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The M2 panel with the visual base of M3.1, headless: the panel is built but never shown, so no handle, no desktop
/// and no input. It takes the shape of the prototype, the theme service of its thread, the look of each tile and the
/// opacity of <see cref="DimPolicy"/>.
/// </summary>
public sealed class PanelWindowLookTests
{
    private static readonly DimSettings Dim = new(AutoDim: true, Opacity: 0.92, DimTo: 0.35);

    [Fact]
    [Trait("Req", "PAN-003")]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "TEM-003")]
    [Trait("Req", "CUA-011")]
    public void The_panel_is_rounded_themed_and_its_tiles_show_icon_category_and_badge()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        var (window, theme, _) = Build(lab, time, textScalePercent: 150);
        try
        {
            WpfThread.Invoke(() =>
            {
                window.Look.ShouldBe(SurfaceLook.Panel);
                window.AllowsTransparency.ShouldBeTrue();
                window.Resources.MergedDictionaries.ShouldContain(theme.Resources);

                var tiles = window.TileControls;
                tiles.Count.ShouldBe(4);
                tiles.ShouldAllBe(static tile =>
                    tile.Symbol == "keyboard" && tile.Category == CategoryToken.Edit
                );
                tiles[0].IconSize.ShouldBe(PanelSizes.M.TileIconPx);
                tiles[0].Badge.ShouldBeEmpty();
                tiles[1].Badge.ShouldBe("MANTENER");
                tiles[2].Badge.ShouldBe("ALTERNAR");

                // CUA-011: names at 14 px × 150 % and the key line at 10 px × 150 %.
                tiles[0].FontSize.ShouldBe(21);
                tiles[0].KeysFontSize.ShouldBe(15);
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "GEN-009")]
    [Trait("Req", "SEG-002")]
    public void The_panel_dims_after_the_pointer_leaves_and_not_while_something_is_held()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        var (window, theme, viewModel) = Build(lab, time, textScalePercent: 100);
        try
        {
            WpfThread.Invoke(() =>
            {
                window.ApplyDimSettings(Dim);
                window.Opacity.ShouldBe(0.92, 0.001);
                window.OnHover(inside: true);
                window.OnHover(inside: false);
            });

            time.Advance(TimeSpan.FromMilliseconds(2400));
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            WpfThread.Invoke(() => window.Opacity).ShouldBe(0.92, 0.001);

            time.Advance(TimeSpan.FromMilliseconds(100));
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            WpfThread.Invoke(() => window.Opacity).ShouldBe(0.35, 0.001);

            // Panic: «Release all» is never dimmed (SEG-002).
            WpfThread.Invoke(() => viewModel.ApplyEngine(HeldCtrl()));
            WpfThread.Invoke(() => window.Opacity).ShouldBe(0.92, 0.001);
            time.Advance(TimeSpan.FromSeconds(10));
            WpfThread.Invoke(WpfThread.DrainPendingWork);
            WpfThread.Invoke(() => window.Opacity).ShouldBe(0.92, 0.001);
        }
        finally
        {
            Close(window, theme);
        }
    }

    [Fact]
    [Trait("Req", "TEM-004")]
    public void A_contrast_theme_keeps_the_panel_fully_opaque()
    {
        using var lab = SurfaceLab.Create();
        var time = new FakeTimeProvider();
        var (window, theme, _) = Build(lab, time, textScalePercent: 100, ThemeChoice.HighContrast);
        try
        {
            WpfThread.Invoke(() =>
            {
                window.ApplyDimSettings(Dim);
                window.Opacity.ShouldBe(1, 0.001);
            });
        }
        finally
        {
            Close(window, theme);
        }
    }

    private static (PanelWindow Window, ThemeService Theme, PanelViewModel ViewModel) Build(
        SurfaceLab lab,
        FakeTimeProvider time,
        int textScalePercent,
        ThemeChoice choice = ThemeChoice.Dark
    ) =>
        WpfThread.Invoke(() =>
        {
            // Reduce motion: the opacity changes at once, so the test reads the value it was given.
            var theme = new ThemeService(
                new FakeSystemTheme(),
                choice,
                textScalePercent,
                reduceMotion: true
            );
            var controller = new PanelInteractionController(new PanelEngineInbox(), () => 1, time);
            var viewModel = new PanelViewModel(
                controller,
                PanelTestData.Localization("es"),
                PanelDesktopFixture.Touch,
                _ => null
            );
            viewModel.Apply(
                PanelProjector.Project(PanelTestData.Profile(), LangCode.Es, LangCode.Es)
            );
            var window = new PanelWindow(
                viewModel,
                lab.Registry,
                time,
                PanelSizes.M,
                columns: 4,
                theme,
                new DimSettings(AutoDim: false, Opacity: 1, DimTo: 1)
            );
            return (window, theme, viewModel);
        });

    private static void Close(PanelWindow window, ThemeService theme) =>
        WpfThread.Invoke(() =>
        {
            window.Close();
            theme.Dispose();
        });

    private static EngineSnapshot HeldCtrl() =>
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
}
