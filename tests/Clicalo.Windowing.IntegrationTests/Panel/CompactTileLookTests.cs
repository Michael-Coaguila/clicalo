using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Library;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.Panel;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.UI.Wpf.Theming;
using Clicalo.Windowing.IntegrationTests.Theming;
using static System.FormattableString;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The short tiles of the Compact view, headless (built and laid out on the WPF thread; never shown): a voice number
/// centred down a tile sits beside its body whatever its digits, so it never covers the icon (ACC-009).
/// </summary>
public sealed class CompactTileLookTests
{
    [Theory]
    [InlineData(PanelSize.Small)]
    [InlineData(PanelSize.Medium)]
    [InlineData(PanelSize.Large)]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "VCO-001")]
    public void The_voice_number_and_the_icon_of_the_row_are_one_pair_centred_in_the_tile(
        PanelSize size
    ) =>
        WithBody(
            PanelLayoutSettings.Default with
            {
                Size = size,
                Compact = true,
                VoiceNumbers = true,
            },
            TypeScale.MinScalePercent,
            (body, _, _) =>
            {
                var tiles = body.Strip.TileControls.Select(static t => t.Control).ToList();
                tiles.ShouldNotBeEmpty();
                foreach (var tile in tiles)
                {
                    foreach (var number in new[] { 1, 15, 99 })
                    {
                        tile.VoiceNumber = number;
                        tile.UpdateLayout();
                        var badge = Bounds(Part(tile, ShortcutTileTemplate.VoiceNumberPart), tile);
                        var icon = Drawn(Part(tile, ShortcutTileTemplate.IconPart), tile);

                        badge.Right.ShouldBeLessThanOrEqualTo(
                            icon.Left,
                            Invariant($"{number} covers the icon")
                        );
                        var left = badge.Left;
                        var right = tile.ActualWidth - icon.Right;
                        Math.Abs(left - right)
                            .ShouldBeLessThanOrEqualTo(1, Invariant($"{number}: off centre"));
                        Part(tile, ShortcutTileTemplate.TypePart)
                            .Visibility.ShouldBe(Visibility.Collapsed);
                    }
                }
            }
        );

    private static void WithBody(
        PanelLayoutSettings layout,
        int scale,
        Action<PanelBodyView, PanelViewModel, ShortcutLibrary> check
    ) => WithBody(PanelBodyTestData.GalleryLibrary(), layout, scale, check);

    private static void WithBody(
        ShortcutLibrary library,
        PanelLayoutSettings layout,
        int scale,
        Action<PanelBodyView, PanelViewModel, ShortcutLibrary> check
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = new ThemeService(
                new FakeSystemTheme(),
                ThemeChoice.Dark,
                scale,
                reduceMotion: true
            );
            var panel = PanelBodyTestData.Panel(
                library,
                PanelBodyTestData.Word,
                new RecordingBodyIntents(),
                keys: PanelBodyTestData.GalleryKeys
            );
            panel.ApplyLayout(layout);
            var body = new PanelBodyView(panel);
            var root = new StackPanel();
            root.Children.Add(body);
            theme.Attach(root);
            try
            {
                root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                root.Arrange(new Rect(root.DesiredSize));
                root.UpdateLayout();
                check(body, panel, library);
            }
            finally
            {
                body.Detach();
                theme.Detach(root);
            }
        });

    private static FrameworkElement Part(ShortcutTile tile, string name) =>
        (FrameworkElement)tile.Template.FindName(name, tile);

    private static Rect Bounds(FrameworkElement part, Visual tile) =>
        part.TransformToAncestor(tile).TransformBounds(new Rect(part.RenderSize));

    /// <summary>The box of the glyph an icon draws, centred in its box.</summary>
    private static Rect Drawn(FrameworkElement icon, Visual tile)
    {
        var box = Bounds(icon, tile);
        var size = ((SymbolIcon)icon).Size;
        return new Rect(
            box.Left + ((box.Width - size) / 2),
            box.Top + ((box.Height - size) / 2),
            size,
            size
        );
    }
}
