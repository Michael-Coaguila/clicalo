using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using Clicalo.Domain.Settings;
using Clicalo.TestKit.Windows.Rendering;
using Clicalo.UI.Wpf.Automation;
using Clicalo.UI.Wpf.Controls;
using Clicalo.UI.Wpf.Resources;
using Clicalo.UI.Wpf.Theming;
using Clicalo.UI.Wpf.Theming.Generated;
using Clicalo.Windowing.IntegrationTests.Theming;

namespace Clicalo.Windowing.IntegrationTests.Controls;

/// <summary>
/// The base controls with the prototype's look (button, icon button, switch, slider with − and +, segmented control,
/// chip and card): each one exposes its name, control type and pattern to UI Automation (ACC-001, REG-06), responds
/// on at least 44 × 44 (REG-02), shows the 3 px focus ring (TEM-009) and paints with the theme tokens, repainting in
/// place. Laid out without a window: nothing here needs a desktop.
/// </summary>
public sealed class BaseControlTests
{
    [Fact]
    [Trait("Req", "REG-02")]
    public void Every_base_control_responds_on_at_least_44_even_when_drawn_smaller() =>
        WpfThread.Invoke(() =>
        {
            var segmented = Segmented();
            var controls = new FrameworkElement[]
            {
                new TouchButton { Content = "Crear", Height = 36 },
                new IconButton
                {
                    Symbol = "search",
                    Width = 32,
                    Height = 32,
                },
                new ToggleSwitch { Content = "Modo prueba" },
                new Chip { Content = "Ctrl", IsMonospace = true },
                new StepSlider { Width = 220 },
                segmented,
                new ShortcutTile
                {
                    AccessibleName = "Copiar",
                    Width = 32,
                    Height = 30,
                },
            };
            using var service = Host(controls);

            foreach (var control in controls.Append(Item(segmented, 0)))
            {
                TouchTarget
                    .IsLargeEnough(new Size(control.ActualWidth, control.ActualHeight))
                    .ShouldBeTrue(control.GetType().Name);
            }
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-06")]
    public void A_button_is_an_invokable_Button_named_by_its_text() =>
        WpfThread.Invoke(() =>
        {
            var button = new TouchButton { Content = "Crear perfil", Symbol = "add" };
            var clicks = 0;
            button.Click += (_, _) => clicks++;
            using var service = Host(button);
            var peer = PeerOf(button);

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetName().ShouldBe("Crear perfil");
            peer.GetClassName().ShouldBe(nameof(TouchButton));
            peer.GetChildren().ShouldBeNull();
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
            WpfThread.DrainPendingWork();

            clicks.ShouldBe(1);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-06")]
    public void An_icon_button_is_named_by_its_automation_name_never_by_its_glyph() =>
        WpfThread.Invoke(() =>
        {
            var button = new IconButton { Symbol = "search" };
            AutomationProperties.SetName(button, "Buscar");
            var unnamed = new IconButton { Symbol = "tune" };
            using var service = Host(button, unnamed);

            PeerOf(button).GetName().ShouldBe("Buscar");
            PeerOf(button).GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            PeerOf(button).GetPattern(PatternInterface.Invoke).ShouldNotBeNull();
            PeerOf(unnamed).GetName().ShouldBeEmpty();
            button.Appearance.ShouldBe(ButtonAppearance.Ghost);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "REG-02")]
    [Trait("Req", "ACC-003")]
    public void A_switch_is_a_Toggle_button_whose_whole_row_toggles() =>
        WpfThread.Invoke(() =>
        {
            var toggle = new ToggleSwitch { Content = "Números para voz", Symbol = "pin" };
            using var service = Host(toggle);
            var peer = PeerOf(toggle);
            var provider = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Button);
            peer.GetName().ShouldBe("Números para voz");
            provider.ToggleState.ShouldBe(ToggleState.Off);
            toggle.KnobOffset.ShouldBe(0);

            provider.Toggle();

            toggle.IsChecked.ShouldBe(true);
            provider.ToggleState.ShouldBe(ToggleState.On);
            toggle.KnobOffset.ShouldBe(ToggleSwitch.KnobTravel, "the knob's side tells the state");
            Fill(toggle, ToggleSwitch.TrackPart).ShouldBe(ThemePalettes.Dark.Accent);
            var track = (Border)toggle.Template.FindName(ToggleSwitch.TrackPart, toggle);
            track.ActualWidth.ShouldBe(ToggleSwitch.TrackWidth);
            track.ActualHeight.ShouldBe(ToggleSwitch.TrackHeight);
            toggle.ActualWidth.ShouldBeGreaterThan(
                track.ActualWidth,
                "the label is part of the target"
            );
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-003")]
    public void A_chip_is_a_Toggle_button_that_shows_a_check_when_on() =>
        WpfThread.Invoke(() =>
        {
            var chip = new Chip { Content = "Alt", IsMonospace = true };
            using var service = Host(chip);
            var icon = (SymbolIcon)chip.Template.FindName(Chip.IconPart, chip);
            var peer = PeerOf(chip);

            peer.GetName().ShouldBe("Alt");
            peer.GetPattern(PatternInterface.Toggle).ShouldNotBeNull();
            icon.Visibility.ShouldBe(Visibility.Collapsed);
            chip.FontFamily.ShouldBeSameAs(AppFonts.Mono);

            chip.IsChecked = true;
            chip.UpdateLayout();

            icon.Visibility.ShouldBe(Visibility.Visible);
            icon.Symbol.ShouldBe(Chip.CheckedSymbol);
            Fill(chip, Chip.ChromePart).ShouldBe(ThemePalettes.Dark.AccentWash);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    [Trait("Req", "ACC-005")]
    public void A_slider_has_RangeValue_and_named_minus_and_plus_buttons() =>
        WpfThread.Invoke(() =>
        {
            var slider = new StepSlider
            {
                Width = 260,
                Minimum = 30,
                Maximum = 100,
                SmallChange = 5,
                Value = 80,
                DecreaseName = "Bajar opacidad",
                IncreaseName = "Subir opacidad",
            };
            AutomationProperties.SetName(slider, "Opacidad");
            using var service = Host(slider);
            var peer = PeerOf(slider);
            var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Slider);
            peer.GetName().ShouldBe("Opacidad");
            range.Value.ShouldBe(80);
            range.Minimum.ShouldBe(30);
            range.Maximum.ShouldBe(100);
            var children = peer.GetChildren().ShouldNotBeNull();
            children
                .Select(child => child.GetName())
                .ShouldBe(["Bajar opacidad", "Subir opacidad"]);
            children.ShouldAllBe(child =>
                child.GetAutomationControlType() == AutomationControlType.Button
            );

            ((IInvokeProvider)children[1].GetPattern(PatternInterface.Invoke)).Invoke();
            WpfThread.DrainPendingWork();
            slider.Value.ShouldBe(85);

            ((IInvokeProvider)children[0].GetPattern(PatternInterface.Invoke)).Invoke();
            ((IInvokeProvider)children[0].GetPattern(PatternInterface.Invoke)).Invoke();
            WpfThread.DrainPendingWork();
            slider.Value.ShouldBe(75);

            range.SetValue(40);
            slider.Value.ShouldBe(40);
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_segmented_control_is_a_List_of_selectable_items() =>
        WpfThread.Invoke(() =>
        {
            var segmented = Segmented();
            AutomationProperties.SetName(segmented, "Tema");
            using var service = Host(segmented);
            var peer = PeerOf(segmented);
            var items = peer.GetChildren().ShouldNotBeNull();

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.List);
            peer.GetPattern(PatternInterface.Selection).ShouldNotBeNull();
            items
                .Select(item => item.GetName())
                .ShouldBe(["Auto", "Oscuro", "Claro", "Alto contraste"]);
            items.ShouldAllBe(item =>
                item.GetAutomationControlType() == AutomationControlType.ListItem
            );

            ((ISelectionItemProvider)items[2].GetPattern(PatternInterface.SelectionItem)).Select();

            segmented.SelectedIndex.ShouldBe(2);
            (
                (ISelectionItemProvider)items[2].GetPattern(PatternInterface.SelectionItem)
            ).IsSelected.ShouldBeTrue();
            Fill(Item(segmented, 2), SegmentedItem.ChromePart).ShouldBe(ThemePalettes.Dark.Accent);
            Fill(Item(segmented, 1), SegmentedItem.ChromePart).ShouldBe(ThemePalettes.Dark.CardHi);
            Item(segmented, 0).ActualWidth.ShouldBe(Item(segmented, 1).ActualWidth, 0.5);
            Item(segmented, 2)
                .TranslatePoint(default, segmented)
                .Y.ShouldBeGreaterThan(
                    Item(segmented, 0).TranslatePoint(default, segmented).Y,
                    "two columns make a 2 × 2 grid"
                );
        });

    [Fact]
    [Trait("Req", "ACC-001")]
    public void A_card_is_a_named_Group_around_its_content() =>
        WpfThread.Invoke(() =>
        {
            var inside = new TouchButton { Content = "Crear perfil" };
            var card = new Card { Content = inside, Tone = CardTone.Accent };
            AutomationProperties.SetName(card, "Sugerencia");
            using var service = Host(card);
            var peer = PeerOf(card);

            peer.GetAutomationControlType().ShouldBe(AutomationControlType.Group);
            peer.GetName().ShouldBe("Sugerencia");
            peer.IsKeyboardFocusable().ShouldBeFalse();
            peer.GetChildren().ShouldNotBeNull().Single().GetName().ShouldBe("Crear perfil");
            Fill(card, Card.ChromePart).ShouldBe(ThemePalettes.Dark.AccentWash);
        });

    [Fact]
    [Trait("Req", "REG-01")]
    public void UI_Automation_cannot_focus_a_control_outside_an_active_window() =>
        WpfThread.Invoke(() =>
        {
            var controls = new FrameworkElement[]
            {
                new TouchButton { Content = "Crear" },
                new ToggleSwitch { Content = "Atenuar" },
                new Chip { Content = "Win" },
                new StepSlider(),
            };
            using var service = Host(controls);

            foreach (var control in controls)
            {
                var peer = PeerOf(control);
                peer.IsKeyboardFocusable().ShouldBeFalse(control.GetType().Name);
                Should.Throw<InvalidOperationException>(peer.SetFocus);
                control.IsKeyboardFocused.ShouldBeFalse();
            }
        });

    [Fact]
    [Trait("Req", "TEM-009")]
    public void Every_interactive_control_shows_the_focus_ring() =>
        WpfThread.Invoke(() =>
        {
            new TouchButton().FocusVisualStyle.ShouldBeSameAs(FocusRingStyle.Button);
            new IconButton().FocusVisualStyle.ShouldBeSameAs(FocusRingStyle.Button);
            new Chip().FocusVisualStyle.ShouldBeSameAs(FocusRingStyle.Button);
            new ToggleSwitch().FocusVisualStyle.ShouldNotBeNull();
            new StepSlider().FocusVisualStyle.ShouldNotBeNull();
            new SegmentedItem().FocusVisualStyle.ShouldNotBeNull();
        });

    [Fact]
    [Trait("Req", "TEM-002")]
    [Trait("Req", "TEM-001")]
    public void Appearances_use_their_tokens_and_a_theme_change_repaints_them_in_place() =>
        WpfThread.Invoke(() =>
        {
            var accent = new TouchButton { Content = "Crear" };
            var danger = new TouchButton
            {
                Content = "Soltar todo",
                Appearance = ButtonAppearance.Danger,
            };
            var neutral = new IconButton { Symbol = "add", Appearance = ButtonAppearance.Neutral };
            using var service = Host(accent, danger, neutral);

            Fill(accent, "PART_Chrome").ShouldBe(ThemePalettes.Dark.Accent);
            Fill(danger, "PART_Chrome").ShouldBe(ThemePalettes.Dark.Danger);
            Fill(neutral, "PART_Chrome").ShouldBe(ThemePalettes.Dark.CardHi);
            ((SolidColorBrush)danger.Foreground).Color.ShouldBe(ThemePalettes.Dark.OnDanger);

            service.Preference = ThemeChoice.Light;

            Fill(accent, "PART_Chrome").ShouldBe(ThemePalettes.Light.Accent);
            Fill(danger, "PART_Chrome").ShouldBe(ThemePalettes.Light.Danger);
            ((SolidColorBrush)accent.Foreground).Color.ShouldBe(ThemePalettes.Light.OnAccent);
        });

    [Fact]
    [Trait("Req", "TEM-005")]
    public void An_icon_is_a_decorative_square_of_its_size() =>
        WpfThread.Invoke(() =>
        {
            var icon = new SymbolIcon { Symbol = "bolt", Size = 28 };
            using var service = Host(icon);

            icon.DesiredSize.ShouldBe(new Size(28, 28));
            UIElementAutomationPeer.CreatePeerForElement(icon)?.IsControlElement().ShouldBeFalse();
        });

    internal static ThemeService Host(params FrameworkElement[] elements)
    {
        var service = new ThemeService(new FakeSystemTheme(), ThemeChoice.Dark);
        var root = new StackPanel { Width = 400 };
        service.Attach(root);
        foreach (var element in elements)
        {
            root.Children.Add(element);
        }

        root.Measure(new Size(400, 2000));
        root.Arrange(new Rect(0, 0, 400, root.DesiredSize.Height));
        root.UpdateLayout();
        return service;
    }

    internal static Color Fill(Control control, string part) =>
        ((SolidColorBrush)((Border)control.Template.FindName(part, control)).Background).Color;

    private static SegmentedControl Segmented()
    {
        var segmented = new SegmentedControl { Columns = 2 };
        foreach (var label in new[] { "Auto", "Oscuro", "Claro", "Alto contraste" })
        {
            segmented.Items.Add(new SegmentedItem { Content = label });
        }

        segmented.SelectedIndex = 0;
        return segmented;
    }

    private static SegmentedItem Item(SegmentedControl control, int index) =>
        (SegmentedItem)control.Items[index];

    private static AutomationPeer PeerOf(UIElement element) =>
        UIElementAutomationPeer.CreatePeerForElement(element);
}
