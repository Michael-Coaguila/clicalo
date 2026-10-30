using System.Windows;
using System.Windows.Automation;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel.ContextMenu;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Surfaces.Panel.QuickSettings;
using Clicalo.UI.Wpf.Surfaces.Panel.TestMode;
using Clicalo.Windowing.IntegrationTests.Controls;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// The components of Quick settings, edit mode, the context menu and test mode as WPF lays them out, headless (built,
/// measured and arranged on the WPF thread; never shown): they show what their view models say, every tappable element
/// is at least 44 × 44 and a tap target reaches the same view model as UI Automation.
/// </summary>
public sealed class InteractionViewsTests
{
    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-001")]
    public void The_sheet_shows_its_sections_only_while_open_and_every_target_is_44() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var sheet = new QuickSettingsSheet(world.QuickSettings);
            using var theme = BaseControlTests.Host(sheet);

            sheet.Visibility.ShouldBe(Visibility.Collapsed);
            sheet.TapTargets.ShouldBeEmpty();

            world.QuickSettings.Open();
            Layout(sheet);

            sheet.Visibility.ShouldBe(Visibility.Visible);
            AutomationProperties.GetName(sheet).ShouldBe("Ajustes rápidos");
            // A name on a plain Border never reached UI Automation: the sheet is a Group named [quick].
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(
                sheet
            );
            peer.GetAutomationControlType()
                .ShouldBe(System.Windows.Automation.Peers.AutomationControlType.Group);
            peer.GetName().ShouldBe("Ajustes rápidos");
            peer.IsKeyboardFocusable().ShouldBeFalse();
            var targets = sheet.TapTargets.ToList();
            // Card, 3 views, 3 sizes, 4 sides, 4 themes, − and +, 4 switches.
            targets.Count.ShouldBe(21);
            foreach (var target in targets.Where(static t => t.Element.ActualHeight > 0))
            {
                TouchTarget
                    .IsLargeEnough(
                        new Size(target.Element.ActualWidth, target.Element.ActualHeight)
                    )
                    .ShouldBeTrue(AutomationProperties.GetName(target.Element));
            }

