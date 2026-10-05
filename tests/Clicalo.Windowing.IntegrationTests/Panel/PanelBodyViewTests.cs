using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Application.Engine;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The body of the panel as WPF lays it out, headless (built, measured and arranged on the WPF thread; never shown):
/// it is as wide as PAN-002 says, the grid holds exactly its visible rows, every tappable element is at least 44 × 44
/// and the controls expose the patterns and states the view model gives them.
/// </summary>
public sealed class PanelBodyViewTests
{
    [Fact]
    [Trait("Req", "PAN-002")]
    [Trait("Req", "PAN-007")]
    [Trait("Req", "CUA-001")]
    public void The_body_is_as_wide_as_its_columns_and_the_grid_holds_its_rows_in_order() =>
        WithBody(
            PanelLayoutSettings.Default,
            PanelBodyContext.Idle,
            (body, panel) =>
            {
                body.ActualWidth.ShouldBe(316);
                body.ShortcutGrid.TileControls.Count.ShouldBe(9);
                body.ShortcutGrid.ActualHeight.ShouldBe(3 * (78 + 8));
                body.ShortcutGrid.TileControls[0].Control.AccessibleName.ShouldBe("Atajo 0");
                body.Strip.TileControls.Count.ShouldBe(5);
                body.Children.IndexOf(body.Strip)
                    .ShouldBeLessThan(body.Children.IndexOf(body.Selector));
                body.Children.IndexOf(body.Selector)
                    .ShouldBeLessThan(body.Children.IndexOf(body.GridArea));
                body.Children.IndexOf(body.Pager)
                    .ShouldBeLessThan(body.Children.IndexOf(body.Notices));
                body.Pager.Visibility.ShouldBe(Visibility.Visible);
                panel.Pager.PageCount.ShouldBe(3);
            }
        );

    [Fact]
    [Trait("Req", "REG-02")]
    [Trait("Req", "CUA-004")]
    public void Every_tappable_element_of_the_body_is_at_least_44_square() =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                StickyRow = true,
                Size = PanelSize.Small,
            },
            PanelBodyContext.Idle with
            {
                PickerOpen = true,
                ElevatedApp = "Taskmgr",
                CanRepeat = true,
                SuggestionApp = "Notion",
            },
            (body, _) =>
            {
                var targets = body.TapTargets.ToList();
                targets.Count.ShouldBeGreaterThan(12);
                targets.ShouldAllBe(static t =>
                    t.Element.ActualWidth >= 44 && t.Element.ActualHeight >= 44
                );
                body.TileControls.ShouldAllBe(static t => t.Control.ActualHeight >= 44);
            }
        );

    [Fact]
    [Trait("Req", "SEL-001")]
    [Trait("Req", "FIJ-005")]
    [Trait("Req", "ACC-009")]
    public void The_controls_expose_their_pattern_state_and_voice_number() =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                StickyRow = true,
                VoiceNumbers = true,
            },
            PanelBodyContext.Idle with
            {
                PickerOpen = true,
            },
            (body, panel) =>
            {
                panel.ApplyEngine(
                    EngineSnapshot.Empty with
                    {
                        Sticky = new StickyState(
                            StickyLevel.Locked,
                            StickyLevel.Once,
                            StickyLevel.Off,
                            StickyLevel.Off
                        ),
                    }
                );

                body.Selector.ProfileButton.Pattern.ShouldBe(ShortcutTilePattern.ExpandCollapse);
                body.Selector.ProfileButton.IsExpanded.ShouldBeTrue();
                body.Sticky.KeyControls.Select(static k => k.ToggleState)
                    .ShouldBe([
                        ToggleState.Indeterminate,
                        ToggleState.On,
                        ToggleState.Off,
                        ToggleState.Off,
                    ]);
                body.ShortcutGrid.TileControls[0].Control.AutomationName.ShouldBe("1 Atajo 0");
                body.Strip.TileControls[0].Control.VoiceNumber.ShouldBe(21);
            }
        );

    private static void WithBody(
        PanelLayoutSettings layout,
        PanelBodyContext context,
        Action<PanelBodyView, PanelViewModel> check
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark);
            var panel = PanelBodyTestData.Panel(
                PanelBodyTestData.Library(20, 5),
                PanelBodyTestData.Word,
                new RecordingBodyIntents()
            );
            panel.ApplyLayout(layout);
            panel.ApplyContext(context);
            var body = new PanelBodyView(panel);
            var root = new StackPanel();
            root.Children.Add(body.AdminNotice);
            root.Children.Add(body);
            root.SetResourceReference(
                StackPanel.BackgroundProperty,
                ThemeBrushKey.For(ColorToken.Panel)
            );
            theme.Attach(root);
            Layout(root);
            try
            {
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
