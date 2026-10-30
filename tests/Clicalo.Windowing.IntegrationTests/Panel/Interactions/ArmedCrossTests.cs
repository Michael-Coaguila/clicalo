using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Surfaces.Panel.EditMode;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.MinimalPanel;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// The × of edit mode armed on the tiles of the grid (CUA-012, REG-04), headless (built and laid out on the WPF
/// thread; never shown): «Confirmar» grows to the left from the right edge of the ×, stays inside its tile and is
/// written whole in the font of the name of the tile.
/// </summary>
public sealed class ArmedCrossTests
{
    [Theory]
    [InlineData(PanelSize.Small, false, "es")]
    [InlineData(PanelSize.Small, false, "en")]
    [InlineData(PanelSize.Medium, false, "es")]
    [InlineData(PanelSize.Large, false, "en")]
    [InlineData(PanelSize.Small, true, "es")]
    [InlineData(PanelSize.Small, true, "en")]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "REG-04")]
    public void Armed_the_cross_grows_to_the_left_inside_its_tile(
        PanelSize size,
        bool compact,
        string language
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                reduceMotion: true
            );
            var world = new InteractionsWorld(language);
            var panel = PanelBodyTestData.Panel(
                PanelBodyTestData.GalleryLibrary(),
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                language,
                PanelBodyTestData.GalleryKeys
            );
            panel.ApplyLayout(
                PanelLayoutSettings.Default with
                {
                    Size = size,
                    Compact = compact,
                    VoiceNumbers = true,
                }
            );
            var body = new PanelBodyView(panel);
            body.AttachLayers(
                new PanelLayerModels(
                    world.QuickSettings,
                    world.EditMode,
                    world.Menu,
                    world.TestMode,
                    world.Modes,
                    static () => false
                )
            );
            var root = new StackPanel();
            root.Children.Add(body);
            theme.Attach(root);
            try
            {
                world.EditMode.Enter();
                panel.ApplyContext(PanelBodyContext.Idle with { EditMode = true, AddTile = true });
                Layout(root);
                var tiles = body.ShortcutGrid.TileControls;
                tiles.ShouldNotBeEmpty();
                foreach (var tile in tiles)
                {
                    var control = tile.Control;
                    var at = Cross(control);
                    var rest = Box(at, control);

                    world.EditMode.Remove(tile.ViewModel.Id);
                    Layout(root);

                    var cross = Cross(control);
                    cross.Content.ShouldNotBeNull(control.AccessibleName);
                    var armed = Box(cross, control);
                    var name = control.AccessibleName;
                    armed.Left.ShouldBeGreaterThanOrEqualTo(Space.S1 - 0.5, name);
                    armed.Right.ShouldBeLessThanOrEqualTo(
                        control.ActualWidth - Space.S1 + 0.5,
                        name
                    );
                    // At rest the 44 × 44 box of the × reaches past its drawing; armed, the pill is its own box.
                    armed.Right.ShouldBe(
                        rest.Right - TileRemoveButton.Overflow,
                        0.5,
                        $"{name}: it grows to the left from the right edge of the ×"
                    );
                    armed.Top.ShouldBe(rest.Top, 0.5, name);
                    cross.FontSize.ShouldBe(control.FontSize, $"{name}: the font of the name");
                    var text = Descendants(cross).OfType<TextBlock>().Single();
                    text.ActualWidth.ShouldBeGreaterThanOrEqualTo(
                        Natural(text) - 0.5,
                        $"{name}: «{text.Text}» is cut"
                    );
                }
            }
            finally
            {
                body.Detach();
                theme.Detach(root);
            }
        });

    [Theory]
    [InlineData(PanelSize.Small, false, "es")]
    [InlineData(PanelSize.Small, true, "es")]
    [InlineData(PanelSize.Medium, true, "en")]
    [InlineData(PanelSize.Medium, false, "es")]
    [InlineData(PanelSize.Large, false, "en")]
    [Trait("Req", "CUA-012")]
    [Trait("Req", "CUA-011")]
    public void Armed_the_pill_grows_with_its_text_and_stays_inside_its_tile(
        PanelSize size,
        bool compact,
        string language
    )
    {
        foreach (var scale in new[] { TypeScale.MinScalePercent, TypeScale.MaxScalePercent })
        {
            WpfThread.Invoke(() =>
            {
                using var theme = new ThemeService(
                    new FakeSystemTheme(),
                    ThemeChoice.Dark,
                    scale,
                    reduceMotion: true
                );
                var world = new InteractionsWorld(language);
                var panel = PanelBodyTestData.Panel(
                    PanelBodyTestData.GalleryLibrary(),
                    PanelBodyTestData.Word,
                    new RecordingBodyIntents(),
                    language,
                    PanelBodyTestData.GalleryKeys
                );
                panel.ApplyLayout(
                    PanelLayoutSettings.Default with
                    {
                        Size = size,
                        Compact = compact,
                        VoiceNumbers = true,
                        TextScalePercent = scale,
                    }
                );
                var body = new PanelBodyView(panel);
                body.AttachLayers(
                    new PanelLayerModels(
                        world.QuickSettings,
                        world.EditMode,
                        world.Menu,
                        world.TestMode,
                        world.Modes,
                        static () => false
                    )
                );
                var root = new StackPanel();
                root.Children.Add(body);
                theme.Attach(root);
                try
                {
                    world.EditMode.Enter();
                    panel.ApplyContext(
                        PanelBodyContext.Idle with
                        {
                            EditMode = true,
                            AddTile = true,
                        }
                    );
                    Layout(root);
                    var tiles = body.ShortcutGrid.TileControls;
                    tiles.ShouldNotBeEmpty();
                    foreach (var tile in tiles)
                    {
                        var control = tile.Control;
                        world.EditMode.Remove(tile.ViewModel.Id);
                        Layout(root);

                        var cross = Cross(control);
                        var what = FormattableString.Invariant(
                            $"{control.AccessibleName} at {scale} %"
                        );
                        var pill = PillOf(cross, control);
                        var text = Descendants(cross).OfType<TextBlock>().Single();
                        var line = Box(text, control);

                        // Its line of text and Stroke.Selected above and below it; the 24 of the × at 100 %.
                        pill.Height.ShouldBeGreaterThanOrEqualTo(
                            text.ActualHeight + (2 * Stroke.Selected) - 0.5,
                            what
                        );
                        if (scale == TypeScale.MinScalePercent)
                        {
                            pill.Height.ShouldBe(IconBox.Medium, 0.5, what);
                        }

                        line.Top.ShouldBeGreaterThanOrEqualTo(
                            pill.Top + Stroke.Selected - 0.5,
                            what
                        );
                        line.Bottom.ShouldBeLessThanOrEqualTo(
                            pill.Bottom - Stroke.Selected + 0.5,
                            what
                        );
                        line.Left.ShouldBeGreaterThanOrEqualTo(pill.Left + Space.S2 - 0.5, what);
                        line.Right.ShouldBeLessThanOrEqualTo(pill.Right - Space.S2 + 0.5, what);
                        text.ActualWidth.ShouldBeGreaterThanOrEqualTo(
                            Natural(text) - 0.5,
                            $"{what}: «{text.Text}» is cut"
                        );

                        // Its top keeps the corner of the ×, and it never leaves its tile.
                        pill.Top.ShouldBe(Space.S1, 0.5, what);
                        pill.Right.ShouldBe(control.ActualWidth - Space.S1, 0.5, what);
                        pill.Left.ShouldBeGreaterThanOrEqualTo(Space.S1 - 0.5, what);
                        pill.Bottom.ShouldBeLessThanOrEqualTo(
                            control.ActualHeight - Space.S1 + 0.5,
                            what
                        );
                    }
                }
                finally
                {
                    body.Detach();
                    theme.Detach(root);
                }
            });
        }
    }

    /// <summary>The drawing of the ×, centred in its 44 × 44 box (REG-02): what the eye sees of the pill.</summary>
    private static Rect PillOf(FrameworkElement cross, Visual tile)
    {
        var box = (Decorator)VisualTreeHelper.GetChild(cross, 0);
        var drawing = (FrameworkElement)box.Child;
        return Box(drawing, tile);
    }

    /// <summary>The × of <paramref name="tile"/>: it shares the cell of the tile.</summary>
    private static TileRemoveButton Cross(FrameworkElement tile) =>
        ((Panel)tile.Parent).Children.OfType<TileRemoveButton>().Single();

    /// <summary>The box of <paramref name="part"/> on its tile, without the margin that reaches past it.</summary>
    private static Rect Box(FrameworkElement part, Visual tile) =>
        part.TransformToVisual(tile).TransformBounds(new Rect(part.RenderSize));

    private static double Natural(TextBlock text)
    {
        var whole = new TextBlock
        {
            Text = text.Text,
            FontFamily = text.FontFamily,
            FontSize = text.FontSize,
            FontWeight = text.FontWeight,
            FontStretch = text.FontStretch,
            FontStyle = text.FontStyle,
        };
        whole.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return whole.DesiredSize.Width;
    }

    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            yield return child;
            foreach (var below in Descendants(child))
            {
                yield return below;
            }
        }
    }

    private static void Layout(FrameworkElement root)
    {
        root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        root.Arrange(new Rect(root.DesiredSize));
        root.UpdateLayout();
    }
}