            targets.Count(static t => t.Element.ActualHeight > 0).ShouldBe(17);
        });

    [Fact]
    [Trait("Req", "AJR-001")]
    public void In_two_columns_the_sheet_keeps_every_control_and_its_order() =>
        WpfThread.Invoke(() =>
        {
            // D21: beside the panel the sheet takes two columns, the first sections on the left and the theme and the
            // switches on the right; back in one column it is the same sheet. Its height is audited in the gallery.
            var world = new InteractionsWorld();
            var sheet = new QuickSettingsSheet(world.QuickSettings);
            var targets = sheet.TapTargets.Select(static t => t.Element).ToList();

            sheet.Columns = 2;
            sheet.Columns.ShouldBe(2);
            sheet.TapTargets.Select(static t => t.Element).ShouldBe(targets);

            sheet.Columns = 1;
            sheet.TapTargets.Select(static t => t.Element).ShouldBe(targets);
            Should.Throw<ArgumentOutOfRangeException>(() => sheet.Columns = 3);
        });

    [Fact]
    [Trait("Req", "AJR-001")]
    public void The_sheet_fits_the_free_space_and_never_gets_below_its_minimum() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var sheet = new QuickSettingsSheet(world.QuickSettings);

            sheet.FitHeight(300);
            sheet.MaxHeight.ShouldBe(300);
            sheet.FitHeight(2000);
            sheet.MaxHeight.ShouldBe(2000, "D21: as tall as the work area, no longer 62 % of it");
            sheet.FitHeight(10);
            sheet.MaxHeight.ShouldBe(120);
        });

    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "AJR-004")]
    public void A_tap_on_a_theme_option_saves_it_and_the_option_shows_selected() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var sheet = new QuickSettingsSheet(world.QuickSettings);
            using var theme = BaseControlTests.Host(sheet);
            world.QuickSettings.Open();
            Layout(sheet);

            var light = sheet.TapTargets.Single(static t =>
                string.Equals(
                    AutomationProperties.GetName(t.Element),
                    "Claro",
                    StringComparison.Ordinal
                )
            );
            light.Tap();

            world.Settings.Theme.ShouldBe(ThemeChoice.Light);
            ((SegmentedItem)light.Element).IsSelected.ShouldBeTrue();
            sheet.Visibility.ShouldBe(Visibility.Visible);
        });

    [Fact]
    [Trait("Req", "CUA-014")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-001")]
    public void The_menu_shows_its_header_and_rows_of_44() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var view = new TileContextMenuView(world.Menu);
            using var theme = BaseControlTests.Host(view);

            view.Visibility.ShouldBe(Visibility.Collapsed);
            world.Menu.Open(InteractionsWorld.Bold, "Negrita", "format_bold", inFrequents: true);
            Layout(view);

            view.Visibility.ShouldBe(Visibility.Visible);
            var rows = view.TapTargets.ToList();
            rows.Select(static r => AutomationProperties.GetName(r.Element))
                .ShouldBe(["Fijar en Frecuentes", "Quitar de Frecuentes", "Editar", "Cancelar"]);
            rows.ShouldAllBe(static r => r.Element.ActualHeight >= 44);
            // A name on a plain Border never reached UI Automation: the menu is a Group around its rows, with the tile
            // it belongs to as its help.
            var peer = System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(
                view
            );
            peer.GetAutomationControlType()
                .ShouldBe(System.Windows.Automation.Peers.AutomationControlType.Group);
            peer.GetName().ShouldBe(AutomationProperties.GetName(view));
            peer.GetName().ShouldNotBeNullOrWhiteSpace();
            peer.GetHelpText().ShouldBe("Negrita");

            rows[3].Tap();
            Layout(view);
            view.Visibility.ShouldBe(Visibility.Collapsed);
        });

    [Fact]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "REG-06")]
    public void The_cross_shows_in_edit_mode_names_its_tile_and_turns_into_confirm() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var tile = world.Tile(InteractionsWorld.Bold);
            var cross = new TileRemoveButton(world.EditMode, tile);
            var add = new AddTileView(world.EditMode);
            using var theme = BaseControlTests.Host(cross, add);

            cross.Visibility.ShouldBe(Visibility.Collapsed);
            add.Visibility.ShouldBe(Visibility.Collapsed);

            world.EditMode.Enter();
            Layout(cross);
            cross.Visibility.ShouldBe(Visibility.Visible);
            add.Visibility.ShouldBe(Visibility.Visible);
            AutomationProperties.GetName(cross).ShouldBe("Eliminar Negrita");
            TouchTarget
                .IsLargeEnough(new Size(cross.ActualWidth, cross.ActualHeight))
                .ShouldBeTrue();
            add.Label.Text.ShouldBe("Añadir");

            cross.TapTarget.Tap();
            cross
                .Content.ShouldBeOfType<System.Windows.Controls.TextBlock>()
                .Text.ShouldBe("Confirmar");
            AutomationProperties.GetItemStatus(cross).ShouldBe("Confirmar");

            cross.Detach();
            add.Detach();
        });

    [Fact]
    [Trait("Req", "TAC-008")]
    [Trait("Req", "CUA-009")]
    [Trait("Req", "ACC-001")]
    public void The_mark_and_the_indicator_follow_test_mode() =>
        WpfThread.Invoke(() =>
        {
            var world = new InteractionsWorld();
            var badge = new TestMarkBadge(world.TestMode, InteractionsWorld.Bold);
            var indicator = new TestModeIndicator(world.TestMode);
            using var theme = BaseControlTests.Host(badge, indicator);

            badge.Visibility.ShouldBe(Visibility.Collapsed);
            indicator.Visibility.ShouldBe(Visibility.Collapsed);

            world.TestMode.Start();
            world.TestMode.OnCounted(InteractionsWorld.Bold);

            indicator.Visibility.ShouldBe(Visibility.Visible);
            indicator.Text.ShouldBe("Modo prueba · 30 s");
            // ACC-001: its text is read as it is; a name on the band, a Border, never reached UI Automation.
            AutomationProperties.GetName(indicator).ShouldBeEmpty();
            badge.Visibility.ShouldBe(Visibility.Visible);
            badge.IsHitTestVisible.ShouldBeFalse();
            AutomationProperties.GetItemStatus(badge).ShouldBe("Toque registrado (no se envió)");

            world.TestMode.Stop();
            badge.Visibility.ShouldBe(Visibility.Collapsed);
            indicator.Visibility.ShouldBe(Visibility.Collapsed);
            badge.Detach();
            indicator.Detach();
        });

    private static void Layout(FrameworkElement element)
    {
        var root = (FrameworkElement)element.Parent;
        root.Measure(new Size(400, 2000));
        root.Arrange(new Rect(0, 0, 400, root.DesiredSize.Height));
        root.UpdateLayout();
    }
}
