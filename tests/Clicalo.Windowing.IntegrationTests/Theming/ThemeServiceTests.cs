using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;

namespace Clicalo.Windowing.IntegrationTests.Theming;

/// <summary>
/// The theme service (blueprint §8.4): «Auto» follows the light or dark mode of Windows live, a Windows contrast
/// theme always wins (TEM-001), a change repaints every attached window in place (AJR-004), and the resources carry
/// the bundled fonts, the type scale with the text scale (CUA-011) and reduce motion (TEM-006). The system is a fake:
/// nothing here reads or changes the real Windows settings, except the read-only check of the Windows source.
/// </summary>
public sealed class ThemeServiceTests
{
    [Theory]
    [Trait("Req", "TEM-001")]
    [InlineData(ThemeChoice.Auto, false, false, ThemeId.Dark)]
    [InlineData(ThemeChoice.Auto, true, false, ThemeId.Light)]
    [InlineData(ThemeChoice.Dark, true, false, ThemeId.Dark)]
    [InlineData(ThemeChoice.Light, false, false, ThemeId.Light)]
    [InlineData(ThemeChoice.HighContrast, true, false, ThemeId.HighContrast)]
    [InlineData(ThemeChoice.Auto, true, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeChoice.Auto, false, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeChoice.Dark, false, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeChoice.Light, true, true, ThemeId.SystemHighContrast)]
    [InlineData(ThemeChoice.HighContrast, false, true, ThemeId.SystemHighContrast)]
    public void Auto_follows_Windows_and_a_contrast_theme_always_wins(
        ThemeChoice preference,
        bool appsUseLightTheme,
        bool highContrast,
        ThemeId expected
    ) =>
        ThemeService
            .Resolve(preference, new SystemThemeState(appsUseLightTheme, highContrast, true))
            .ShouldBe(expected);

    [Fact]
    [Trait("Req", "TEM-001")]
    public void An_unknown_choice_is_refused() =>
        Should.Throw<ArgumentOutOfRangeException>(() =>
            ThemeService.Resolve((ThemeChoice)42, default)
        );

    [Fact]
    [Trait("Req", "TEM-001")]
    [Trait("Req", "AJR-004")]
    public void Auto_switches_live_when_Windows_changes_its_app_mode() =>
        WpfThread.Invoke(() =>
        {
            var system = new FakeSystemTheme();
            using var service = new ThemeService(system);
            var (root, card) = Host(service);
            var changes = 0;
            service.Changed += (_, _) => changes++;

            service.Effective.ShouldBe(ThemeId.Dark);
            Color(card).ShouldBe(ThemePalettes.Dark.Card);

            system.Change(system.State with { AppsUseLightTheme = true });
            WpfThread.DrainPendingWork();

            service.Effective.ShouldBe(ThemeId.Light);
            Color(card).ShouldBe(ThemePalettes.Light.Card);
            changes.ShouldBe(1);
            root.Resources.MergedDictionaries.ShouldContain(service.Resources);
        });

    [Fact]
    [Trait("Req", "TEM-001")]
    public void A_contrast_theme_turned_on_in_Windows_wins_over_an_explicit_choice() =>
        WpfThread.Invoke(() =>
        {
            var system = new FakeSystemTheme();
            using var service = new ThemeService(system, ThemeChoice.Light);
            var (_, card) = Host(service);

            system.Change(system.State with { HighContrast = true });
            WpfThread.DrainPendingWork();

            service.Effective.ShouldBe(ThemeId.SystemHighContrast);
            service.Palette.IsHighContrast.ShouldBeTrue();
            Color(card).ShouldBe(service.Palette.Card);
            service
                .Resources[ThemeScope.BorderThicknessKey]
                .ShouldBe(new Thickness(service.Palette.BorderThickness));

            system.Change(system.State with { HighContrast = false });
            WpfThread.DrainPendingWork();

            service.Effective.ShouldBe(ThemeId.Light);
            Color(card).ShouldBe(ThemePalettes.Light.Card);
        });

