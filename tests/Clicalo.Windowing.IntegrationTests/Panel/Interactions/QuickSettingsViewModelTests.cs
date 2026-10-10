using Clicalo.Domain.Execution;
using Clicalo.Domain.Settings;
using Clicalo.Windowing.IntegrationTests.SearchPanel;

namespace Clicalo.Windowing.IntegrationTests.Interactions;

/// <summary>
/// The Quick settings sheet headless (AJR-001 to AJR-005): its sections in order with the prototype's texts, every change
/// written to the document at once, which choices close the sheet and the opacity steps.
/// </summary>
public sealed class QuickSettingsViewModelTests
{
    private readonly InteractionsWorld _world = new();

    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "IDI-001")]
    public void The_sheet_shows_its_sections_in_order_with_the_texts_of_the_prototype()
    {
        var sheet = _world.QuickSettings;

        sheet.ControlCenterTitle.ShouldBe("Centro de control");
        sheet.ControlCenterSubtitle.ShouldBe("Atajos, plantillas, columnas, idioma y más");
        sheet.ViewHeading.ShouldBe("Vista");
        sheet.Views.Select(static o => o.Label).ShouldBe(["Completa", "Compacta", "Pestaña"]);
        sheet
            .Views.Select(static o => o.Icon)
            .ShouldBe(["view_agenda", "view_compact", "view_sidebar"]);
        sheet.OpacityHeading.ShouldBe("Opacidad");
        sheet.OpacityText.ShouldBe("92%");
        sheet.Sizes.Select(static o => o.Label).ShouldBe(["S", "M", "L"]);
        sheet.Sizes.Select(static o => o.AccessibleName).ShouldBe(["Pequeño", "Mediano", "Grande"]);
        sheet.ShowsSides.ShouldBeFalse();
        sheet
            .Sides.Select(static o => o.AccessibleName)
            .ShouldBe(["Izquierda", "Arriba", "Abajo", "Derecha"]);
        sheet
            .Themes.Select(static o => o.Label)
            .ShouldBe(["Auto", "Oscuro", "Claro", "Alto contraste"]);
        sheet
            .Switches.Select(static s => s.Label)
            .ShouldBe([
                "Modo prueba (30 s)",
                "Atenuar cuando no lo uso",
                "Teclas fijas",
                "Números para voz",
            ]);
        sheet.Views.Single(static o => o.IsSelected).Value.ShouldBe(PanelDensity.Full);
        sheet.Themes.Count(static o => o.IsSelected).ShouldBe(1);

        _world.Localization.TrySetLanguage("en").ShouldBeTrue();
        sheet.Relocalize();
        sheet.ViewHeading.ShouldBe("View");
        sheet.Views[0].Label.ShouldBe("Full");
        sheet.OpacityLessName.ShouldBe("Less opacity");
    }

    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "AJR-003")]
    [Trait("Req", "AJR-005")]
    public void View_size_and_side_are_saved_at_once_and_close_the_sheet()
    {
        var sheet = _world.QuickSettings;
        sheet.Open();

        sheet.Views[2].Select();

        _world.Settings.Density.ShouldBe(PanelDensity.Dock);
        sheet.IsOpen.ShouldBeFalse();
        sheet.ShowsSides.ShouldBeTrue();
        sheet.Views[2].IsSelected.ShouldBeTrue();

        sheet.Open();
        sheet.Sides[0].Select();
        _world.Settings.Dock.Side.ShouldBe(DockSide.Left);
        sheet.IsOpen.ShouldBeFalse();

        sheet.Open();
        sheet.Sizes[2].Select();
        _world.Settings.Size.ShouldBe(PanelSize.Large);
        sheet.IsOpen.ShouldBeFalse();
        _world.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "AJR-004")]
    public void Theme_and_the_switches_are_saved_and_leave_the_sheet_open()
    {
        var sheet = _world.QuickSettings;
        sheet.Open();

        sheet.Themes[3].Select();
        sheet.AutoDimSwitch.Toggle();
        sheet.StickySwitch.Toggle();
        sheet.VoiceNumbersSwitch.Toggle();

        _world.Settings.Theme.ShouldBe(ThemeChoice.HighContrast);
        _world.Settings.AutoDim.ShouldBe(!SettingsSchema.Defaults.AutoDim);
        _world.Settings.StickyModifiersRow.ShouldBe(!SettingsSchema.Defaults.StickyModifiersRow);
        _world.Settings.VoiceNumbers.ShouldBe(!SettingsSchema.Defaults.VoiceNumbers);
        sheet.AutoDimSwitch.IsOn.ShouldBe(_world.Settings.AutoDim);
        sheet.Themes[3].IsSelected.ShouldBeTrue();
        sheet.IsOpen.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "AJR-002")]
    public void The_opacity_buttons_move_ten_percent_round_to_five_and_stay_between_30_and_100()
    {
        var sheet = _world.QuickSettings;
        sheet.Open();

        sheet.DecreaseOpacity();
        _world.Settings.Opacity.ShouldBe(0.80);
        sheet.OpacityText.ShouldBe("80%");

        sheet.IncreaseOpacity();
        sheet.IncreaseOpacity();
        sheet.IncreaseOpacity();
        _world.Settings.Opacity.ShouldBe(1.00);

        sheet.SetOpacityPercent(67);
        _world.Settings.Opacity.ShouldBe(0.65);
        sheet.OpacityPercent.ShouldBe(65);

        for (var i = 0; i < 10; i++)
        {
            sheet.DecreaseOpacity();
        }

        _world.Settings.Opacity.ShouldBe(0.30);
        sheet.IsOpen.ShouldBeTrue();
        QuickSettingsBounds();
    }

    [Fact]
    [Trait("Req", "AJR-001")]
    public void The_control_center_card_opens_the_profile_in_view_or_general_from_frequents()
    {
        var sheet = _world.QuickSettings;
        sheet.Open();

        sheet.OpenControlCenter();
        _world.ProfileInView = null;
        sheet.OpenControlCenter();

        _world.ControlCenter.Requests.ShouldBe(["cc:" + SearchTestWorld.Word.Value, "cc:general"]);
        sheet.IsOpen.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "AJR-001")]
    [Trait("Req", "TAC-008")]
    public void Switching_test_mode_on_closes_the_sheet_and_tells_the_engine()
    {
        var sheet = _world.QuickSettings;
        sheet.Open();

        sheet.TestModeSwitch.Toggle();

        sheet.IsOpen.ShouldBeFalse();
        sheet.TestModeSwitch.IsOn.ShouldBeTrue();
        _world.TestMode.IsOn.ShouldBeTrue();
        _world.Engine.Events.OfType<EngineEvent.SetTestMode>().Single().On.ShouldBeTrue();

        sheet.Open();
        sheet.TestModeSwitch.Toggle();
        sheet.TestModeSwitch.IsOn.ShouldBeFalse();
        sheet.IsOpen.ShouldBeTrue();
    }

    private static void QuickSettingsBounds()
    {
        Presentation.Panel.QuickSettings.QuickSettingsViewModel.OpacityMinimumPercent.ShouldBe(30);
        Presentation.Panel.QuickSettings.QuickSettingsViewModel.OpacityMaximumPercent.ShouldBe(100);
        Presentation.Panel.QuickSettings.QuickSettingsViewModel.OpacityStepPercent.ShouldBe(5);
    }
}
