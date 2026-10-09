using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Desktop;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.Controls;

/// <summary>
/// Render snapshots of the base controls and the tile in the three themes, at the 175 % of the target screen
/// (prototype v4, medium visual fidelity). Rendering depends on the machine's font rasterizer and GPU, so they run in
/// the nightly desktop run and never block (<c>Requires=Desktop</c>); accept new images with
/// <c>CLICALO_ACCEPT_SNAPSHOTS=1</c>.
/// </summary>
[Collection(DesktopCollectionDefinition.Name)]
[Trait("Requires", "Desktop")]
public sealed class BaseControlRenderTests
{
    private const double ScreenDpi = 168;

    [DesktopTheory]
    [Trait("Req", "TEM-001")]
    [Trait("Req", "TEM-005")]
    [Trait("Req", "CUA-007")]
    [InlineData(ThemeChoice.Dark)]
    [InlineData(ThemeChoice.Light)]
    [InlineData(ThemeChoice.HighContrast)]
    public void The_gallery_matches_its_snapshot(ThemeChoice theme) =>
        RenderSnapshot.Match(
            () => Gallery(theme),
            theme.ToString(),
            new RenderSnapshotOptions
            {
                Dpi = ScreenDpi,
                ChannelTolerance = 8,
                MaxDifferentPixelsPercent = 1,
            }
        );

    private static Border Gallery(ThemeChoice theme)
    {
        var system = new FakeSystemTheme();
        var service = new ThemeService(system, theme);
        var stack = new StackPanel { Margin = new Thickness(12) };
        var root = new Border { Width = 320, Child = stack };
        root.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Panel));
        service.Attach(root);

        var card = new Card { Content = Section(), Margin = new Thickness(0, 0, 0, 10) };
        stack.Children.Add(card);
        stack.Children.Add(Tiles());
        stack.Children.Add(Buttons());
        return root;
    }

    private static StackPanel Section()
    {
        var section = new StackPanel();
        var segmented = new SegmentedControl { Columns = 2, SelectedIndex = 1 };
        foreach (var label in new[] { "Auto", "Oscuro", "Claro", "Alto contraste" })
        {
            segmented.Items.Add(new SegmentedItem { Content = label });
        }

        segmented.SelectedIndex = 1;
        var slider = new StepSlider
        {
            Minimum = 30,
            Maximum = 100,
            SmallChange = 5,
            Value = 85,
            Margin = new Thickness(0, 10, 0, 6),
        };
        section.Children.Add(segmented);
        section.Children.Add(slider);
        section.Children.Add(
            new ToggleSwitch { Content = "Modo prueba (30 s)", Symbol = "science" }
        );
        section.Children.Add(
            new ToggleSwitch
            {
                Content = "Números para voz",
                Symbol = "pin",
                IsChecked = true,
            }
        );
        return section;
    }

    private static WrapPanel Tiles()
    {
        var tiles = new WrapPanel();
        tiles.Children.Add(Tile("Copiar", "content_copy", "Ctrl + C", CategoryToken.Edit, 1));
        var bold = Tile("Negrita", "format_bold", "Ctrl + N", CategoryToken.Fmt, 2);
        bold.Pattern = ShortcutTilePattern.Toggle;
        bold.ToggleState = ToggleState.On;
        bold.AccessibleState = "ACTIVO";
        tiles.Children.Add(bold);
        var web = Tile("Correo", "mail", "outlook.com", CategoryToken.Web, 3);
        web.Badge = "WEB";
        tiles.Children.Add(web);
        return tiles;
    }

    private static ShortcutTile Tile(
        string name,
        string symbol,
        string keys,
        CategoryToken category,
        int number
    ) =>
        new()
        {
            AccessibleName = name,
            Symbol = symbol,
            Keys = keys,
            Category = category,
            VoiceNumber = number,
            Width = 92,
            Height = 78,
            FontSize = 14,
            Margin = new Thickness(0, 0, 8, 8),
        };

    private static WrapPanel Buttons()
    {
        var buttons = new WrapPanel();
        buttons.Children.Add(
            new TouchButton
            {
                Content = "Crear perfil",
                Symbol = "add",
                Margin = new Thickness(0, 0, 6, 6),
            }
        );
        buttons.Children.Add(
            new TouchButton
            {
                Content = "Ahora no",
                Appearance = ButtonAppearance.Outline,
                Margin = new Thickness(0, 0, 6, 6),
            }
        );
        buttons.Children.Add(
            new TouchButton
            {
                Content = "Soltar todo",
                Appearance = ButtonAppearance.Danger,
                Symbol = "warning",
                Margin = new Thickness(0, 0, 6, 6),
            }
        );
        var search = new IconButton { Symbol = "search", Appearance = ButtonAppearance.Neutral };
        AutomationProperties.SetName(search, "Buscar");
        buttons.Children.Add(search);
        buttons.Children.Add(
            new Chip
            {
                Content = "Ctrl",
                IsMonospace = true,
                IsChecked = true,
            }
        );
        buttons.Children.Add(
            new Chip
            {
                Content = "Alt",
                IsMonospace = true,
                Margin = new Thickness(6, 0, 0, 0),
            }
        );
        return buttons;
    }
}
