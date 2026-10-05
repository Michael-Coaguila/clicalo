using Clicalo.Application.Engine;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Messages;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.StickyModifiers;
using Clicalo.Presentation.Panel;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// The body of the panel view model, headless (Presentation has no test project of its own): pages, the measured
/// rows, the Always visible row, the voice numbers, the sticky modifiers, the selector and its grid, the notice bar,
/// the empty profile and the administrator notice, against the real texts of <c>data/i18n</c>. The view model only
/// applies <c>Clicalo.Domain.PanelLayout</c>; these tests check that it shows what the rules decide and forwards every
/// intention.
/// </summary>
public sealed class PanelBodyViewModelTests
{
    private readonly RecordingBodyIntents _intents = new();

    [Fact]
    [Trait("Req", "CUA-004")]
    [Trait("Req", "ACC-003")]
    public void Twenty_shortcuts_on_three_by_three_make_three_pages_and_the_arrows_stay_inside_them()
    {
        var panel = Panel(20);

        panel.Shape.PerPage.ShouldBe(9);
        Ids(panel).ShouldBe(Range(0, 9));
        panel.Pager.IsVisible.ShouldBeTrue();
        panel.Pager.Dots.Select(static d => d.WidthPx).ShouldBe([24d, 10, 10]);
        panel.Pager.Dots[1].AccessibleName.ShouldBe("Página 2");
        panel.Pager.Dots.Select(static d => d.AccessibleState).ShouldBe(["Activado", "", ""]);
        panel.Pager.CanGoPrevious.ShouldBeFalse();

        panel.Pager.Next();
        Ids(panel).ShouldBe(Range(9, 9));
        panel.Pager.Dots[2].Select();
        Ids(panel).ShouldBe(Range(18, 2));
        panel.Pager.CanGoNext.ShouldBeFalse();
        panel.Pager.Next();
        panel.Pager.Page.ShouldBe(2);
    }

