using Clicalo.Application.Tests.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Application.UseCases.Library;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Privacy;
using Clicalo.Domain.Templates;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>
/// The «Atajos» section as a use case (docs/05 §1): drafts without trace (ATJ-011), one undo step per shortcut
/// (EDI-021), the library (ATJ-010), the editor's footer (EDI-019), pinning (EDI-015) and repeated combinations
/// (REP-005, REP-006). The sample document: Always visible «Copiar»; General «Deshacer»; Word «Negrita» and «Guardar».
/// </summary>
public sealed class ShortcutsWorkspaceTests
{
    private static readonly ListRef Word = new ListRef.InProfile(StoreSamples.Word);

    private static readonly EditorCatalogs Catalogs = EditorCatalogs.Empty with
    {
        Icons = new IconCatalog(
            [
                new IconEntry(new IconRef("save"), ["guardar", "save"]),
                new IconEntry(new IconRef("picture_as_pdf"), ["pdf"]),
            ],
            [],
            []
        ),
        Library = new LibraryContent(
            2,
            [
                new LibrarySection(
                    "edit",
                    [
                        new TemplateShortcut(
                            "paste",
                            LocalizedText.Same("Pegar", LangCode.Es, LangCode.En),
                            new IconRef("content_paste"),
                            new CategoryId("edit"),
                            new TapAction(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.V), []),
                            false
                        ),
                    ]
                ),
            ]
        ),
    };

    private readonly StoreHarness _harness = new(StoreSamples.Document());
    private readonly ShortcutsWorkspace _workspace;
    private readonly List<WorkspaceNotice> _notices = [];

    public ShortcutsWorkspaceTests()
    {
        _workspace = new ShortcutsWorkspace(
            _harness.Store,
            I18nRepository.Context("es"),
            () => Catalogs,
            () => ProfileId.General
        );
        _workspace.Noticed += (_, e) => _notices.Add(e.Notice);
    }

    private UserDocument Document => _harness.Store.Current;

    [Fact]
    [Trait("Req", "ATJ-002")]
    public void Choosing_a_list_opens_its_first_shortcut()
    {
        _workspace.SelectList(Word);

        _workspace.Pane.ShouldBe(new EditorPane.Editing(StoreSamples.Bold));
    }

    [Fact]
    [Trait("Req", "ATJ-011")]
    [Trait("Req", "ATJ-010")]
    public void A_blank_draft_leaves_no_trace()
    {
        _workspace.SelectList(Word);
        _workspace.CreateOwn();

        _workspace.Pane.ShouldBeOfType<EditorPane.Draft>();
        _notices.ShouldContain(n => n.Text == L.NewCreated);
        _workspace.SelectList(Word);

        _harness.Changes.ShouldBeEmpty();
        _harness.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "ATJ-011")]
    [Trait("Req", "EDI-003")]
    public void The_first_edit_creates_the_draft_and_a_draft_blank_again_is_discarded_without_trace()
    {
        _workspace.SelectList(Word);
        _workspace.CreateOwn();

        _workspace.Rename("Guardar como PDF");
        var created = _workspace.Selected.ShouldNotBeNull();

        _workspace.Pane.ShouldBe(new EditorPane.Editing(created.Id));
        created.Name.Get(LangCode.En, LangCode.Es).ShouldBe("Guardar como PDF", "EDI-002");
        created.Icon.ShouldBe(new IconRef("save"), "the icon follows the name");
        _workspace.Shortcuts[^1].Id.ShouldBe(created.Id);

        _workspace.Rename(string.Empty);
        _workspace.Select(StoreSamples.Bold);

        Document.Library.TryGetShortcut(created.Id, out _).ShouldBeFalse();
        _harness.Store.CanUndo.ShouldBeFalse("creating, editing and discarding cancel out");
    }

    [Fact]
    [Trait("Req", "EDI-021")]
    [Trait("Req", "REG-07")]
    public void The_edits_of_one_shortcut_are_one_undo_step_until_the_editor_moves_on()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.Rename("Negrita fuerte");
        _workspace.TapKey(KeyIds.Shift);
        _workspace.SetIcon(new IconRef("format_bold"));
        _workspace.Select(StoreSamples.Save);
        _workspace.Rename("Guardar todo");

        _harness.UndoAll().ShouldBe(2);
        Document.ShouldBe(StoreSamples.Document() with { Revision = Document.Revision });
    }

    [Fact]
    [Trait("Req", "EDI-003")]
    public void An_icon_chosen_by_hand_no_longer_follows_the_name()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.SetIcon(new IconRef("star"));
        _workspace.Rename("Guardar");

        _workspace.Selected!.Icon.ShouldBe(new IconRef("star"));
        _workspace.Selected.AutoIcon.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "EDI-007")]
    public void Clearing_the_keys_is_its_own_undo_step()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.Rename("Negrita 2");
        _workspace.ClearKeys();

        _workspace.ChordInBox().ShouldBe(KeyChord.Empty);
        _notices.ShouldContain(n => n.Text == L.ComboCleared && n.CanUndo);
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        _workspace.ChordInBox().ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N));
        _workspace.Selected!.Name.Get(LangCode.Es, LangCode.Es).ShouldBe("Negrita 2");
    }

    [Fact]
    [Trait("Req", "EDI-006")]
    public void Switching_kinds_back_and_forth_keeps_what_each_kind_had()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.SetKind(ActionKind.Text);
        _workspace.SetText("hola");
        _workspace.SetKind(ActionKind.Hold);
        _workspace.SetKind(ActionKind.Text);

        ((TextAction)_workspace.Selected!.Action).Text.ShouldBe(SecretText.From("hola"));
        _workspace.SetKind(ActionKind.Tap);
        _workspace.ChordInBox().ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N));
    }

    [Fact]
    [Trait("Req", "ATJ-010")]
    public void A_ready_action_is_added_at_the_end_with_its_catalog_reference_once()
    {
        _workspace.SelectList(Word);
        _workspace.OpenLibrary();
        var category = _workspace
            .Categories()
            .Single(c => string.Equals(c.Id, "edit", StringComparison.Ordinal));

        _workspace.AddFromLibrary(category, category.Items[0]);
        _workspace.AddFromLibrary(category, category.Items[0]);

        var added = _workspace.Shortcuts[^1];
        _workspace.Shortcuts.Count.ShouldBe(3);
        added.Origin.ShouldBe(new CatalogRef(LibraryContent.Source, "2", "paste"));
        _workspace.Pane.ShouldBeOfType<EditorPane.Library>("the library stays open");
        _notices.ShouldContain(n => n.Text == L.AddedToProf(profile: "Word", name: "Pegar"));
        LibraryMatching
            .IsAdded(_workspace.Shortcuts, category.Items[0], LibraryContent.Source)
            .ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EDI-019")]
    public void Duplicate_puts_the_copy_right_after_it_and_selects_it()
    {
        _workspace.Select(StoreSamples.Bold);

        _workspace.Duplicate();

        _workspace
            .Shortcuts.Select(s => s.Name.Get(LangCode.Es, LangCode.Es))
            .ShouldBe(["Negrita", "Negrita (copia)", "Guardar"]);
        _workspace.Selected!.Id.ShouldBe(_workspace.Shortcuts[1].Id);
    }

    [Fact]
    [Trait("Req", "EDI-019")]
    [Trait("Req", "REG-04")]
    public void Delete_needs_the_second_tap_then_opens_the_first_and_can_be_undone()
    {
        _workspace.Select(StoreSamples.Save);

        _workspace.Delete(_harness.TokenFor(new DeleteShortcut(StoreSamples.Save)));

        Document.Library.TryGetShortcut(StoreSamples.Save, out _).ShouldBeFalse();
        _workspace.Pane.ShouldBe(new EditorPane.Editing(StoreSamples.Bold));
        _notices.ShouldContain(n => n.Text == L.Deleted && n.CanUndo);
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        Document.Library.TryGetShortcut(StoreSamples.Save, out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "EDI-015")]
    public void Pinning_moves_the_shortcut_and_unpinning_returns_it_to_its_profile()
    {
        _workspace.Select(StoreSamples.Save);

        _workspace.SetPinned(true);

        _workspace.List.ShouldBe(new ListRef.AlwaysVisible());
        Document.Library.AlwaysVisible[^1].Id.ShouldBe(StoreSamples.Save);
        _workspace.SetPinned(false);
        _workspace.List.ShouldBe(Word);
        _workspace.Shortcuts[^1].Id.ShouldBe(StoreSamples.Save);
    }

    [Fact]
    [Trait("Req", "EDI-016")]
    public void The_position_buttons_move_it_inside_its_list()
    {
        _workspace.Select(StoreSamples.Save);

        _workspace.Move(PositionMove.First);

        _workspace.Shortcuts.Select(s => s.Id).ShouldBe([StoreSamples.Save, StoreSamples.Bold]);
        _workspace.Reorder(StoreSamples.Save, null);
        _workspace.Shortcuts.Select(s => s.Id).ShouldBe([StoreSamples.Bold, StoreSamples.Save]);
        _workspace.Reorder(StoreSamples.Save, StoreSamples.Bold);
        _workspace.Shortcuts.Select(s => s.Id).ShouldBe([StoreSamples.Save, StoreSamples.Bold]);
    }

    [Fact]
    [Trait("Req", "EDI-013")]
    public void A_new_step_opens_and_the_keys_go_to_it()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.SetKind(ActionKind.Macro);

        _workspace.AddStep(MacroStepKind.Keys);
        _workspace.TapKey(KeyIds.F5);
        _workspace.AddStep(MacroStepKind.Wait);

        _workspace.EditingStep.ShouldBe(1);
        var macro = (MacroAction)_workspace.Selected!.Action;
        macro.Steps[0].ShouldBe(new KeysStep(KeyChord.FromKeys(KeyIds.F5)));
        _workspace.DeleteStep(0, _harness.TokenFor(new DeleteMacroStep(StoreSamples.Bold, 0)));
        ((MacroAction)_workspace.Selected!.Action)
            .Steps.ShouldHaveSingleItem()
            .ShouldBeOfType<WaitStep>();
        _workspace.EditingStep.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "REP-006")]
    public void Using_another_combination_empties_the_box_and_keep_old_brings_it_back()
    {
        _workspace.Select(StoreSamples.Bold);

        _workspace.UseOtherCombination();

        _workspace.ReplacedChord.ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N));
        _workspace.ChordInBox().ShouldBe(KeyChord.Empty);
        _workspace.KeepOldCombination();
        _workspace.ReplacedChord.ShouldBeNull();
        _workspace.ChordInBox().ShouldBe(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.N));
        _notices.ShouldContain(n => n.Text == L.KeptOld);
    }

    [Fact]
    [Trait("Req", "REP-003")]
    [Trait("Req", "REP-005")]
    public void Repeated_combinations_are_reviewed_kept_in_Always_visible_or_accepted()
    {
        _workspace.Select(StoreSamples.Bold);
        _workspace.TapKey(KeyIds.N);
        _workspace.TapKey(KeyIds.C);
        _workspace.Rename("Copiar");
        _workspace.SelectList(new ListRef.InProfile(ProfileId.General));

        _workspace.ReviewDuplicates();
        _workspace.Pane.ShouldBe(
            new EditorPane.Editing(StoreSamples.Bold),
            "outside Always visible first"
        );
        _workspace.KeepOnlyInAlwaysVisible(
            _harness.TokenFor(new KeepOnlyInAlwaysVisible(StoreSamples.Bold))
        );

        Document.Library.TryGetShortcut(StoreSamples.Bold, out _).ShouldBeFalse();
        _workspace.List.ShouldBe(new ListRef.AlwaysVisible());
        _workspace.Pane.ShouldBe(new EditorPane.Editing(StoreSamples.Copy));
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        _workspace.OnDocumentChanged();
        _workspace.Select(StoreSamples.Bold);
        _workspace.AcceptRepeated();
        _workspace.Duplicates().RepeatedCombinationCount.ShouldBe(0);
        _notices.ShouldContain(n => n.Text == L.DupKept && n.CanUndo);
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void A_list_that_is_gone_becomes_General()
    {
        _workspace.Select(StoreSamples.Save);
        _harness.Dispatch(new DeleteProfile(StoreSamples.Word)).IsSuccess.ShouldBeTrue();

        _workspace.OnDocumentChanged();

        _workspace.List.ShouldBe(new ListRef.InProfile(ProfileId.General));
        _workspace.Pane.ShouldBe(new EditorPane.Editing(new ShortcutId("undo")));
    }
}
