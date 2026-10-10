using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Presentation.ControlCenter.Shortcuts;
using Clicalo.TestKit.Windows.Rendering;

namespace Clicalo.Windowing.IntegrationTests.ControlCenter;

/// <summary>
/// What the Shortcuts section and its editor project for the view (docs/05), with the texts of <c>data/i18n</c> in
/// Spanish: the «Tu panel muestra» bar, the header of the list, the empty editor, the Text field, the two switches of
/// «Más opciones» and the «Probar» card. Only view models: no window, no desktop and no input.
/// </summary>
public sealed class ShortcutsProjectionTests
{
    private static readonly ShortcutId Bold = new("bold");

    [Fact]
    [Trait("Req", "ATJ-001")]
    public void The_top_bar_says_what_the_panel_shows()
    {
        Run(
            open: true,
            (_, section) =>
            {
                var bar = section.Screen.TopBar;

                bar.Title.ShouldBe("Tu panel muestra:");
                bar.Layers.Select(static l => (l.Icon, l.Label, l.Meta))
                    .ShouldBe([
                        ("push_pin", "Siempre visible", "1"),
                        ("description", "Word", "6 · app activa"),
                        ("star", "Frecuentes", "automático"),
                    ]);
                bar.Duplicates.ShouldNotBeNull("the chip of repeated combinations (REP-003)");
            }
        );
    }

