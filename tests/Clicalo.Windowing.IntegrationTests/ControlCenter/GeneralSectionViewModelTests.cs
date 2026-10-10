using Clicalo.Application.Confirmation;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Presentation.ControlCenter.General;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// «General y panel» headless (docs/05 §3, GEN-001 to GEN-014): the cards in the prototype's order and texts, every tap
/// written to the document at once, the limits of − / + and the arrows, the undo of behavior settings, the two taps of
/// «Reiniciar Frecuentes» and the first-steps rows.
/// </summary>
public sealed class GeneralSectionViewModelTests
{
    private readonly ControlCenterTestWorld _world = new();
    private readonly List<WorkspaceNotice> _notices = [];
    private int _welcomes;

    private GeneralSectionViewModel Create()
    {
        var section = new GeneralSectionViewModel(
            new GeneralServices(
                _world.Store,
                _world.Localization,
                new TwoStepConfirm(_world.Time),
                _world.Time,
                action => action(),
                () => _welcomes++
            )
        );
        section.Noticed += (_, e) => _notices.Add(e.Notice);
        return section;
    }

    private UserSettings Settings => _world.Store.Current.Settings;

    [Fact]
    [Trait("Req", "ACC-006")]
    public void The_times_can_be_made_two_or_three_times_longer_with_undo()
    {
        var section = Create();
        var access = section.Screen.Access;

        access.TimesTitle.ShouldBe("Más tiempo para confirmar y leer avisos");
        access.Times.Select(static t => t.Label).ShouldBe(["×1", "×2", "×3"]);
        access.Times.Single(static t => t.Selected).Value.ShouldBe(1);

        section.SetTimeMultiplier(3);

        Settings.TimeMultiplier.ShouldBe(3);
        section.Screen.Access.Times.Single(static t => t.Selected).Label.ShouldBe("×3");
        _notices[^1].CanUndo.ShouldBeTrue();
        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        Settings.TimeMultiplier.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "BUR-005")]
    public void The_global_shortcut_comes_off_and_its_combination_is_one_of_the_closed_list()
    {
        var section = Create();
        var access = section.Screen.Access;

        access.Hotkey.On.ShouldBeFalse("D10: it comes off");
        access.Hotkey.Title.ShouldBe("Atajo de teclado para mostrar u ocultar el panel");
        access.Hotkeys.ShouldBeEmpty("the list shows once it is on");

        section.ToggleGlobalHotkey();

        Settings.GlobalHotkey.Enabled.ShouldBeTrue();
        var choices = section.Screen.Access.Hotkeys;
        choices.Select(static c => c.Value).ShouldBe(GlobalHotkeys.All.Select(static h => h.Id));
        choices.Single(static c => c.Selected).Value.ShouldBe(GlobalHotkeys.Default.Id);
        choices.ShouldAllBe(c => !c.Label.Contains("Win", StringComparison.Ordinal));
        choices.ShouldNotContain(
            c => string.Equals(c.Value, "ctrl-shift-m", StringComparison.Ordinal),
            "never Ctrl+Shift+M (Teams)"
        );

        section.SetGlobalHotkey("ctrl-alt-f10");
        Settings.GlobalHotkey.Combo.ShouldBe("ctrl-alt-f10");
        section.SetGlobalHotkey("ctrl-shift-m");
        Settings.GlobalHotkey.Combo.ShouldBe("ctrl-alt-f10", "only the list");
    }

    [Fact]
    [Trait("Req", "IDI-004")]
    [Trait("Req", "GEN-008")]
    public void Counts_and_option_names_are_whole_messages()
    {
        var section = Create();
        var layout = section.Screen.Layout;

        layout
            .Columns.Select(static c => c.Label)
            .ShouldBe(["2 columnas", "3 columnas", "4 columnas"]);
        layout.Rows[1].Name.ShouldBe("Filas visibles: 1");
        section.Screen.Ai.ShouldBeNull("the AI card needs the AI services");
    }

    [Fact]
    [Trait("Req", "GEN-001")]
    [Trait("Req", "GEN-002")]
    [Trait("Req", "GEN-003")]
    [Trait("Req", "GEN-004")]
    [Trait("Req", "GEN-005")]
    [Trait("Req", "IDI-001")]
    public void The_grid_shows_language_theme_size_and_view_with_the_texts_of_the_prototype()
    {
        var section = Create();
        var look = section.Screen.Look;

        section.Screen.Title.ShouldBe("General y panel");
        look.LanguageTitle.ShouldBe("Idioma · Language");
        look.Languages.Select(static l => l.Label).ShouldBe(["ES", "EN"]);
        look.Languages.Select(static l => l.Name).ShouldBe(["Español", "English"]);
        look.Themes.Select(static t => t.Label)
            .ShouldBe(["Como Windows", "Oscuro", "Claro", "Alto contraste"]);
        look.Themes.Single(static t => t.Selected).Value.ShouldBe(ThemeChoice.Auto);
        look.Sizes.Select(static s => s.Label).ShouldBe(["Pequeño", "Mediano", "Grande"]);
        look.Sizes.Single(static s => s.Selected).Value.ShouldBe(PanelSize.Medium);
        look.TextSize.ShouldBe("100%");
        look.CanTextSmaller.ShouldBeFalse();
        look.Views.Select(static v => v.Label).ShouldBe(["Completa", "Compacta", "Pestaña"]);
        look.ViewDescription.ShouldBe("Todo a la vista: fila fija, pestañas, teclas y avisos.");

        _world.Localization.TrySetLanguage("en").ShouldBeTrue();
        section.Screen.Look.LanguageTitle.ShouldBe("Language · Idioma");
        section.Screen.Title.ShouldBe("General & panel");
    }

    [Fact]
    [Trait("Req", "GEN-001")]
    [Trait("Req", "GEN-006")]
    [Trait("Req", "GEN-007")]
    [Trait("Req", "GEN-008")]
    [Trait("Req", "GEN-012")]
    [Trait("Req", "GEN-013")]
    [Trait("Req", "GEN-014")]
    public void The_columns_below_show_their_cards_in_order()
    {
        var screen = Create().Screen;

        screen.Layout.Caption.ShouldBe("Disposición");
        screen.Layout.Rows.Select(static r => r.Label).ShouldBe(["Auto", "1", "2", "3"]);
        screen
            .Layout.Rows.Select(static r => r.Cells)
            .ShouldBe([3, 1, 2, 3], "Auto draws 3 rows in M");
        screen.Layout.FollowApp.Title.ShouldBe("Cambiar con la app activa");
        screen.Layout.FollowApp.On.ShouldBeTrue();
        screen.Layout.ProfileSelectorNote.ShouldBeEmpty();
        screen
            .Layout.Columns.Select(static c => c.Label)
            .ShouldBe(["2 columnas", "3 columnas", "4 columnas"]);
        screen.Layout.ShowKeys.Select(static k => k.Label).ShouldBe(["Con teclas", "Solo nombre"]);
        screen.Transparency.OpacityValue.ShouldBe("92%");
        screen.Transparency.DimValue.ShouldBe("35%");
        screen
            .Dock.Sides.Select(static s => s.Label)
            .ShouldBe(["Izquierda", "Arriba", "Abajo", "Derecha"]);
        screen.Dock.PerPage.Select(static p => p.Label).ShouldBe(["4", "5", "6", "8"]);
        screen.Dock.HandleBackName.ShouldBe("Subir la pestaña");
        screen.Feedback.Sound.On.ShouldBeTrue();
        screen.Feedback.Flash.On.ShouldBeTrue();
        screen
            .Safety.MaxHold.Select(static m => m.Label)
            .ShouldBe(["30 s", "1 min", "2 min", "Nunca"]);
        screen.Safety.MaxHold.Single(static m => m.Selected).Label.ShouldBe("1 min");
        screen.Safety.ReleaseOnAppSwitch.On.ShouldBeTrue();
        screen.Access.Caption.ShouldBe("Accesibilidad y datos");
        screen.Access.ResetTitle.ShouldBe("Reiniciar Frecuentes");
        screen.Start.WelcomeTitle.ShouldBe("Ver la bienvenida otra vez");
        screen.Start.CoachTitle.ShouldBe("Ver la guía de la pestaña");
        screen.Start.NoKeyboard.Title.ShouldBe("No puedo usar el teclado");
    }

    [Fact]
    [Trait("Req", "GEN-003")]
    [Trait("Req", "GEN-004")]
    [Trait("Req", "GEN-005")]
    [Trait("Req", "GEN-006")]
    [Trait("Req", "GEN-007")]
    [Trait("Req", "GEN-008")]
    [Trait("Req", "GEN-011")]
    [Trait("Req", "GEN-012")]
    [Trait("Req", "GEN-013")]
    [Trait("Req", "REG-07")]
    public void Every_tap_is_written_to_the_document_at_once()
    {
        var section = Create();

        section.SetTheme(ThemeChoice.Dark);
        section.SetSize(PanelSize.Large);
        section.TextBigger();
        section.SetDensity(PanelDensity.Compact);
        section.SetRows(2);
        section.ToggleFollowApp();
        section.ToggleAlwaysVisibleRow();
        section.ToggleProfileSelectorRow();
        section.SetColumns(4);
        section.SetShowKeys(false);
        section.ToggleSound();
        section.ToggleFlash();
        section.SetMaxHold(null);
        section.ToggleReleaseOnAppSwitch();
        section.ToggleReduceMotion();
        section.ToggleNoKeyboard();

        var settings = Settings;
        settings.Theme.ShouldBe(ThemeChoice.Dark);
        settings.Size.ShouldBe(PanelSize.Large);
        settings.TextScalePercent.ShouldBe(110);
        settings.Density.ShouldBe(PanelDensity.Compact);
        settings.RowsPreference.ShouldBe(2);
        settings.LockProfile.ShouldBeTrue("following the app off is the Fixed state");
        settings.ShowAlwaysVisibleRow.ShouldBeFalse();
        settings.ShowProfileSelectorRow.ShouldBeFalse();
        settings.Columns.ShouldBe(4);
        settings.ShowKeys.ShouldBeFalse();
        settings.Feedback.ShouldBe(new FeedbackSettings(false, false));
        settings.KeySafety.ShouldBe(new KeySafetySettings(null, false));
        settings.ReduceMotion.ShouldBeTrue();
        settings.NoKeyboardUser.ShouldBeTrue();
        section.Screen.Look.Themes.Single(static t => t.Selected).Value.ShouldBe(ThemeChoice.Dark);
        section.Screen.Layout.Rows.Select(static r => r.Cells).ShouldBe([3, 1, 2, 3]);
        section.Screen.Layout.ProfileSelectorNote.ShouldStartWith("Sin esta fila");
        section.Screen.Safety.MaxHold.Single(static m => m.Selected).Label.ShouldBe("Nunca");

        // Behavior settings offer [undo] in the status bar; presentation ones do not enter the history (DAT-006).
        _notices.Count.ShouldBe(3);
        _notices.ShouldAllBe(static n => n.CanUndo);
        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        Settings.NoKeyboardUser.ShouldBeFalse();
        Settings.ReduceMotion.ShouldBeTrue("undo restores only the last behavior change");
    }

    [Fact]
    [Trait("Req", "GEN-004")]
    [Trait("Req", "GEN-009")]
    public void Text_size_and_opacity_stay_inside_their_ranges()
    {
        var section = Create();

        for (var i = 0; i < 8; i++)
        {
            section.TextBigger();
        }

        Settings.TextScalePercent.ShouldBe(150);
        section.Screen.Look.CanTextBigger.ShouldBeFalse();

        section.OpacityLess();
        Settings.Opacity.ShouldBe(0.80, 1e-9, "0.92 − 0.10 lands on the 5 % grid");
        section.OpacityMore();
        section.OpacityMore();
        section.OpacityMore();
        Settings.Opacity.ShouldBe(1.0, 1e-9);
        section.SetOpacityPercent(47);
        Settings.Opacity.ShouldBe(0.45, 1e-9);
        section.SetOpacityPercent(10);
        Settings.Opacity.ShouldBe(0.30, 1e-9);
        section.ToggleAutoDim();
        Settings.AutoDim.ShouldBeFalse();
        section.SetDimPercent(62);
        Settings.DimTo.ShouldBe(0.60, 1e-9);
        section.Screen.Transparency.DimValue.ShouldBe("60%");
    }

    [Fact]
    [Trait("Req", "GEN-010")]
    [Trait("Req", "PES-003")]
    public void The_tab_settings_move_the_handle_of_the_chosen_side_within_its_limits()
    {
        var section = Create();

        section.SetDockSide(DockSide.Top);
        Settings.Dock.Side.ShouldBe(DockSide.Top);
        Settings.Density.ShouldBe(
            PanelDensity.Full,
            "choosing a side here does not change the view"
        );
        section.Screen.Dock.Vertical.ShouldBeFalse();
        section.Screen.Dock.HandleForwardName.ShouldBe("Mover la pestaña a la derecha");

        for (var i = 0; i < 6; i++)
        {
            section.HandleBack();
        }

        Settings.Dock.HandlePositions.Top.ShouldBe(8);
        Settings.Dock.HandlePositions.Right.ShouldBe(50, "only the chosen side moves");
        section.Screen.Dock.CanBack.ShouldBeFalse();
        section.HandleForward();
        Settings.Dock.HandlePositions.Top.ShouldBe(18);

        section.ToggleHandleLock();
        section.SetPerPage(8);
        section.ToggleAutoHide();
        section.ToggleKeepScrollbar();
        Settings.Dock.HandleLocked.ShouldBeTrue();
        Settings.Dock.PerPage.ShouldBe(8);
        Settings.Dock.PinOpen.ShouldBeTrue("not hiding after use keeps the bar open");
        Settings.Dock.Gutter.ShouldBeTrue();
        section.Screen.Dock.Gutter.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "GEN-013")]
    [Trait("Req", "FRE-004")]
    [Trait("Req", "REG-04")]
    public void Resetting_frequents_needs_two_taps_and_can_be_undone()
    {
        _world.Store.Dispatch(new RecordUsage(new ShortcutId("bold"))).IsSuccess.ShouldBeTrue();
        var section = Create();

        section.ResetFrequents();
        section.Screen.Access.ResetArmed.ShouldBeTrue();
        section.Screen.Access.ResetTitle.ShouldBe("Confirmar");
        _world.Store.Current.Frequents.UsageEpoch.ShouldBe(0, "the first tap only arms it");

        _world.Time.Advance(TimeSpan.FromSeconds(4));
        section.Screen.Access.ResetArmed.ShouldBeFalse("it disarms after 3.5 s");

        section.ResetFrequents();
        section.ResetFrequents();
        _world.Store.Current.Frequents.UsageEpoch.ShouldBe(1);
        _world.Store.Current.Frequents.Usage.LastUse(new ShortcutId("bold")).ShouldBeNull();
        _notices[^1].Text.ShouldBe(Domain.Messages.L.ResetFreqT);
        _notices[^1].CanUndo.ShouldBeTrue();
        _world.Store.Undo().IsSuccess.ShouldBeTrue();
        _world.Store.Current.Frequents.UsageEpoch.ShouldBe(0);
        _world.Store.Current.Frequents.Usage.LastUse(new ShortcutId("bold")).ShouldNotBeNull();
    }

    [Fact]
    [Trait("Req", "GEN-014")]
    [Trait("Req", "PES-015")]
    public void First_steps_open_the_welcome_and_show_the_tab_guide_again()
    {
        _world
            .Store.Dispatch(new SetSetting(SettingPaths.DockCoachDone, true))
            .IsSuccess.ShouldBeTrue();
        var section = Create();

        section.SeeWelcome();
        _welcomes.ShouldBe(1);

        section.SeeTabGuide();
        Settings.Dock.CoachDone.ShouldBeFalse();
        _notices[^1].Text.ShouldBe(Domain.Messages.L.SeeCoachD);
    }

    [Fact]
    [Trait("Req", "GEN-002")]
    [Trait("Req", "IDI-001")]
    public void A_language_button_writes_the_language()
    {
        var section = Create();

        section.SetLanguage("en");

        Settings.Language.ShouldBe(LangCode.En);
    }
}