    [Fact]
    [Trait("Req", "CUA-005")]
    [Trait("Req", "ACC-001")]
    public void A_swipe_and_its_equivalent_without_gesture_change_the_page_alike()
    {
        var swiped = Panel(20);
        var tapped = Panel(20);

        swiped.Pager.Swiped(towardLeft: true);
        tapped.Pager.Next();
        swiped.Pager.Page.ShouldBe(tapped.Pager.Page);

        swiped.Pager.Swiped(towardLeft: false);
        tapped.Pager.Previous();
        swiped.Pager.Page.ShouldBe(0);
        tapped.Pager.Page.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "CUA-001")]
    [Trait("Req", "CUA-002")]
    public void The_rows_that_fit_are_the_measured_ones()
    {
        var panel = Panel(20);

        panel.ApplyGridSpace(165);
        panel.Shape.Rows.ShouldBe(2);
        panel.Tiles.Count.ShouldBe(6);
        panel.Pager.PageCount.ShouldBe(4);

        panel.ApplyGridSpace(10);
        panel.Shape.Rows.ShouldBe(1);
        panel.Tiles.Count.ShouldBe(3);
    }

    [Fact]
    [Trait("Req", "CUA-006")]
    public void Changing_profile_or_columns_goes_back_to_page_one()
    {
        var library = PanelBodyTestData.Library(20, 0);
        var panel = PanelBodyTestData.Panel(library, PanelBodyTestData.Word, _intents);
        panel.Pager.Next();
        panel.Pager.Page.ShouldBe(1);

        panel.Apply(
            PanelProjector.Project(library, PanelBodyTestData.Excel, LangCode.Es, LangCode.Es)
        );
        panel.Apply(
            PanelProjector.Project(library, PanelBodyTestData.Word, LangCode.Es, LangCode.Es)
        );
        panel.Pager.Page.ShouldBe(0);

        panel.Pager.Next();
        panel.ApplyLayout(PanelLayoutSettings.Default with { Columns = 4 });
        panel.Pager.Page.ShouldBe(0);
        panel.Shape.PerPage.ShouldBe(12);
    }

    [Fact]
    [Trait("Req", "FIJ-003")]
    public void The_Always_visible_row_shows_one_less_and_the_chip_that_cycles_its_pages()
    {
        var panel = Panel(0, strip: 10);

        panel.Strip.IsVisible.ShouldBeTrue();
        panel.Strip.Label.ShouldBe("Siempre visible");
        panel.Strip.Tiles.Count.ShouldBe(7);
        panel.Strip.HasMore.ShouldBeTrue();
        panel.Strip.MoreLabel.ShouldBe("1/2");
        panel.Strip.MoreName.ShouldBe("Ver más atajos fijos");

        panel.Strip.More();
        panel
            .Strip.Tiles.Select(static t => t.AccessibleName)
            .ShouldBe(["Fijo 7", "Fijo 8", "Fijo 9"]);
        panel.Strip.MoreLabel.ShouldBe("2/2");

        panel.Strip.More();
        panel.Strip.MoreLabel.ShouldBe("1/2");
    }

    [Fact]
    [Trait("Req", "FIJ-002")]
    [Trait("Req", "FIJ-003")]
    public void In_S_the_row_holds_four_and_shows_only_icons()
    {
        var panel = Panel(0, strip: 5);
        panel.ApplyLayout(
            PanelLayoutSettings.Default with
            {
                Size = Clicalo.Domain.Settings.PanelSize.Small,
            }
        );

        panel.Strip.ShowsNames.ShouldBeFalse();
        panel.Strip.Tiles.Count.ShouldBe(3);
        panel.Strip.MoreLabel.ShouldBe("1/2");
        panel.Strip.TileHeightPx.ShouldBe(PanelSizes.S.StripHeightPx);
    }

    [Fact]
    [Trait("Req", "ACC-009")]
    [Trait("Req", "ACC-010")]
    public void Voice_numbers_continue_across_pages_and_the_row_follows_the_list()
    {
        var panel = Panel(20, strip: 3);
        panel.Tiles[0].VoiceNumber.ShouldBeNull();
        panel.Tiles[0].SpokenName.ShouldBe("Atajo 0");

        panel.ApplyLayout(PanelLayoutSettings.Default with { VoiceNumbers = true });
        panel.Pager.Next();

        panel.Tiles[0].VoiceNumber.ShouldBe(10);
        panel.Tiles[0].SpokenName.ShouldBe("10 Atajo 9");
        panel.Strip.Tiles.Select(static t => t.VoiceNumber).ShouldBe([21, 22, 23]);
    }

    [Fact]
    [Trait("Req", "CUA-003")]
    public void Without_room_for_a_row_while_something_is_held_the_row_and_the_selector_hide_until_it_is_released()
    {
        var panel = Panel(20, strip: 2);
        panel.ApplyGridSpace(10);
        panel.Strip.IsVisible.ShouldBeTrue();

        panel.ApplyEngine(PanelBodyTestData.Holding(new ShortcutId("w1")));
        panel.Strip.IsVisible.ShouldBeFalse();
        panel.Selector.IsVisible.ShouldBeFalse();

        panel.ApplyGridSpace(500);
        panel.Strip.IsVisible.ShouldBeFalse();

        panel.ApplyEngine(EngineSnapshot.Empty);
        panel.Strip.IsVisible.ShouldBeTrue();
        panel.Selector.IsVisible.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "FIJ-005")]
    public void The_sticky_modifiers_show_their_three_states_and_ask_to_advance()
    {
        var panel = Panel(3);
        panel.Sticky.IsVisible.ShouldBeFalse();

        panel.ApplyLayout(PanelLayoutSettings.Default with { StickyRow = true });
        panel.ApplyEngine(
            EngineSnapshot.Empty with
            {
                Sticky = new StickyState(
                    StickyLevel.Once,
                    StickyLevel.Off,
                    StickyLevel.Locked,
                    StickyLevel.Off
                ),
            }
        );

        panel.Sticky.IsVisible.ShouldBeTrue();
        panel.Sticky.Keys.Select(static k => k.Label).ShouldBe(["Ctrl", "Alt", "Shift", "Win"]);
        panel
            .Sticky.Keys.Select(static k => k.Level)
            .ShouldBe([StickyLevel.Once, StickyLevel.Off, StickyLevel.Locked, StickyLevel.Off]);
        panel.Sticky.Keys[2].IsLocked.ShouldBeTrue();
        panel.Sticky.Keys[0].AccessibleState.ShouldBe("se sumará a tu próximo toque o clic");

        panel.Sticky.Keys[3].Tap();
        _intents.Calls.ShouldBe(["AdvanceSticky:" + ModifierKind.Win]);
    }

    [Fact]
    [Trait("Req", "SEL-001")]
    [Trait("Req", "SEL-002")]
    [Trait("Req", "ACC-003")]
    public void The_profile_button_opens_the_grid_and_from_Frequents_returns_in_one_tap()
    {
        var panel = Panel(3);
        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                ActiveAppProfile = PanelBodyTestData.Word,
            }
        );

        panel.Selector.IsVisible.ShouldBeTrue();
        panel.Selector.ProfileName.ShouldBe("Word");
        panel.Selector.IsActiveApp.ShouldBeTrue();
        panel.Selector.Caret.ShouldBe(SelectorCaret.Expand);
        panel.Selector.ProfileButton();
        panel.Selector.Frequents();

        panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
        panel.Selector.Caret.ShouldBe(SelectorCaret.Return);
        panel.Selector.IsFrequentsActive.ShouldBeTrue();
        panel.Selector.FrequentsState.ShouldBe("Activado");
        panel.Selector.ProfileButton();

        _intents.Calls.ShouldBe(["TogglePicker", "ShowFrequents", "ReturnFromFrequents"]);
    }

    [Fact]
    [Trait("Req", "SEL-003")]
    [Trait("Req", "SEL-004")]
    [Trait("Req", "ACC-003")]
    public void The_profile_grid_lists_every_profile_marks_the_current_one_and_the_active_app()
    {
        var panel = Panel(3);
        panel.Picker.IsVisible.ShouldBeFalse();

        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                PickerOpen = true,
                ActiveAppProfile = PanelBodyTestData.Excel,
                SuggestionApp = "Notion",
            }
        );

        panel.Picker.IsVisible.ShouldBeTrue();
        panel.Picker.Entries.Select(static e => e.Name).ShouldBe(["General", "Word", "Excel"]);
        panel.Picker.Entries.Select(static e => e.IsCurrent).ShouldBe([false, true, false]);
        panel.Picker.Entries.Select(static e => e.IsActiveApp).ShouldBe([false, false, true]);
        panel
            .Picker.Entries.Select(static e => e.AccessibleState)
            .ShouldBe(["", "Activado", "app activa"]);
        panel.Picker.SuggestionName.ShouldBe("Crear para Notion");
        panel.Picker.MoreName.ShouldBe("Más");
        panel.Picker.Legend.ShouldBe("El punto marca la app activa");
        panel.Selector.Caret.ShouldBe(SelectorCaret.Collapse);

        panel.Picker.Entries[2].Choose();
        panel.Picker.CreateSuggested();
        panel.Picker.More();
        _intents.Calls.ShouldBe([
            "ChooseProfile:p-excel",
            "CreateProfileForActiveApp",
            "OpenTemplates",
        ]);
    }

    [Fact]
    [Trait("Req", "AVI-001")]
    [Trait("Req", "AVI-003")]
    [Trait("Req", "AVI-004")]
    public void The_notice_bar_rests_on_ready_and_offers_undo_and_repeat_when_they_apply()
    {
        var panel = Panel(3);
        panel.Notices.IsVisible.ShouldBeTrue();
        panel.Notices.Message.ShouldBe("Listo. Toca un botón para usarlo.");
        panel.Notices.Tone.ShouldBe(NoticeTone.Rest);
        panel.Notices.CanUndo.ShouldBeFalse();
        panel.Notices.CanRepeat.ShouldBeFalse();

        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                Notice = new PanelNotice(
                    L.Deleted,
                    new IconRef("delete"),
                    NoticeTone.Notice,
                    CanUndo: true
                ),
                CanRepeat = true,
            }
        );
        panel.Notices.Message.ShouldBe("Atajo eliminado");
        panel.Notices.CanUndo.ShouldBeTrue();
        panel.Notices.UndoName.ShouldBe("Deshacer");
        panel.Notices.CanRepeat.ShouldBeTrue();
        panel.Notices.RepeatName.ShouldBe("Repetir la última acción");
        panel.Notices.Undo();
        panel.Notices.Repeat();

        panel.ApplyContext(PanelBodyContext.Idle with { CanRepeat = true, EditMode = true });
        panel.Notices.CanRepeat.ShouldBeFalse();
        _intents.Calls.ShouldBe(["Undo", "Repeat"]);
    }

    [Fact]
    [Trait("Req", "EJE-013")]
    public void An_elevated_app_shows_the_administrator_notice_and_its_button_relaunches()
    {
        var panel = Panel(3);
        panel.Admin.IsVisible.ShouldBeFalse();

        panel.ApplyContext(PanelBodyContext.Idle with { ElevatedApp = "Administrador de tareas" });

        panel.Admin.IsVisible.ShouldBeTrue();
        panel.Admin.Message.ShouldStartWith(
            "Administrador de tareas se ejecuta como administrador."
        );
        panel.Admin.ButtonName.ShouldBe("Iniciar Clícalo como administrador");
        panel.Admin.Relaunch();
        _intents.Calls.ShouldBe(["RelaunchElevated"]);
    }

    [Fact]
    [Trait("Req", "CUA-010")]
    public void An_empty_profile_shows_its_card_with_Add_shortcut()
    {
        var panel = PanelBodyTestData.Panel(
            PanelBodyTestData.Library(3, 0),
            PanelBodyTestData.Excel,
            _intents
        );

        panel.Empty.IsVisible.ShouldBeTrue();
        panel.Empty.Title.ShouldBe("Este perfil aún no tiene atajos");
        panel.Empty.Subtitle.ShouldBe("Añade el primero para «Excel».");
        panel.Empty.ButtonName.ShouldBe("Añadir atajo");
        panel.Pager.IsVisible.ShouldBeFalse();
        panel.Empty.Add();
        _intents.Calls.ShouldBe(["AddShortcut:p-excel"]);

        panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });
        panel.Empty.IsVisible.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PAN-008")]
    public void Searching_with_text_hides_the_rows_and_the_selector()
    {
        var panel = Panel(3, strip: 2);
        panel.ApplyLayout(PanelLayoutSettings.Default with { StickyRow = true });

        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                SearchingWithText = true,
                PickerOpen = true,
            }
        );

        panel.Strip.IsVisible.ShouldBeFalse();
        panel.Sticky.IsVisible.ShouldBeFalse();
        panel.Selector.IsVisible.ShouldBeFalse();
        panel.Picker.IsVisible.ShouldBeFalse();
        panel.Notices.IsVisible.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "IDI-001")]
    public void A_language_change_relocalizes_the_body()
    {
        var localization = PanelTestData.Localization("es");
        var controller = new Clicalo.Application.Coordinators.PanelInteractionController(
            new PanelEngineInbox(),
            () => 1,
            TimeProvider.System
        );
        var panel = new PanelViewModel(
            controller,
            localization,
            PanelDesktopFixture.Touch,
            _ => null,
            _intents,
            null
        );
        panel.Apply(
            PanelProjector.Project(
                PanelBodyTestData.Library(3, 0),
                PanelBodyTestData.Word,
                LangCode.Es,
                LangCode.Es
            )
        );

        localization.TrySetLanguage("en").ShouldBeTrue();
        panel.Relocalize();

        panel.Notices.Message.ShouldBe("Ready. Tap a button to use it.");
        panel.Selector.FrequentsName.ShouldBe("Frequent");
    }

    private static string[] Range(int start, int count) =>
        [
            .. Enumerable
                .Range(start, count)
                .Select(static i =>
                    "w" + i.ToString(System.Globalization.CultureInfo.InvariantCulture)
                ),
        ];

    private static string[] Ids(PanelViewModel panel) =>
        [.. panel.Tiles.Select(static t => t.Id.Value)];

    private PanelViewModel Panel(int words, int strip = 0) =>
        PanelBodyTestData.Panel(
            PanelBodyTestData.Library(words, strip),
            PanelBodyTestData.Word,
            _intents
        );
}