    [Fact]
    [Trait("Req", "ATJ-003")]
    public void The_header_of_the_list_names_it_says_how_it_shows_and_adds_from_the_library()
    {
        Run(
            open: true,
            (world, section) =>
            {
                section.Screen.Header.ShouldBe(
                    new ListHeaderModel(
                        "description",
                        "Word",
                        "Se activa solo al abrir WINWORD.EXE",
                        CanEdit: true,
                        Editing: false,
                        "Editar perfil",
                        "Añadir"
                    )
                );

                section.SelectList(new ListRef.InProfile(ProfileId.General));
                section.Refresh();
                section.Screen.Header.Subtitle.ShouldBe("Se elige a mano desde las pestañas.");

                section.SelectList(new ListRef.AlwaysVisible());
                section.Refresh();
                var always = section.Screen.Header;
                always.Title.ShouldBe("Siempre visible");
                always.Subtitle.ShouldBe("Aparece en todas las apps. Ideal para dictado y voz.");
                always.CanEdit.ShouldBeFalse("Always visible has no profile to edit");

                section.OpenLibrary();
                section.Refresh();
                section.Screen.Column.ShouldBe(
                    EditorColumn.Library,
                    "«+ Añadir» opens the library"
                );
                world.Shortcuts.List.ShouldBe(new ListRef.AlwaysVisible());
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-020")]
    public void Without_a_shortcut_or_the_library_the_editor_column_asks_to_pick_one()
    {
        Run(
            open: false,
            (_, section) =>
            {
                section.Screen.Column.ShouldBe(EditorColumn.Empty);
                section.Screen.EmptyText.ShouldBe("Elige un atajo para editarlo.");
                section.Editor.Model.ShouldBeNull();
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-001")]
    public void The_identity_shows_the_preview_tile_the_name_with_dictation_and_how_the_icon_is_chosen()
    {
        Run(
            open: true,
            (_, section) =>
            {
                var identity = section.Editor.Model.ShouldNotBeNull().Identity;
                identity.Icon.ShouldBe("format_bold");
                identity.TileName.ShouldBe("Negrita");
                identity.Name.ShouldBe("Negrita");
                identity.Placeholder.ShouldNotBeNullOrWhiteSpace();
                identity.DictateName.ShouldBe("Dictar nombre");
                identity.HintIcon.ShouldBe("auto_awesome", "the icon follows the name");
                identity.PickerOpen.ShouldBeFalse();

                section.Editor.TogglePicker();
                section.Editor.PickIcon("save");
                section.Refresh();

                var model = section.Editor.Model.ShouldNotBeNull();
                model.Picker.ShouldNotBeNull("touching the preview tile opens the icon picker");
                model.Identity.PickerOpen.ShouldBeTrue();
                model.Identity.Icon.ShouldBe("save");
                model.Identity.HintIcon.ShouldBe("edit", "an icon chosen by hand");
                string.Equals(model.Identity.Hint, identity.Hint, StringComparison.Ordinal)
                    .ShouldBeFalse();

                section.SelectTile(new ShortcutId("empty"));
                section.Refresh();

                var unnamed = section.Editor.Model.ShouldNotBeNull().Identity;
                unnamed.Name.ShouldBeEmpty();
                unnamed.TileName.ShouldNotBeNullOrWhiteSpace("the tile shows a placeholder");
            }
        );
    }

    [Fact]
    [Trait("Req", "REP-004")]
    public void A_repeated_combination_shows_its_card_folded_with_how_many_places_repeat_it()
    {
        Run(
            open: true,
            (_, section) =>
            {
                section
                    .Editor.Model.ShouldNotBeNull()
                    .Duplicate.ShouldBeNull("Ctrl + N is only here");

                section.SelectTile(new ShortcutId("ccopy"));
                section.Refresh();

                var card = section.Editor.Model.ShouldNotBeNull().Duplicate.ShouldNotBeNull();
                card.Head.ShouldBe("Combinación repetida en 2 sitios · 1 de 1");
                card.Expanded.ShouldBeFalse("the card starts folded");
                card.PrevName.ShouldNotBeNullOrWhiteSpace();
                card.NextName.ShouldNotBeNullOrWhiteSpace();
                card.Rows.Count.ShouldBe(2, "Always visible and Navegador");

                section.Editor.ShowRepeated(1);
                section.Refresh();
                section
                    .Editor.Model.ShouldNotBeNull()
                    .Duplicate.ShouldNotBeNull()
                    .Head.ShouldEndWith(
                        "1 de 1",
                        Case.Sensitive,
                        "the only repeated one: it goes round"
                    );

                section.Editor.ToggleDuplicates();
                section.Refresh();
                section
                    .Editor.Model.ShouldNotBeNull()
                    .Duplicate.ShouldNotBeNull()
                    .Expanded.ShouldBeTrue();
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-011")]
    public void A_text_shortcut_has_its_field_with_dictation_and_the_privacy_note()
    {
        Run(
            open: true,
            (_, section) =>
            {
                section.Editor.Model.ShouldNotBeNull().Text.ShouldBeNull("«Negrita» sends keys");

                section.Editor.SetKind(ActionKind.Text);
                section.Refresh();

                var field = section.Editor.Model.ShouldNotBeNull().Text.ShouldNotBeNull();
                field.Label.ShouldBe("Texto a escribir");
                field.Hint.ShouldBe("Puedes dictarlo con el botón del micrófono.");
                field.DictateName.ShouldNotBeNullOrWhiteSpace();
                field.Encrypted.ShouldBe("Se guarda cifrado en tu equipo.");
            }
        );
    }

    [Fact]
    [Trait("Req", "EDI-017")]
    [Trait("Req", "EDI-018")]
    public void More_options_has_the_switches_to_ask_before_running_and_to_pin_in_Frequents()
    {
        Run(
            open: true,
            (world, section) =>
            {
                var more = section.Editor.Model.ShouldNotBeNull().More;
                more.ConfirmTitle.ShouldBe("Pedir confirmación antes de ejecutar");
                more.Confirm.ShouldBeFalse();
                more.FrequentsTitle.ShouldBe("Fijar en Frecuentes");
                more.Frequents.ShouldBeFalse();

                section.Editor.ToggleConfirm();
                section.Editor.ToggleFrequents();
                section.Refresh();

                more = section.Editor.Model.ShouldNotBeNull().More;
                more.Confirm.ShouldBeTrue();
                more.Frequents.ShouldBeTrue();
                world.Store.Current.Library.TryGetShortcut(Bold, out var bold).ShouldBeTrue();
                bold!.Options.Confirm.ShouldBeTrue();
                world.Store.Current.Frequents.Pins.ShouldContain(Bold);

                section.Editor.ToggleConfirm();
                section.Editor.ToggleFrequents();
                section.Refresh();

                more = section.Editor.Model.ShouldNotBeNull().More;
                more.Confirm.ShouldBeFalse();
                more.Frequents.ShouldBeFalse();
                world.Store.Current.Frequents.Pins.ShouldNotContain(Bold);
            }
        );
    }

    [Fact]
    [Trait("Req", "PRB-001")]
    public void The_try_card_shows_the_sequence_the_sentence_of_the_kind_and_the_phase()
    {
        Run(
            open: true,
            (world, section) =>
            {
                section.Editor.Model.ShouldNotBeNull().Test.ShouldBeNull("the card starts closed");

                section.Editor.ToggleTest();
                WpfThread.DrainPendingWork();
                section.Refresh();

                var card = section.Editor.Model.ShouldNotBeNull().Test.ShouldNotBeNull();
                card.Title.ShouldBe("Qué hará este atajo");
                card.CloseName.ShouldNotBeNullOrWhiteSpace();
                card.Sequence.Select(static s => (s.Label, s.Separator))
                    .ShouldBe([("Ctrl", null), ("N", "+")]);
                card.PlayText.ShouldBe("Ver");
                card.What.ShouldBe("Pulsa estas teclas juntas y las suelta, como en un teclado.");
                card.Phase.ShouldBeNull("nothing plays until «Ver»");

                section.Editor.Play();
                world.Time.Advance(TimeSpan.FromSeconds(5));
                WpfThread.DrainPendingWork();
                section.Refresh();

                card = section.Editor.Model.ShouldNotBeNull().Test.ShouldNotBeNull();
                card.PhaseIcon.ShouldBe("check_circle");
                card.Phase.ShouldNotBeNullOrWhiteSpace();

                section.SelectTile(new ShortcutId("macro"));
                section.Refresh();
                section.Editor.ToggleTest();
                WpfThread.DrainPendingWork();
                section.Refresh();

                section
                    .Editor.Model.ShouldNotBeNull()
                    .Test.ShouldNotBeNull()
                    .Sequence.Select(static s => s.Separator)
                    .ShouldBe([null, "→", "→"], "the steps of a macro are joined by arrows");
            }
        );
    }

    [Fact]
    [Trait("Req", "PRB-003")]
    public void Try_in_offers_the_open_apps_starts_on_the_last_one_and_keeps_the_choice()
    {
        Run(
            open: true,
            (_, section) =>
            {
                section.Editor.ToggleTest();
                WpfThread.DrainPendingWork();
                section.Refresh();

                var card = section.Editor.Model.ShouldNotBeNull().Test.ShouldNotBeNull();
                card.TargetsLabel.ShouldBe("Probar en");
                card.Targets.Select(static t => (t.Name, t.Selected))
                    .ShouldBe(
                        [("Word", true), ("Chrome", false)],
                        "the last app in front that is not Clícalo"
                    );
                card.NoApps.ShouldBeNull();

                section.Editor.ChooseTarget("chrome.exe");
                section.SelectTile(new ShortcutId("italic"));
                section.Refresh();
                section.Editor.ToggleTest();
                WpfThread.DrainPendingWork();
                section.Refresh();

                section
                    .Editor.Model.ShouldNotBeNull()
                    .Test.ShouldNotBeNull()
                    .Targets.Single(static t => t.Selected)
                    .Name.ShouldBe("Chrome", "the choice stays between shortcuts");

                section.Editor.ApplyApps([]);
                section.Refresh();

                card = section.Editor.Model.ShouldNotBeNull().Test.ShouldNotBeNull();
                card.Targets.ShouldBeEmpty();
                card.NoApps.ShouldBe("Abre la app donde quieras probarlo");
                card.CanLive.ShouldBeFalse();
            }
        );
    }

    /// <summary>
    /// Runs <paramref name="test"/> on the WPF thread with the section over the sample document; with
    /// <paramref name="open"/> it shows Word with «Negrita» in the editor, as the Control Center opens it.
    /// </summary>
    private static void Run(
        bool open,
        Action<ControlCenterTestWorld, ShortcutsSectionViewModel> test
    ) =>
        WpfThread.Invoke(() =>
        {
            var world = new ControlCenterTestWorld();
            if (open)
            {
                world.Shortcuts.Open(
                    new ListRef.InProfile(ControlCenterTestWorld.Word),
                    null,
                    false
                );
            }

            var section = new ShortcutsSectionViewModel(world.Services);
            world.Store.Changed += (_, _) => world.Shortcuts.OnDocumentChanged();
            section.Refresh();
            try
            {
                test(world, section);
            }
            finally
            {
                section.Editor.Dispose();
            }
        });
}
