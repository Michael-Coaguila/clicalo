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
/// The tile of direction C (D12; CUA-007, CUA-009, TEM-003): icon in the category tint in its fixed box, bold name,
/// key line in JetBrains Mono, reserved slots for the voice number and for the type or the state (ACC-003), and the
/// visual states, with «elegido» drawn as everywhere else. The accessibility contract of the tile is
/// <c>ShortcutTilePeerTests</c>.
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
            icon.Size.ShouldBe(IconBox.Medium);
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
    [Trait("Req", "EJE-010")]
    public void The_slot_shows_the_type_and_the_state_takes_the_key_line() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var type = Part<SymbolIcon>(tile, ShortcutTileTemplate.TypePart);
            var word = Part<TextBlock>(tile, ShortcutTileTemplate.StatePart);
            var keys = Part<TextBlock>(tile, ShortcutTileTemplate.KeysPart);

            word.Visibility.ShouldBe(Visibility.Collapsed);
            type.Visibility.ShouldBe(Visibility.Collapsed);

            tile.Badge = "WEB";
            tile.UpdateLayout();
            word.Visibility.ShouldBe(
                Visibility.Visible,
                "a tile without a type icon writes its badge"
            );
            word.Text.ShouldBe("WEB");
            word.FontSize.ShouldBeGreaterThanOrEqualTo(11, "TEM-007: no text below 11");

            tile.TypeSymbol = "toggle_on";
            tile.UpdateLayout();
            type.Visibility.ShouldBe(Visibility.Visible, "the type icon takes the slot");
            word.Visibility.ShouldBe(Visibility.Collapsed);

            tile.AccessibleState = "ACTIVO";
            tile.UpdateLayout();
            type.Visibility.ShouldBe(Visibility.Visible, "the state never covers the slot");
            word.Visibility.ShouldBe(Visibility.Collapsed);
            keys.Text.ShouldBe("ACTIVO", "the state takes the key line");
            keys.FontFamily.ShouldBeSameAs(AppFonts.Ui, "a state is words, not keys");
            Color(keys.Foreground).ShouldBe(ThemePalettes.Dark.AccentInk);
            AutomationProperties.GetItemStatus(tile).ShouldBeEmpty("the peer reads the state");

            tile.AccessibleState = string.Empty;
            tile.UpdateLayout();
            keys.Text.ShouldBe("Ctrl + C");
            keys.FontFamily.ShouldBeSameAs(AppFonts.Mono);
        });

    [Fact]
    [Trait("Req", "ACC-003")]
    [Trait("Req", "EJE-010")]
    public void The_state_shows_without_keys_and_never_overlaps_the_icon_or_the_voice_number() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            tile.Keys = string.Empty;
            tile.VoiceNumber = 1;
            tile.TypeSymbol = "pan_tool";
            using var service = BaseControlTests.Host(tile);
            tile.AccessibleState = "Manteniendo";
            tile.IsHeld = true;
            tile.UpdateLayout();
            var keys = Part<TextBlock>(tile, ShortcutTileTemplate.KeysPart);
            var icon = Part<SymbolIcon>(tile, ShortcutTileTemplate.IconPart);
            var number = Part<Border>(tile, ShortcutTileTemplate.VoiceNumberPart);
            var type = Part<SymbolIcon>(tile, ShortcutTileTemplate.TypePart);

            keys.Visibility.ShouldBe(Visibility.Visible);
            keys.Text.ShouldBe("Manteniendo");
            type.IsFilled.ShouldBeTrue("held is the single look of «elegido»");
            Color(type.Foreground).ShouldBe(ThemePalettes.Dark.AccentInk);
            var state = Bounds(keys, tile);
            state.IntersectsWith(Bounds(icon, tile)).ShouldBeFalse();
            state.IntersectsWith(Bounds(number, tile)).ShouldBeFalse();
            state.IntersectsWith(Bounds(type, tile)).ShouldBeFalse();

            tile.IsHeld = false;
            tile.IsArmed = true;
            Color(keys.Foreground).ShouldBe(ThemePalettes.Dark.WarnText);
            Color(type.Foreground).ShouldBe(ThemePalettes.Dark.WarnText);
        });

    [Fact]
    [Trait("Req", "CUA-009")]
    public void An_active_tile_is_chosen_as_every_option_without_changing_size() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            tile.Pattern = ShortcutTilePattern.Toggle;
            using var service = BaseControlTests.Host(tile);
            var chrome = Part<Border>(tile, ShortcutTileTemplate.ChromePart);

            var ring = Part<System.Windows.Shapes.Rectangle>(tile, ShortcutTileTemplate.RingPart);
            var size = chrome.RenderSize;

            Color(chrome.Background).ShouldBe(ThemePalettes.Dark.Card);
            ring.Visibility.ShouldBe(Visibility.Collapsed);

            tile.ToggleState = ToggleState.On;
            tile.UpdateLayout();

            Color(chrome.Background).ShouldBe(ThemePalettes.Dark.AccentWash);
            ring.Visibility.ShouldBe(Visibility.Visible);
            Color(ring.Stroke).ShouldBe(ThemePalettes.Dark.Accent);
            ring.StrokeThickness.ShouldBe(ShortcutTileTemplate.ActiveBorderThickness);
            Part<SymbolIcon>(tile, ShortcutTileTemplate.IconPart).IsFilled.ShouldBeTrue();
            chrome.RenderSize.ShouldBe(size);
        });

    [Fact]
    [Trait("Req", "CUA-009")]
    public void Held_armed_and_flashing_tiles_look_as_the_prototype() =>
        WpfThread.Invoke(() =>
        {
            var tile = Tile();
            using var service = BaseControlTests.Host(tile);
            var chrome = Part<Border>(tile, ShortcutTileTemplate.ChromePart);
            var layers = Part<Grid>(tile, ShortcutTileTemplate.LayersPart);
            var ring = Part<System.Windows.Shapes.Rectangle>(tile, ShortcutTileTemplate.RingPart);
            var wash = ThemePalettes.Dark.AccentWash;

            tile.IsHeld = true;
            var scale = layers.RenderTransform.ShouldBeOfType<ScaleTransform>();
            scale.ScaleX.ShouldBe(ShortcutTileTemplate.HeldScale);
            Color(chrome.Background).ShouldBe(wash);

            tile.IsHeld = false;
            tile.IsArmed = true;
            Color(ring.Stroke).ShouldBe(ThemePalettes.Dark.WarnText);
            Color(chrome.Background).ShouldBe(ThemePalettes.Dark.WarnWash);

            tile.IsArmed = false;
            tile.IsFlashing = true;
            Color(chrome.Background).ShouldBe(wash);
            layers.RenderTransform.ShouldNotBeOfType<ScaleTransform>();
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

    private static Rect Bounds(FrameworkElement part, Visual tile) =>
        part.TransformToAncestor(tile).TransformBounds(new Rect(part.RenderSize));
}
