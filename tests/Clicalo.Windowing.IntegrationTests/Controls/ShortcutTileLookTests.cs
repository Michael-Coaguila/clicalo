using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.Windowing.IntegrationTests.Controls;

/// <summary>
/// The tile with the prototype's look (CUA-007, CUA-009, TEM-003): icon in the category tint, bold name, key line in
/// JetBrains Mono, badge with the type or the state (ACC-003) and the visual states. The accessibility contract of
/// the tile is <c>ShortcutTilePeerTests</c>.
/// </summary>
public sealed class ShortcutTileLookTests
{
    [Fact]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "TEM-003")]
    public void The_icon_takes_the_tint_of_the_category_and_follows_it() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var icon = Part<SymbolIcon>(tile, ShortcutTileTemplate.IconPart);

            icon.Symbol.ShouldBe("content_copy");
            icon.Size.ShouldBe(28);
            Color(icon.Foreground).ShouldBe(ThemePalettes.Dark.GetCategoryTint(CategoryToken.Edit));

            tile.Category = CategoryToken.Voice;

            Color(icon.Foreground)
                .ShouldBe(ThemePalettes.Dark.GetCategoryTint(CategoryToken.Voice));
        });

    [Fact]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "TEM-005")]
    public void The_key_line_is_monospaced_and_hidden_when_empty() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var keys = Part<TextBlock>(tile, ShortcutTileTemplate.KeysPart);

            keys.Text.ShouldBe("Ctrl + C");
            keys.FontFamily.ShouldBeSameAs(AppFonts.Mono);
            keys.FontSize.ShouldBe(11);
            Part<TextBlock>(tile, ShortcutTileTemplate.LabelPart)
                .FontFamily.ShouldBeSameAs(AppFonts.Ui);

            tile.Keys = string.Empty;
            tile.UpdateLayout();

            keys.Visibility.ShouldBe(Visibility.Collapsed);
        });

    [Fact]
    [Trait("Req", "CUA-007")]
    [Trait("Req", "ACC-003")]
    public void The_badge_shows_the_type_and_the_state_wins_over_it() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var badge = Part<Border>(tile, ShortcutTileTemplate.BadgePart);
            var text = Part<TextBlock>(tile, ShortcutTileTemplate.StatePart);

            badge.Visibility.ShouldBe(Visibility.Collapsed);

            tile.Badge = "ALTERNAR";
            tile.UpdateLayout();
            badge.Visibility.ShouldBe(Visibility.Visible);
            text.Text.ShouldBe("ALTERNAR");
            text.FontSize.ShouldBeGreaterThanOrEqualTo(11, "TEM-007: no text below 11");

            tile.AccessibleState = "ACTIVO";
            tile.UpdateLayout();
            text.Text.ShouldBe("ACTIVO");
            AutomationProperties.GetItemStatus(tile).ShouldBeEmpty("the peer reads the state");

            tile.AccessibleState = string.Empty;
            tile.UpdateLayout();
            text.Text.ShouldBe("ALTERNAR");
        });

    [Fact]
    [Trait("Req", "CUA-009")]
    public void An_active_tile_has_the_category_wash_and_a_2_px_tint_outline() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            tile.Pattern = ShortcutTilePattern.Toggle;
            using var service = BaseControlTests.Host(tile);
            var chrome = Part<Border>(tile, ShortcutTileTemplate.ChromePart);

            Color(chrome.Background).ShouldBe(ThemePalettes.Dark.Card);

            tile.ToggleState = ToggleState.On;

            Color(chrome.Background)
                .ShouldBe(ThemePalettes.Dark.GetCategoryWash(CategoryToken.Edit));
            Color(chrome.BorderBrush)
                .ShouldBe(ThemePalettes.Dark.GetCategoryTint(CategoryToken.Edit));
            chrome.BorderThickness.ShouldBe(
                new Thickness(ShortcutTileTemplate.ActiveBorderThickness)
            );
        });

    [Fact]
    [Trait("Req", "CUA-009")]
    public void Held_armed_and_flashing_tiles_look_as_the_prototype() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var chrome = Part<Border>(tile, ShortcutTileTemplate.ChromePart);
            var wash = ThemePalettes.Dark.GetCategoryWash(CategoryToken.Edit);

            tile.IsHeld = true;
            var scale = chrome.RenderTransform.ShouldBeOfType<ScaleTransform>();
            scale.ScaleX.ShouldBe(ShortcutTileTemplate.HeldScale);
            Color(chrome.Background).ShouldBe(wash);

            tile.IsHeld = false;
            tile.IsArmed = true;
            Color(chrome.BorderBrush).ShouldBe(ThemePalettes.Dark.Warn);
            Color(chrome.Background).ShouldBe(wash);

            tile.IsArmed = false;
            tile.IsFlashing = true;
            Color(chrome.Background).ShouldBe(wash);
            chrome.RenderTransform.ShouldNotBeOfType<ScaleTransform>();
        });

    [Fact]
    [Trait("Req", "ACC-009")]
    public void The_voice_number_is_dark_text_on_warn() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            tile.VoiceNumber = 4;
            using var service = BaseControlTests.Host(tile);
            var badge = Part<Border>(tile, ShortcutTileTemplate.VoiceNumberPart);

            badge.Visibility.ShouldBe(Visibility.Visible);
            Color(badge.Background).ShouldBe(ThemePalettes.Dark.Warn);
            Color(((TextBlock)badge.Child).Foreground).ShouldBe(ThemePalettes.Dark.OnWarn);
        });

    private static ShortcutTile Tile() =>
        new()
        {
            AccessibleName = "Copiar",
            Symbol = "content_copy",
            Keys = "Ctrl + C",
            Width = 92,
            Height = 78,
            FontSize = 14,
        };

    private static T Part<T>(ShortcutTile tile, string name)
        where T : FrameworkElement => (T)tile.Template.FindName(name, tile);

    private static Color Color(Brush? brush) => ((SolidColorBrush)brush!).Color;
}
