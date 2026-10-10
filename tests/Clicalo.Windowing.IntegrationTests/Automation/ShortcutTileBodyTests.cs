using System.Windows;
using System.Windows.Controls;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.Windowing.IntegrationTests.Automation.Audit;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// CUA-011 and EC-PAN-03 on the real tile: a long name at the largest text takes two lines at most and ends in «…»,
/// the key line shows only while it fits under it, and nothing of the tile is drawn cut.
/// </summary>
public sealed class ShortcutTileBodyTests
{
    private const string LongName = "Captura de pantalla de la ventana activa";
    private const double Rounding = 0.5;

    [Theory]
    [Trait("Req", "CUA-011")]
    [Trait("Req", "CUA-007")]
    // S at 150 %: 66 + 0.5 × 12 × 1.6 high, the name in 18 px and the keys in 14: one line of the name.
    [InlineData(72, 75.6, 20, 18, 14, 1, null)]
    // S at 100 %: the two lines of the name and no room for the keys.
    [InlineData(72, 66, 20, 12, 11, 2, false)]
    // L at 150 %: the two lines of the name and no room for the keys.
    [InlineData(116, 110.8, 34, 24, 17, 2, false)]
    // A tile high enough for the three.
    [InlineData(116, 140, 34, 24, 17, 2, true)]
    // A shortcut of a side bar of the Tab view in S (PES-007): one line.
    [InlineData(60, 50, 20, 11, 11, 1, false)]
    public void A_long_name_takes_two_lines_at_most_and_nothing_of_the_tile_is_cut(
        double width,
        double height,
        double icon,
        double namePx,
        double keysPx,
        int nameLines,
        bool? keysShow
    ) =>
        WpfThread.Invoke(() =>
        {
            using var theme = AuditLooks.Theme(AuditLook.Dark);
            var tile = new ShortcutTile
            {
                Template = ShortcutTileTemplate.Default,
                Width = width,
                Height = height,
                Padding = new Thickness(3),
                IconSize = icon,
                FontSize = namePx,
                KeysFontSize = keysPx,
                Symbol = "keyboard",
                AccessibleName = LongName,
                Keys = "Ctrl+Alt+Shift+S",
            };
            theme.Attach(tile);
            using var host = new AuditHost(tile);
            host.LayOut();

            var name = Part<TextBlock>(tile, ShortcutTileTemplate.LabelPart);
            var keys = Part<TextBlock>(tile, ShortcutTileTemplate.KeysPart);
            var line = name.FontFamily.LineSpacing * name.FontSize;

            SurfaceAudit.CutTexts(host.Root).ShouldBeEmpty();
            name.ActualHeight.ShouldBe(nameLines * line, Rounding);
            if (keysShow is { } show)
            {
                (keys.Opacity > 0 && keys.ActualHeight > 0).ShouldBe(show);
            }
        });

    [Fact]
    [Trait("Req", "CUA-007")]
    public void A_short_name_and_its_keys_show_whole_as_before() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                Template = ShortcutTileTemplate.Default,
                Width = 92,
                Height = 78,
                Padding = new Thickness(4),
                IconSize = 28,
                FontSize = 14,
                KeysFontSize = 11,
                Symbol = "content_copy",
                AccessibleName = "Copiar",
                Keys = "Ctrl+C",
            };
            using var theme = AuditLooks.Theme(AuditLook.Dark);
            theme.Attach(tile);
            using var host = new AuditHost(tile);
            host.LayOut();

            var name = Part<TextBlock>(tile, ShortcutTileTemplate.LabelPart);
            var keys = Part<TextBlock>(tile, ShortcutTileTemplate.KeysPart);

            SurfaceAudit.CutTexts(host.Root).ShouldBeEmpty();
            name.ActualHeight.ShouldBe(name.FontFamily.LineSpacing * name.FontSize, Rounding);
            keys.Opacity.ShouldBe(1);
            keys.ActualHeight.ShouldBeGreaterThan(0);

            // One under the other, in their order.
            var top = name.TranslatePoint(default, tile).Y;
            keys.TranslatePoint(default, tile)
                .Y.ShouldBeGreaterThanOrEqualTo(top + name.ActualHeight);
        });

    private static T Part<T>(ShortcutTile tile, string name)
        where T : FrameworkElement => (T)tile.Template.FindName(name, tile);
}
