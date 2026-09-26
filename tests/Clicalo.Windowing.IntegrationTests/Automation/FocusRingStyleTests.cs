using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>TEM-009: the visible keyboard focus is a 3 px ring, 2 px outside the control, in the focusRing token.</summary>
public sealed class FocusRingStyleTests
{
    [Fact]
    [Trait("Req", "TEM-009")]
    public void The_ring_is_3_px_wide_2_px_outside_and_follows_the_corner() =>
        WpfThread.Invoke(() =>
        {
            var ring = LoadRing(FocusRingStyle.Create(10));

            ring.BorderThickness.ShouldBe(new Thickness(3));
            ring.Margin.ShouldBe(new Thickness(-5));
            ring.CornerRadius.ShouldBe(new CornerRadius(15));
            ring.IsHitTestVisible.ShouldBeFalse();
        });

    [Theory]
    [Trait("Req", "TEM-009")]
    [InlineData(ThemeId.Dark)]
    [InlineData(ThemeId.Light)]
    [InlineData(ThemeId.HighContrast)]
    public void The_ring_takes_the_focus_ring_color_of_the_theme(ThemeId theme) =>
        WpfThread.Invoke(() =>
        {
            var root = new Grid();
            using var scope = new ThemeScope(root, theme);
            var ring = LoadRing(FocusRingStyle.Tile);
            root.Children.Add(ring);

            ((SolidColorBrush)ring.BorderBrush).Color.ShouldBe(scope.Palette.FocusRing);
        });

    [Fact]
    [Trait("Req", "TEM-009")]
    public void The_shared_styles_are_sealed_and_reject_a_negative_radius()
    {
        FocusRingStyle.Tile.IsSealed.ShouldBeTrue();
        FocusRingStyle.Button.IsSealed.ShouldBeTrue();
        Should.Throw<ArgumentOutOfRangeException>(() => FocusRingStyle.Create(-1));
    }

    private static Border LoadRing(Style style)
    {
        var template = (ControlTemplate)
            style
                .Setters.OfType<Setter>()
                .Single(setter => setter.Property == Control.TemplateProperty)
                .Value;
        return template.LoadContent().ShouldBeOfType<Border>();
    }
}
