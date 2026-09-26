using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Automation.Lab;
using Clicalo.Windowing.IntegrationTests.Automation.Rules;

namespace Clicalo.Windowing.IntegrationTests.Automation;

/// <summary>
/// The theme of a surface as resources (TEM-001, TEM-002): the effective theme follows the system contrast state,
/// and a change repaints in place without touching the UI Automation tree (S3). The real Windows contrast switch
/// is <see cref="HighContrastTests"/> (CI only).
/// </summary>
public sealed class ThemeScopeTests
{
    [Theory]
    [Trait("Req", "TEM-001")]
    [InlineData(ThemeId.Dark, false, ThemeId.Dark)]
    [InlineData(ThemeId.Light, false, ThemeId.Light)]
    [InlineData(ThemeId.HighContrast, false, ThemeId.HighContrast)]
    [InlineData(ThemeId.SystemHighContrast, false, ThemeId.HighContrast)]
    [InlineData(ThemeId.Dark, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeId.Light, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeId.HighContrast, true, ThemeId.SystemHighContrast)]
    public void A_Windows_contrast_theme_always_wins(
        ThemeId preferred,
        bool systemHighContrast,
        ThemeId expected
    ) => ThemeScope.Resolve(preferred, systemHighContrast).ShouldBe(expected);

    [Fact]
    [Trait("Req", "TEM-002")]
    public void Every_color_token_becomes_a_frozen_brush_of_the_effective_palette() =>
        WpfThread.Invoke(() =>
        {
            var root = new Grid();
            using var scope = new ThemeScope(root, ThemeId.Dark);
            var palette = ThemeCatalog.GetPalette(Effective(ThemeId.Dark));

            scope.Effective.ShouldBe(Effective(ThemeId.Dark));
            foreach (var token in Enum.GetValues<ColorToken>())
            {
                var brush = root.Resources[ThemeBrushKey.For(token)]
                    .ShouldBeOfType<SolidColorBrush>();
                brush.IsFrozen.ShouldBeTrue();
                brush.Color.ShouldBe(palette.GetColor(token), token.ToString());
            }

            root.Resources[ThemeScope.BorderThicknessKey]
                .ShouldBe(new Thickness(palette.BorderThickness));
        });

    [Fact]
    [Trait("Req", "TEM-001")]
    [Trait("Req", "REG-06")]
    public void Switching_to_high_contrast_repaints_the_tiles_and_keeps_the_automation_tree() =>
        WpfThread.Invoke(() =>
        {
            using var lab = new TileLab(ThemeId.Dark);
            lab.SetVoiceNumbers(true);
            lab.LayOut(new Size(340, 330));
            var before = UiaTreeText.Format(Snapshot(lab));
            var applied = 0;
            lab.Theme.Applied += (_, _) => applied++;
            var tile = lab.Tile("tile.bold");
            var chrome = (Border)tile.Template.FindName(ShortcutTileTemplate.ChromePart, tile);

            lab.Theme.Preferred = ThemeId.HighContrast;
            lab.LayOut(new Size(340, 330));

            applied.ShouldBe(1);
            lab.Theme.Effective.ShouldBe(Effective(ThemeId.HighContrast));
            ((SolidColorBrush)chrome.Background).Color.ShouldBe(
                ThemeCatalog.GetPalette(Effective(ThemeId.HighContrast)).Card
            );
            chrome.BorderThickness.ShouldBe(
                new Thickness(
                    ThemeCatalog.GetPalette(Effective(ThemeId.HighContrast)).BorderThickness
                )
            );
            UiaTreeText.Format(Snapshot(lab)).ShouldBe(before);
        });

    [Fact]
    [Trait("Req", "TEM-001")]
    public void Refresh_reapplies_and_a_disposed_scope_refuses_changes() =>
        WpfThread.Invoke(() =>
        {
            var root = new Grid();
            var scope = new ThemeScope(root, ThemeId.Light);
            var applied = 0;
            scope.Applied += (_, _) => applied++;

            scope.Refresh();
            scope.Preferred = ThemeId.Light;
            scope.Dispose();
            scope.Dispose();

            applied.ShouldBe(1, "setting the same preference does not repaint");
            Should.Throw<ObjectDisposedException>(scope.Refresh);
            Should.Throw<ObjectDisposedException>(() => scope.Preferred = ThemeId.Dark);
        });

    [Fact]
    public void The_scope_belongs_to_the_thread_of_its_root()
    {
        var root = WpfThread.Invoke(() => new Grid());

        Should.Throw<InvalidOperationException>(() => new ThemeScope(root, ThemeId.Dark));
    }

    [Fact]
    public void Brush_keys_are_one_per_token_and_compare_by_token()
    {
        ThemeBrushKey.All.Count().ShouldBe(Enum.GetValues<ColorToken>().Length);
        ThemeBrushKey.For(ColorToken.Accent).ShouldBeSameAs(ThemeBrushKey.For(ColorToken.Accent));
        ThemeBrushKey.For(ColorToken.Accent).ShouldNotBe(ThemeBrushKey.For(ColorToken.Text));
        ThemeBrushKey.For(ColorToken.Accent).Token.ShouldBe(ColorToken.Accent);
        ThemeBrushKey.For(ColorToken.Accent).Assembly.ShouldBe(typeof(ThemeScope).Assembly);
        Should.Throw<ArgumentOutOfRangeException>(() => ThemeBrushKey.For((ColorToken)(-1)));
    }

    private static ThemeId Effective(ThemeId preferred) =>
        ThemeScope.Resolve(preferred, SystemParameters.HighContrast);

    private static UiaNode Snapshot(TileLab lab) =>
        PeerSnapshot.CaptureElements("lab", [.. lab.Tiles, lab.Notice]);
}
