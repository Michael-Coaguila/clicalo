using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Catalog;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// REG-02 on the tile: its box, hit area and automation bounds never go below 44 × 44, while a smaller drawing stays
/// centered inside (ACC-002: «visual → táctil 44»).
/// </summary>
public sealed class TouchTargetTests
{
    [Fact]
    [Trait("Req", "REG-02")]
    public void The_minimum_comes_from_the_sizes_catalog() =>
        TouchTarget.MinimumSize.ShouldBe(PanelSizes.Layout.MinTouchTargetPx);

    [Fact]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-002")]
    public void A_tile_declared_smaller_is_laid_out_at_44_and_drawn_at_its_size() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Deshacer",
                Width = 32,
                Height = 30,
            };
            var host = Host(tile);

            tile.MinWidth.ShouldBe(TouchTarget.MinimumSize);
            tile.MinHeight.ShouldBe(TouchTarget.MinimumSize);
            tile.ActualWidth.ShouldBe(TouchTarget.MinimumSize);
            tile.ActualHeight.ShouldBe(TouchTarget.MinimumSize);
            var chrome = (Border)tile.Template.FindName(ShortcutTileTemplate.ChromePart, tile);
            chrome.ActualWidth.ShouldBe(32);
            chrome.ActualHeight.ShouldBe(30);
            chrome.TranslatePoint(default, tile).ShouldBe(new Point(6, 7));
            host.Children.Count.ShouldBe(1);
        });

    [Fact]
    [Trait("Req", "REG-02")]
    public void The_whole_target_hit_tests_to_the_tile_even_outside_the_drawing() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Deshacer",
                Width = 24,
                Height = 24,
            };
            Host(tile);

            var corner = VisualTreeHelper.HitTest(tile, new Point(1, 1));
            corner.ShouldNotBeNull();
            IsInside(corner.VisualHit, tile).ShouldBeTrue();
        });

    [Fact]
    [Trait("Req", "REG-02")]
    public void A_style_or_view_cannot_lower_the_minimum() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile { MinWidth = 10, MinHeight = 20 };

            tile.MinWidth.ShouldBe(TouchTarget.MinimumSize);
            tile.MinHeight.ShouldBe(TouchTarget.MinimumSize);

            tile.MinWidth = 60;
            tile.MinWidth.ShouldBe(60);
        });

    [Fact]
    [Trait("Req", "REG-02")]
    public void A_large_tile_is_drawn_at_its_full_size() =>
        WpfThread.Invoke(() =>
        {
            var tile = new ShortcutTile
            {
                AccessibleName = "Copiar",
                Width = 92,
                Height = 78,
            };
            Host(tile);

            var chrome = (Border)tile.Template.FindName(ShortcutTileTemplate.ChromePart, tile);
            chrome.ActualWidth.ShouldBe(92);
            chrome.ActualHeight.ShouldBe(78);
        });

    [Fact]
    [Trait("Req", "REG-02")]
    public void An_empty_box_still_claims_a_whole_target() =>
        WpfThread.Invoke(() =>
        {
            var box = new TouchTargetBox();
            box.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));

            box.DesiredSize.ShouldBe(new Size(TouchTarget.MinimumSize, TouchTarget.MinimumSize));
            TouchTarget.IsLargeEnough(box.DesiredSize).ShouldBeTrue();
            TouchTarget.IsLargeEnough(new Size(43.9, 50)).ShouldBeFalse();
            TouchTarget.AtLeastMinimum(12).ShouldBe(TouchTarget.MinimumSize);
        });

    private static Canvas Host(UIElement element)
    {
        var host = new Canvas();
        host.Children.Add(element);
        host.Measure(new Size(400, 400));
        host.Arrange(new Rect(0, 0, 400, 400));
        host.UpdateLayout();
        return host;
    }

    private static bool IsInside(DependencyObject hit, DependencyObject ancestor)
    {
        for (
            DependencyObject? current = hit;
            current is not null;
            current = VisualTreeHelper.GetParent(current)
        )
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }
}