    [Fact]
    [Trait("Req", "AJR-004")]
    public void Changing_the_choice_repaints_in_place_and_the_same_choice_does_nothing() =>
        WpfThread.Invoke(() =>
        {
            using var service = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark);
            var (_, card) = Host(service);
            var changes = 0;
            service.Changed += (_, _) => changes++;

            service.Preference = ThemeChoice.Dark;
            service.Preference = ThemeChoice.HighContrast;

            changes.ShouldBe(1);
            service.Effective.ShouldBe(ThemeId.HighContrast);
            Color(card).ShouldBe(ThemePalettes.HighContrast.Card);
        });

    [Fact]
    [Trait("Req", "TEM-002")]
    [Trait("Req", "TEM-003")]
    [Trait("Req", "TEM-005")]
    public void The_resources_are_frozen_and_cover_every_token_category_and_font() =>
        WpfThread.Invoke(() =>
        {
            using var service = new ThemeService(new FakeSystemTheme(), ThemeChoice.Light);
            var resources = service.Resources;
            var palette = ThemePalettes.Light;

            foreach (var token in Enum.GetValues<ColorToken>())
            {
                var brush = resources[ThemeBrushKey.For(token)].ShouldBeOfType<SolidColorBrush>();
                brush.IsFrozen.ShouldBeTrue();
                brush.Color.ShouldBe(palette.GetColor(token), token.ToString());
            }

            foreach (var category in Enum.GetValues<CategoryToken>())
            {
                var tint = resources[CategoryBrushKey.Tint(category)]
                    .ShouldBeOfType<SolidColorBrush>();
                var wash = resources[CategoryBrushKey.Wash(category)]
                    .ShouldBeOfType<SolidColorBrush>();
                tint.IsFrozen.ShouldBeTrue();
                wash.IsFrozen.ShouldBeTrue();
                tint.Color.ShouldBe(palette.GetCategoryTint(category));
                wash.Color.ShouldBe(palette.GetCategoryWash(category));
            }

            resources[ThemeKeys.UiFont].ShouldBeSameAs(AppFonts.Ui);
            resources[ThemeKeys.MonoFont].ShouldBeSameAs(AppFonts.Mono);
            resources[ThemeKeys.SymbolsFont].ShouldBeSameAs(AppFonts.Symbols);
            resources[ThemeKeys.SymbolsFilledFont].ShouldBeSameAs(AppFonts.SymbolsFilled);
            foreach (var value in resources.Values)
            {
                if (value is Freezable freezable)
                {
                    freezable.IsFrozen.ShouldBeTrue();
                }
            }
        });

    [Fact]
    [Trait("Req", "TEM-005")]
    public void An_attached_window_inherits_the_interface_font_and_the_text_color() =>
        WpfThread.Invoke(() =>
        {
            using var service = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark);
            var (root, _) = Host(service);
            var text = new TextBlock { Text = "Listo" };
            root.Children.Add(text);

            text.FontFamily.ShouldBeSameAs(AppFonts.Ui);
            ((SolidColorBrush)text.Foreground).Color.ShouldBe(ThemePalettes.Dark.Text);

            service.Attach(root);
            service.Detach(root);

            root.Resources.MergedDictionaries.ShouldNotContain(service.Resources);
            root.ReadLocalValue(TextElement.FontFamilyProperty)
                .ShouldBe(DependencyProperty.UnsetValue);
        });

    [Theory]
    [Trait("Req", "CUA-011")]
    [Trait("Req", "TEM-007")]
    [InlineData(14, 100, 14)]
    [InlineData(14, 150, 21)]
    [InlineData(12, 110, 13)]
    [InlineData(13, 150, 20)]
    [InlineData(9, 100, 11)]
    [InlineData(10, 120, 12)]
    [InlineData(8, 150, 12)]
    public void The_text_scale_rounds_like_the_prototype_and_never_goes_below_11(
        double designPx,
        int percent,
        double expected
    ) => TypeScale.Scale(designPx, percent).ShouldBe(expected);

    [Fact]
    [Trait("Req", "CUA-011")]
    public void The_service_publishes_the_type_scale_and_the_scaled_sizes() =>
        WpfThread.Invoke(() =>
        {
            using var service = new ThemeService(new FakeSystemTheme(), textScalePercent: 100);

            service.TextScalePercent = 150;

            TypeScale.Steps.ShouldBe([11d, 12, 13, 14, 15, 16, 18, 20, 24, 28, 30, 40]);
            foreach (var step in TypeScale.Steps)
            {
                service.Resources[ThemeKeys.TextSize(step)].ShouldBe(step);
                service
                    .Resources[ThemeKeys.ScaledTextSize(step)]
                    .ShouldBe(TypeScale.Scale(step, 150));
            }

            service.Resources[ThemeKeys.TextScalePercent].ShouldBe(150);
            Should.Throw<ArgumentOutOfRangeException>(() => service.TextScalePercent = 160);
            Should.Throw<ArgumentOutOfRangeException>(() => service.TextScalePercent = 90);
            Should.Throw<ArgumentOutOfRangeException>(() => ThemeKeys.TextSize(17));
        });

    [Theory]
    [Trait("Req", "TEM-006")]
    [InlineData(false, true, false)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    public void Reduce_motion_is_the_app_setting_or_Windows_animations_off(
        bool preference,
        bool windowsAnimations,
        bool expected
    ) =>
        WpfThread.Invoke(() =>
        {
            var system = new FakeSystemTheme
            {
                State = new(false, false, ClientAreaAnimation: windowsAnimations),
            };
            using var service = new ThemeService(system, reduceMotion: preference);

            service.ReduceMotion.ShouldBe(expected);
            service.Resources[ThemeKeys.ReduceMotion].ShouldBe(expected);
        });

    [Fact]
    [Trait("Req", "TEM-001")]
    public void A_disposed_service_stops_listening_and_refuses_changes() =>
        WpfThread.Invoke(() =>
        {
            var system = new FakeSystemTheme();
            var service = new ThemeService(system);
            system.Listeners.ShouldBe(1);

            system.Change(system.State with { AppsUseLightTheme = true });
            service.Dispose();
            service.Dispose();
            WpfThread.DrainPendingWork();

            system.Listeners.ShouldBe(0);
            service.Effective.ShouldBe(ThemeId.Dark, "a change queued before Dispose is dropped");
            Should.Throw<ObjectDisposedException>(service.Refresh);
            Should.Throw<ObjectDisposedException>(() => service.Preference = ThemeChoice.Light);
        });

    [Fact]
    public void The_service_belongs_to_the_thread_that_created_it()
    {
        var service = WpfThread.Invoke(() => new ThemeService(new FakeSystemTheme()));

        Should.Throw<InvalidOperationException>(() => service.Preference = ThemeChoice.Light);
        WpfThread.Invoke(service.Dispose);
    }

    [Fact]
    [Trait("Req", "TEM-001")]
    public void The_Windows_source_reads_the_contrast_and_animation_settings_of_the_system() =>
        WpfThread.Invoke(() =>
        {
            using var source = new WindowsSystemThemeSource();

            var state = source.Read();

            state.HighContrast.ShouldBe(SystemParameters.HighContrast);
            state.ClientAreaAnimation.ShouldBe(SystemParameters.ClientAreaAnimation);
        });

    [Fact]
    [Trait("Req", "TEM-003")]
    public void A_theme_scope_publishes_the_same_category_and_font_resources() =>
        WpfThread.Invoke(() =>
        {
            var root = new Grid();
            using var scope = new ThemeScope(root, ThemeId.Dark);

            foreach (var key in CategoryBrushKey.All)
            {
                root.Resources[key].ShouldBeOfType<SolidColorBrush>().IsFrozen.ShouldBeTrue();
            }

            root.Resources[ThemeKeys.UiFont].ShouldBeSameAs(AppFonts.Ui);
            root.Resources[ThemeKeys.ScaledTextSize(14)].ShouldBe(14d);
            root.Resources[ThemeKeys.ReduceMotion].ShouldBe(false);
        });

    [Fact]
    public void Category_keys_are_one_per_category_and_kind()
    {
        CategoryBrushKey.All.Count().ShouldBe(2 * Enum.GetValues<CategoryToken>().Length);
        CategoryBrushKey
            .Tint(CategoryToken.Voice)
            .ShouldBeSameAs(CategoryBrushKey.Tint(CategoryToken.Voice));
        CategoryBrushKey
            .Tint(CategoryToken.Voice)
            .ShouldNotBe(CategoryBrushKey.Wash(CategoryToken.Voice));
        CategoryBrushKey.Wash(CategoryToken.Web).ToString().ShouldBe("Wash.Web");
        Should.Throw<ArgumentOutOfRangeException>(() => CategoryBrushKey.Tint((CategoryToken)99));
    }

    private static (Grid Root, Border Card) Host(ThemeService service)
    {
        var root = new Grid();
        service.Attach(root);
        var card = new Border();
        card.SetResourceReference(Border.BackgroundProperty, ThemeBrushKey.For(ColorToken.Card));
        root.Children.Add(card);
        return (root, card);
    }

    private static Color Color(Border border) => ((SolidColorBrush)border.Background).Color;
}
