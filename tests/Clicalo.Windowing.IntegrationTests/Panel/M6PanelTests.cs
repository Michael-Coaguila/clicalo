using Clicalo.Application.Engine;
using Clicalo.Application.Profiles;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.PanelLayout;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Touch;
using Clicalo.Presentation.Panel;
using Clicalo.Presentation.Panel.Header;
using Clicalo.UI.Wpf.Surfaces.Panel;
using Clicalo.Windowing.IntegrationTests.Interactions;

namespace Clicalo.Windowing.IntegrationTests.MinimalPanel;

/// <summary>
/// What M6 closed in the panel, headless and against the real texts of <c>data/i18n</c>: the empty Frequents card
/// (CUA-010), the yellow dot of a pending suggestion (SEL-005), the title as the way to the profile grid and to ★
/// Frequents (SEL-006), the armed tile (EJE-002), the answer to an ignored touch (TAC-003), scrolling without firing
/// (TAC-004), pause (BUR-004) and the inherited compatible mode (D24).
/// </summary>
public sealed class M6PanelTests
{
    private readonly RecordingBodyIntents _intents = new();

    [Fact]
    [Trait("Req", "CUA-010")]
    public void An_empty_Frequents_shows_its_card_with_the_way_back_to_the_profile()
    {
        var library = PanelBodyTestData.Library(3, 0);
        var panel = PanelBodyTestData.Panel(library, PanelBodyTestData.Word, _intents);
        panel.Apply(
            PanelProjector.ProjectFrequents(
                library,
                [],
                PanelBodyTestData.Word,
                "Siempre visible",
                LangCode.Es,
                LangCode.Es
            )
        );

        panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });

        panel.Tiles.ShouldBeEmpty();
        panel.Empty.IsVisible.ShouldBeTrue();
        panel.Empty.IsFrequents.ShouldBeTrue();
        panel.Empty.Icon.ShouldBe("star");
        panel.Empty.Title.ShouldBe("Aún no hay frecuentes");
        panel.Empty.Subtitle.ShouldBe("Los atajos que uses aparecerán aquí.");
        panel.Empty.ButtonName.ShouldBe("Volver a Word");
        panel.Pager.IsVisible.ShouldBeFalse();

        panel.Empty.Add();

        _intents.Calls.ShouldBe(["ReturnFromFrequents"]);
    }

    [Fact]
    [Trait("Req", "CUA-010")]
    public void The_empty_Frequents_card_goes_away_with_the_first_frequent_and_while_searching()
    {
        var library = PanelBodyTestData.Library(3, 0);
        var panel = PanelBodyTestData.Panel(library, PanelBodyTestData.Word, _intents);
        var empty = PanelProjector.ProjectFrequents(
            library,
            [],
            PanelBodyTestData.Word,
            "Siempre visible",
            LangCode.Es,
            LangCode.Es
        );
        panel.Apply(empty);
        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                Frequents = true,
                SearchingWithText = true,
            }
        );
        panel.Empty.IsVisible.ShouldBeFalse();

        library.TryLocate(new ShortcutId("w0"), out var location).ShouldBeTrue();
        library.TryGetShortcut(new ShortcutId("w0"), out var shortcut).ShouldBeTrue();
        panel.Apply(
            PanelProjector.ProjectFrequents(
                library,
                [new FrequentEntry(shortcut, location, Pinned: false, Uses: 1)],
                PanelBodyTestData.Word,
                "Siempre visible",
                LangCode.Es,
                LangCode.Es
            )
        );
        panel.ApplyContext(PanelBodyContext.Idle with { Frequents = true });

        panel.Empty.IsVisible.ShouldBeFalse();
        panel.Tiles.Count.ShouldBe(1);
    }

    [Fact]
    [Trait("Req", "SEL-005")]
    [Trait("Req", "ACC-003")]
    public void A_pending_suggestion_puts_a_named_dot_on_the_profile_button()
    {
        var panel = Panel();
        panel.Selector.HasSuggestion.ShouldBeFalse();
        panel.Selector.ButtonState.ShouldBeEmpty();

        panel.ApplyContext(
            PanelBodyContext.Idle with
            {
                SuggestionApp = "Excel",
                ActiveAppProfile = PanelBodyTestData.Word,
            }
        );

        panel.Selector.HasSuggestion.ShouldBeTrue();
        panel.Selector.SuggestionName.ShouldBe("Hay una sugerencia de perfil para Excel");
        panel.Selector.ButtonState.ShouldBe("app activa · Hay una sugerencia de perfil para Excel");

        panel.ApplyContext(PanelBodyContext.Idle);
        panel.Selector.HasSuggestion.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SEL-006")]
    public void Without_the_selector_row_the_title_opens_the_grid_and_the_grid_offers_Frequents()
    {
        var panel = Panel();
        panel.TitleOpensPicker.ShouldBeFalse();
        panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true });
        panel.Picker.HasFrequents.ShouldBeFalse();

        panel.ApplyLayout(PanelLayoutSettings.Default with { ShowSelectorRow = false });

        panel.Selector.IsVisible.ShouldBeFalse();
        panel.TitleOpensPicker.ShouldBeTrue();
        panel.Picker.IsVisible.ShouldBeTrue();
        panel.Picker.HasFrequents.ShouldBeTrue();
        panel.Picker.FrequentsName.ShouldBe("Frecuentes");
        panel.Picker.IsFrequentsCurrent.ShouldBeFalse();

        panel.Picker.Frequents();
        _intents.Calls.ShouldBe(["ShowFrequents"]);

        panel.ApplyContext(PanelBodyContext.Idle with { PickerOpen = true, Frequents = true });
        panel.Picker.IsFrequentsCurrent.ShouldBeTrue();
        panel.Picker.FrequentsState.ShouldBe("Activado");
        panel.Picker.Entries.ShouldAllBe(static entry => !entry.IsCurrent);
    }

    [Fact]
    [Trait("Req", "SEL-006")]
    public void The_title_is_not_a_button_in_Compact_nor_while_searching()
    {
        var panel = Panel();

        panel.ApplyLayout(
            PanelLayoutSettings.Default with
            {
                ShowSelectorRow = false,
                Compact = true,
            }
        );
        panel.TitleOpensPicker.ShouldBeFalse();

        panel.ApplyLayout(PanelLayoutSettings.Default with { ShowSelectorRow = false });
        panel.ApplyContext(PanelBodyContext.Idle with { SearchingWithText = true });
        panel.TitleOpensPicker.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "SEL-006")]
    [Trait("Req", "ACC-004")]
    public void The_title_of_the_header_runs_its_action_only_while_it_opens_the_grid()
    {
        var world = new InteractionsWorld();
        var taps = 0;
        var header = new PanelHeaderViewModel(
            new ProfileViewCoordinator(world.Store),
            world.Localization,
            PanelHeaderActions.None with
            {
                Title = () => taps++,
            }
        );

        header.TitleTapped();
        taps.ShouldBe(0);

        header.ApplyPicker(titleOpensPicker: true, pickerOpen: false);
        header.TitleOpensPicker.ShouldBeTrue();
        header.TitleHelp.ShouldBe("Cambiar de perfil");
        header.TitleTapped();
        taps.ShouldBe(1);

        header.ApplyPicker(titleOpensPicker: true, pickerOpen: true);
        header.IsPickerOpen.ShouldBeTrue();

        var plain = new PanelHeaderViewModel(
            new ProfileViewCoordinator(world.Store),
            world.Localization,
            PanelHeaderActions.None
        );
        plain.ApplyPicker(titleOpensPicker: true, pickerOpen: false);
        plain.TitleOpensPicker.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "EJE-002")]
    [Trait("Req", "ACC-003")]
    public void The_armed_tile_is_marked_and_says_it_waits_for_the_second_tap()
    {
        var panel = Panel();
        var armed = new ShortcutId("w1");

        panel.ApplyEngine(
            EngineSnapshot.Empty with
            {
                Armed = new ArmedConfirmation(armed, DateTimeOffset.UnixEpoch),
                Version = 1,
            }
        );

        var tile = panel.Tiles.Single(t => t.Id == armed);
        tile.IsArmed.ShouldBeTrue();
        tile.AccessibleState.ShouldBe("Toca otra vez para confirmar");
        panel.Tiles.Where(t => t.Id != armed).ShouldAllBe(static t => !t.IsArmed);

        panel.ApplyEngine(EngineSnapshot.Empty with { Version = 2 });

        tile.IsArmed.ShouldBeFalse();
        tile.AccessibleState.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "BUR-004")]
    public void The_panel_knows_when_the_engine_is_paused()
    {
        var panel = Panel();
        var changes = new List<string?>();
        panel.PropertyChanged += (_, change) => changes.Add(change.PropertyName);

        panel.ApplyEngine(EngineSnapshot.Empty with { Paused = true, Version = 1 });
        panel.IsPaused.ShouldBeTrue();
        changes.ShouldContain(nameof(PanelViewModel.IsPaused), StringComparer.Ordinal);

        panel.ApplyEngine(EngineSnapshot.Empty with { Version = 2 });
        panel.IsPaused.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    public void An_ignored_touch_in_normal_use_asks_for_its_discreet_answer()
    {
        var world = new InteractionsWorld();
        var answered = new List<ShortcutId>();
        world.Modes.IgnoredFeedback = tile => answered.Add(tile.Id);
        var tile = world.Tile(InteractionsWorld.Bold);

        world.Modes.Ignored(tile, IgnoreReason.Debounced);
        world.Modes.Ignored(tile, IgnoreReason.Moved);
        world.Modes.Ignored(tile, IgnoreReason.TooShort);

        answered.ShouldBe([tile.Id, tile.Id, tile.Id]);
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    [Trait("Req", "TAC-008")]
    public void A_palm_test_mode_and_edit_mode_get_no_such_answer()
    {
        var world = new InteractionsWorld();
        var answered = 0;
        world.Modes.IgnoredFeedback = _ => answered++;
        var tile = world.Tile(InteractionsWorld.Bold);

        world.Modes.Ignored(tile, IgnoreReason.Palm);
        world.TestMode.Start();
        world.Modes.Ignored(tile, IgnoreReason.Debounced);
        world.TestMode.MarkOf(tile.Id).ShouldNotBeNull();
        world.TestMode.Stop();
        world.EditMode.Toggle();
        world.Modes.Ignored(tile, IgnoreReason.Debounced);

        answered.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "TAC-003")]
    public void The_outline_of_an_ignored_touch_is_a_state_of_its_tile()
    {
        var tile = new InteractionsWorld().Tile(InteractionsWorld.Bold);
        var changes = new List<string?>();
        tile.PropertyChanged += (_, change) => changes.Add(change.PropertyName);

        tile.ShowIgnored(true);
        tile.IsIgnored.ShouldBeTrue();
        tile.ShowIgnored(false);

        tile.IsIgnored.ShouldBeFalse();
        changes.ShouldBe([nameof(TileViewModel.IsIgnored), nameof(TileViewModel.IsIgnored)]);
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void A_finger_that_stays_within_the_cancel_distance_does_not_scroll()
    {
        var pan = new PanScroll();

        pan.Down(7, y: 500, offset: 40).ShouldBeTrue();
        pan.Move(7, y: 510, thresholdPx: 24, scale: 1).ShouldBeNull();
        pan.Move(7, y: 480, thresholdPx: 24, scale: 1).ShouldBeNull();

        pan.Up(7).ShouldBeFalse();
        pan.IsTracking.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void Past_the_cancel_distance_the_content_follows_the_finger_and_the_contact_is_not_a_tap()
    {
        var pan = new PanScroll();
        pan.Down(7, y: 500, offset: 40).ShouldBeTrue();

        pan.Move(7, y: 440, thresholdPx: 24, scale: 2).ShouldBe(70);
        pan.IsScrolling.ShouldBeTrue();
        // Back near the start it is still a scroll: the finger never becomes a tap again.
        pan.Move(7, y: 495, thresholdPx: 24, scale: 2).ShouldBe(42.5);

        pan.Up(7).ShouldBeTrue();
        pan.IsScrolling.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "TAC-004")]
    public void Only_the_first_finger_scrolls_and_another_one_changes_nothing()
    {
        var pan = new PanScroll();
        pan.Down(1, y: 100, offset: 0).ShouldBeTrue();

        pan.Down(2, y: 300, offset: 0).ShouldBeFalse();
        pan.Move(2, y: 600, thresholdPx: 24, scale: 1).ShouldBeNull();
        pan.Up(2).ShouldBeFalse();

        pan.IsTracking.ShouldBeTrue();
        pan.Move(1, y: 60, thresholdPx: 24, scale: 1).ShouldBe(40);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    [Trait("Req", "EJE-003")]
    public void General_and_Always_visible_inherit_the_mode_of_the_profile_of_the_app_in_front()
    {
        var library = CompatLibrary();

        var general = PanelProjector.Project(
            library,
            ProfileId.General,
            LangCode.Es,
            LangCode.Es,
            keys: null,
            PanelProjector.AppMode(library, Game)
        );

        general.Tiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.ScanCode);
        general.StripTiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.ScanCode);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Without_a_profile_for_the_app_in_front_General_uses_its_own_mode()
    {
        var library = CompatLibrary();
        PanelProjector.AppMode(library, null).ShouldBeNull();
        PanelProjector.AppMode(library, new ProfileId("gone")).ShouldBeNull();

        var general = PanelProjector.Project(library, ProfileId.General, LangCode.Es, LangCode.Es);

        general.Tiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.VirtualKey);
        general.StripTiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.VirtualKey);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void The_tiles_of_a_profile_keep_the_mode_of_their_own_profile()
    {
        var library = CompatLibrary();

        var game = PanelProjector.Project(library, Game, LangCode.Es, LangCode.Es);
        var word = PanelProjector.Project(
            library,
            PanelBodyTestData.Word,
            LangCode.Es,
            LangCode.Es,
            keys: null,
            InjectionMode.ScanCode
        );

        game.Tiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.ScanCode);
        game.StripTiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.VirtualKey);
        word.Tiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.VirtualKey);
        word.StripTiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.ScanCode);
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    [Trait("Req", "FRE-003")]
    public void Frequents_inherits_the_mode_of_the_app_in_front_and_otherwise_keeps_the_origin()
    {
        var library = CompatLibrary();
        var entries = new[] { Entry(library, "g0"), Entry(library, "w0"), Entry(library, "x0") };

        var inGame = PanelProjector.ProjectFrequents(
            library,
            entries,
            ProfileId.General,
            "Siempre visible",
            LangCode.Es,
            LangCode.Es,
            InjectionMode.ScanCode
        );
        var elsewhere = PanelProjector.ProjectFrequents(
            library,
            entries,
            ProfileId.General,
            "Siempre visible",
            LangCode.Es,
            LangCode.Es
        );

        inGame.Tiles.ShouldAllBe(static t => t.Binding.Injection == InjectionMode.ScanCode);
        elsewhere
            .Tiles.Select(static t => t.Binding.Injection)
            .ShouldBe([InjectionMode.VirtualKey, InjectionMode.VirtualKey, InjectionMode.ScanCode]);
    }

    private static readonly ProfileId Game = new("p-game");

    private static FrequentEntry Entry(ShortcutLibrary library, string id)
    {
        library.TryGetShortcut(new ShortcutId(id), out var shortcut).ShouldBeTrue();
        library.TryLocate(new ShortcutId(id), out var location).ShouldBeTrue();
        return new FrequentEntry(shortcut, location, Pinned: false, Uses: 1);
    }

    /// <summary>General (g0), Word (w0), a game in compatible mode (x0) and an Always visible row (a0).</summary>
    private static ShortcutLibrary CompatLibrary()
    {
        var plain = PanelBodyTestData.Library(1, 1);
        var general = plain.General with { Shortcuts = new ValueList<Shortcut>([Tap("g0")]) };
        var word = plain.Profiles.Single(p => p.Id == PanelBodyTestData.Word);
        var game = word with
        {
            Id = Game,
            Injection = InjectionMode.ScanCode,
            Shortcuts = new ValueList<Shortcut>([Tap("x0")]),
        };
        return ShortcutLibrary
            .CreateValidated(plain.AlwaysVisible, new ValueList<Profile>([general, word, game]))
            .Value;
    }

    private static Shortcut Tap(string id) =>
        PanelTestData.Shortcut(new ShortcutId(id), id, id, new TapAction(KeyChord.Empty, []));

    private PanelViewModel Panel() =>
        PanelBodyTestData.Panel(PanelBodyTestData.Library(3, 0), PanelBodyTestData.Word, _intents);
}
