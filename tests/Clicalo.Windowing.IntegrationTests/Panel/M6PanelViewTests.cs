using System.Windows;
using System.Windows.Controls;
using Clicalo.Application.Engine;
using Clicalo.Application.Profiles;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.Header;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Interactions;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// What M6 added to the views of the panel, headless (built and laid out on the WPF thread; never shown): the yellow
/// dot of the profile button (SEL-005), ★ Frequents in the profile grid and the title as a button (SEL-006), the armed
/// tile (EJE-002) and the outline of an ignored touch (TAC-003).
/// </summary>
public sealed class M6PanelViewTests
{
    [Fact]
    [Trait("Req", "SEL-005")]
    [Trait("Req", "ACC-003")]
    public void The_profile_button_shows_the_yellow_dot_and_says_it() =>
        WithBody(
            PanelLayoutSettings.Default,
            PanelBodyContext.Idle with
            {
                SuggestionApp = "Excel",
            },
            (body, panel) =>
            {
                body.Selector.SuggestionMark.Visibility.ShouldBe(Visibility.Visible);
                body.Selector.ProfileButton.AccessibleState.ShouldBe(
                    "Hay una sugerencia de perfil para Excel"
                );

                panel.ApplyContext(PanelBodyContext.Idle);

                body.Selector.SuggestionMark.Visibility.ShouldBe(Visibility.Collapsed);
                body.Selector.ProfileButton.AccessibleState.ShouldBeEmpty();
            }
        );

    [Fact]
    [Trait("Req", "SEL-006")]
    [Trait("Req", "REG-02")]
    public void Opened_from_the_title_the_grid_starts_with_Frequents_as_a_full_target() =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                ShowSelectorRow = false,
            },
            PanelBodyContext.Idle with
            {
                PickerOpen = true,
            },
            (body, panel) =>
            {
                var star = body.Picker.FrequentsTile;

                body.Picker.TapTargets.First().Element.ShouldBe(star);
                star.AccessibleName.ShouldBe("Frecuentes");
                star.ActualWidth.ShouldBeGreaterThanOrEqualTo(TouchTarget.MinimumSize);
                star.ActualHeight.ShouldBeGreaterThanOrEqualTo(TouchTarget.MinimumSize);

                panel.ApplyLayout(PanelLayoutSettings.Default);

                body.Picker.TapTargets.ShouldAllBe(target => target.Element != star);
            }
        );

    [Fact]
    [Trait("Req", "CUA-010")]
    public void The_empty_Frequents_card_takes_the_place_of_the_grid() =>
        WithBody(
            PanelLayoutSettings.Default,
            PanelBodyContext.Idle,
            (body, panel) =>
            {
                body.EmptyState.Visibility.ShouldBe(Visibility.Collapsed);
                body.ShortcutGrid.Visibility.ShouldBe(Visibility.Visible);

                panel.Apply(
                    PanelProjector.ProjectFrequents(
                        PanelBodyTestData.Library(3, 0),
                        [],
                        PanelBodyTestData.Word,
                        "Siempre visible",
                        LangCode.Es,
                        LangCode.Es
                    )
                );
                panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });

                body.EmptyState.Visibility.ShouldBe(Visibility.Visible);
                body.ShortcutGrid.Visibility.ShouldBe(Visibility.Collapsed);
                body.EmptyState.TapTargets.ShouldHaveSingleItem();

                panel.ApplyContext(PanelBodyContext.Idle);
                panel.Apply(
                    PanelProjector.Project(
                        PanelBodyTestData.Library(3, 0),
                        PanelBodyTestData.Word,
                        LangCode.Es,
                        LangCode.Es
                    )
                );

                body.EmptyState.Visibility.ShouldBe(Visibility.Collapsed);
                body.ShortcutGrid.Visibility.ShouldBe(Visibility.Visible);
            }
        );

    [Fact]
    [Trait("Req", "EJE-002")]
    public void The_armed_tile_gets_its_warn_outline() =>
        WithBody(
            PanelLayoutSettings.Default,
            PanelBodyContext.Idle,
            (body, panel) =>
            {
                var armed = new ShortcutId("w1");

                panel.ApplyEngine(
                    EngineSnapshot.Empty with
                    {
                        Armed = new ArmedConfirmation(armed, DateTimeOffset.UnixEpoch),
                        Version = 1,
                    }
                );

                body.ShortcutGrid.TileControls.Single(t => t.ViewModel.Id == armed)
                    .Control.IsArmed.ShouldBeTrue();
                body.ShortcutGrid.TileControls.Count(t => t.Control.IsArmed).ShouldBe(1);

                panel.ApplyEngine(EngineSnapshot.Empty with { Version = 2 });

                body.ShortcutGrid.TileControls.ShouldAllBe(t => !t.Control.IsArmed);
            }
        );

    [Fact]
    [Trait("Req", "TAC-003")]
    public void The_outline_of_an_ignored_touch_shows_only_while_its_tile_says_so() =>
        WpfThread.Invoke(() =>
        {
            var tile = new InteractionsWorld().Tile(InteractionsWorld.Bold);
            var outline = new IgnoredTouchOutline(tile);
            outline.Visibility.ShouldBe(Visibility.Collapsed);
            outline.IsHitTestVisible.ShouldBeFalse();

            tile.ShowIgnored(true);
            outline.Visibility.ShouldBe(Visibility.Visible);

            tile.ShowIgnored(false);
            outline.Visibility.ShouldBe(Visibility.Collapsed);

            outline.Detach();
            tile.ShowIgnored(true);
            outline.Visibility.ShouldBe(Visibility.Collapsed);
        });

    [Fact]
    [Trait("Req", "SEL-006")]
    [Trait("Req", "REG-06")]
    public void The_title_is_an_expand_collapse_button_only_while_it_opens_the_grid() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var taps = 0;
            var viewModel = new PanelHeaderViewModel(
                new ProfileViewCoordinator(world.Store),
                world.Localization,
                PanelHeaderActions.None with
                {
                    Title = () => taps++,
                }
            );
            var header = new PanelHeader(viewModel, PanelSizes.M);
            header.TitleButton.Visibility.ShouldBe(Visibility.Collapsed);
            var before = header.TapTargets.Count;

            viewModel.ApplyPicker(titleOpensPicker: true, pickerOpen: true);

            header.TitleButton.Visibility.ShouldBe(Visibility.Visible);
            header.TitleButton.Pattern.ShouldBe(ShortcutTilePattern.ExpandCollapse);
            header.TitleButton.IsExpanded.ShouldBeTrue();
            header.TitleButton.AccessibleHelpText.ShouldBe("Cambiar de perfil");
            header.TapTargets.Count.ShouldBe(before + 1);
            header.TapTargets[0].Tap();
            taps.ShouldBe(1);
            // PAN-004: the title still drags the panel.
            header.DragZones.Count.ShouldBe(2);
        });

    private static void WithBody(
        PanelLayoutSettings layout,
        PanelBodyContext context,
        Action<PanelBodyView, PanelViewModel> check
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark);
            var panel = PanelBodyTestData.Panel(
                PanelBodyTestData.Library(3, 0),
                PanelBodyTestData.Word,
                new RecordingBodyIntents()
            );
            panel.ApplyLayout(layout);
            panel.ApplyContext(context);
            var body = new PanelBodyView(panel);
            var root = new StackPanel();
            root.Children.Add(body);
            theme.Attach(root);
            try
            {
                Layout(root);
                check(body, panel);
            }
            finally
            {
                body.Detach();
                theme.Detach(root);
            }
        });

    private static void Layout(FrameworkElement root)
    {
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
    }
}
