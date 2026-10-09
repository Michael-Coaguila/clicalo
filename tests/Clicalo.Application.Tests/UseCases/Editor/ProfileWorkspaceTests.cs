using Clicalo.Application.Tests.Localization;
using Clicalo.Application.Tests.Store;
using Clicalo.Application.UseCases.Editor;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Icons;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Application.Tests.UseCases.Editor;

/// <summary>
/// The profile of the list in view (ATJ-004 to ATJ-008): name, icon, compatible mode, binding, capture and deletion,
/// each undoable.
/// </summary>
public sealed class ProfileWorkspaceTests
{
    private static readonly ProcessName Excel = new("EXCEL.EXE");
    private static readonly ProcessName WinWord = new("winword.exe");

    private static readonly EditorCatalogs Catalogs = EditorCatalogs.Empty with
    {
        Icons = new IconCatalog(
            [new IconEntry(new IconRef("table_chart"), ["hoja", "tabla", "sheet"])],
            [],
            []
        ),
    };

    private readonly StoreHarness _harness = new(StoreSamples.Document());
    private readonly ShortcutsWorkspace _shortcuts;
    private readonly ProfileWorkspace _profiles;
    private readonly List<WorkspaceNotice> _notices = [];

    public ProfileWorkspaceTests()
    {
        _shortcuts = new ShortcutsWorkspace(
            _harness.Store,
            I18nRepository.Context("es"),
            () => Catalogs,
            () => ProfileId.General
        );
        _profiles = new ProfileWorkspace(_harness.Store, _shortcuts, () => Catalogs);
        _profiles.Noticed += (_, e) => _notices.Add(e.Notice);
        _shortcuts.SelectList(new ListRef.InProfile(StoreSamples.Word));
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void The_name_is_the_same_in_every_language_and_an_empty_one_is_refused()
    {
        _profiles.Rename("  ").ShouldBeFalse();
        _profiles.Rename("Hoja de cálculo").ShouldBeTrue();

        var profile = _profiles.Current.ShouldNotBeNull();
        profile.Name.Get(LangCode.En, LangCode.Es).ShouldBe("Hoja de cálculo");
        profile.Icon.ShouldBe(new IconRef("table_chart"), "a profile's icon follows its name too");
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        _profiles.Current!.Name.Get(LangCode.Es, LangCode.Es).ShouldBe("Word");
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void Icon_and_compatible_mode_are_undoable_edits()
    {
        _profiles.SetIcon(new IconRef("work"));
        _profiles.SetCompatible(true);

        _profiles.Current!.Icon.ShouldBe(new IconRef("work"));
        _profiles.Current.AutoIcon.ShouldBeFalse();
        _profiles.Current.Injection.ShouldBe(InjectionMode.ScanCode);
        _harness.UndoAll().ShouldBe(1, "consecutive edits of a profile coalesce");
    }

    [Fact]
    [Trait("Req", "ATJ-006")]
    [Trait("Req", "ATJ-007")]
    public void A_process_of_another_profile_asks_before_moving_it()
    {
        _shortcuts.SelectList(new ListRef.InProfile(ProfileId.General));
        _harness.Dispatch(
            new CreateProfile(
                new Profile(
                    new ProfileId("x"),
                    LocalizedText.Same("Excel", LangCode.Es, LangCode.En),
                    new IconRef("table_chart"),
                    false,
                    new AppBinding.Manual(),
                    InjectionMode.VirtualKey,
                    [],
                    null
                ),
                ListPosition.End
            )
        );
        var excel = _harness.Store.Current.Library.Profiles[^1].Id;
        _shortcuts.SelectList(new ListRef.InProfile(excel));

        _profiles.Bind(WinWord);

        _profiles.PendingTakeOver.ShouldBe(new TakeOverRequest(excel, WinWord, StoreSamples.Word));
        _harness.Store.Current.Library.ProfileFor(WinWord)!.Id.ShouldBe(StoreSamples.Word);
        _profiles.AnswerTakeOver(yes: true);
        _profiles.PendingTakeOver.ShouldBeNull();
        _harness.Store.Current.Library.ProfileFor(WinWord)!.Id.ShouldBe(excel);
        _notices.ShouldContain(n => n.Text == L.LinkedToApp(process: WinWord.Value) && n.CanUndo);
    }

    [Fact]
    [Trait("Req", "ATJ-006")]
    public void None_makes_the_profile_manual()
    {
        _profiles.Unlink();

        _profiles.Current!.Binding.ShouldBe(new AppBinding.Manual());
        _notices.ShouldContain(n => n.Text == L.LinkRemoved && n.CanUndo);
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        _harness.Store.Current.Library.ProfileFor(WinWord)!.Id.ShouldBe(StoreSamples.Word);
    }

    [Fact]
    [Trait("Req", "ATJ-008")]
    public void Capture_binds_the_next_app_in_front_once()
    {
        _profiles.StartCapture();

        _profiles.Capturing.ShouldBe(StoreSamples.Word);
        _notices.ShouldContain(n => n.Text == L.WaitingApp);
        _profiles.OnForeground(Excel);
        _profiles.OnForeground(new ProcessName("notepad.exe"));

        _profiles.Capturing.ShouldBeNull();
        _harness.Store.Current.Library.ProfileFor(Excel)!.Id.ShouldBe(StoreSamples.Word);
        _harness
            .Store.Current.Library.ProfileFor(WinWord)!
            .Id.ShouldBe(StoreSamples.Word, "added, not replaced");
        _harness.Store.Current.Library.ProfileFor(new ProcessName("notepad.exe")).ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "ATJ-008")]
    public void Cancel_ends_the_capture()
    {
        _profiles.StartCapture();
        _profiles.CancelCapture();
        _profiles.OnForeground(Excel);

        _harness.Store.Current.Library.ProfileFor(Excel).ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    [Trait("Req", "PER-008")]
    [Trait("Req", "REG-04")]
    public void Deleting_the_profile_goes_to_General_and_can_be_undone()
    {
        _profiles.Delete(_harness.TokenFor(new DeleteProfile(StoreSamples.Word)));

        _shortcuts.List.ShouldBe(new ListRef.InProfile(ProfileId.General));
        _notices.ShouldContain(n => n.Text == L.ProfDeleted && n.CanUndo);
        _harness.Store.Undo().IsSuccess.ShouldBeTrue();
        _harness.Store.Current.Library.TryGetProfile(StoreSamples.Word, out _).ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    public void General_cannot_be_deleted_nor_capture()
    {
        _shortcuts.SelectList(new ListRef.InProfile(ProfileId.General));

        _profiles.StartCapture();
        _profiles.Delete(_harness.TokenFor(new DeleteProfile(ProfileId.General)));

        _profiles.Capturing.ShouldBeNull();
        _harness.Store.Current.Library.TryGetProfile(ProfileId.General, out _).ShouldBeTrue();
    }
}
