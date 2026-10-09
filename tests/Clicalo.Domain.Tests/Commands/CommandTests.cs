using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Duplicates;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using static Clicalo.Domain.Tests.Library.LibraryBuilder;

namespace Clicalo.Domain.Tests.Commands;

/// <summary>What each document command does to the sample document, its undo intent and its backup (§6.3, §6.4).</summary>
public sealed class CommandTests
{
    private static readonly UserDocument Document = UserDocument.Create(
        Sample(),
        SettingsSchema.Defaults
    );
    private static readonly ListRef Always = new ListRef.AlwaysVisible();
    private static readonly ProfileId Word = new("word");
    private static readonly ShortcutId Bold = new("bold");

    [Fact]
    [Trait("Req", "DAT-004")]
    [Trait("Req", "EDI-021")]
    public void Creating_a_shortcut_gives_it_a_new_id_that_keys_its_undo_step()
    {
        var change = Apply(
            new CreateShortcut(
                new ListRef.InProfile(Word),
                TemplateNamed("Pegar"),
                ListPosition.At(0)
            )
        );

        var created = change.Events.OfType<ShortcutCreated>().Single();
        created.Id.Value.ShouldStartWith("ns");
        change.Next.Library.TryLocate(created.Id, out var location).ShouldBeTrue();
        location.ShouldBe(new ShortcutLocation(new ListRef.InProfile(Word), 0));
        change.Undo.ShouldBe(new UndoIntent.Record(L.NewCreated.Key, created.Id.Value));
        change.Backup.ShouldBe(new BackupRequirement.None());
    }

    [Fact]
    [Trait("Req", "ATJ-011")]
    public void A_blank_draft_is_never_created_and_only_a_blank_draft_is_discarded()
    {
        Fail(
            new CreateShortcut(
                Always,
                Shortcut("_", "", new TapAction(KeyChord.Empty, [])),
                ListPosition.End
            )
        )
            .Code.ShouldBe("command.draft.blank");
        Fail(new DiscardDraft(Bold)).Code.ShouldBe("command.draft.not_blank");

        var edited = Document.Library.TryGetShortcut(Bold, out var bold)
            ? bold with
            {
                Name = LocalizedText.Same("", LangCode.Es),
                Action = new TapAction(KeyChord.Empty, []),
            }
            : throw new InvalidOperationException();
        var blank = Apply(new EditShortcut(edited)).Next;
        var discarded = new DiscardDraft(Bold).Apply(blank, Contexts.Fresh()).Value;
        discarded.Next.Library.TryLocate(Bold, out _).ShouldBeFalse();
        discarded.Undo.ShouldBe(new UndoIntent.Record(L.Deleted.Key, Bold.Value));
    }

    [Fact]
    [Trait("Req", "EDI-019")]
    public void Duplicating_inserts_a_copy_right_after_it()
    {
        var change = Apply(
            new DuplicateShortcut(
                Bold,
                LocalizedText.Same("Negrita (copia)", LangCode.Es, LangCode.En)
            )
        );

        change.Next.Library.TryGetProfile(Word, out var word).ShouldBeTrue();
        word.Shortcuts.Select(s => s.Name.Get(LangCode.Es, LangCode.En))
            .ShouldBe(["Negrita", "Negrita (copia)", "Guardar"]);
        word.Shortcuts[1].Id.ShouldNotBe(Bold);
        word.Shortcuts[1].Action.ShouldBe(word.Shortcuts[0].Action);
    }

    [Fact]
    [Trait("Req", "EDI-021")]
    public void Editing_and_moving_a_shortcut_are_keyed_by_it()
    {
        Document.Library.TryGetShortcut(Bold, out var bold).ShouldBeTrue();

        Apply(new EditShortcut(bold with { AutoIcon = false }))
            .Undo.ShouldBe(new UndoIntent.Record(L.Saved.Key, "bold"));
        Apply(new MoveShortcut(Bold, Always, ListPosition.End))
            .Undo.ShouldBe(new UndoIntent.Record(L.Saved.Key, "bold"));
        Fail(new EditShortcut(bold with { Id = new ShortcutId("nope") }))
            .Code.ShouldBe("library.shortcut.not_found");
    }

    [Fact]
    [Trait("Req", "EDI-015")]
    public void Pinning_to_Always_visible_moves_it_and_unpinning_returns_it_to_its_profile()
    {
        var pinned = Apply(new PinToAlwaysVisible(Bold)).Next;

        pinned.Library.TryLocate(Bold, out var location).ShouldBeTrue();
        location.ShouldBe(new ShortcutLocation(Always, 1));
        pinned.Library.TryGetShortcut(Bold, out var bold).ShouldBeTrue();
        bold.PinnedFrom.ShouldBe(Word);
        pinned
            .Library.EnumerateShortcuts()
            .Count()
            .ShouldBe(Document.Library.EnumerateShortcuts().Count());

        var unpinned = new UnpinFromAlwaysVisible(Bold, ProfileId.General)
            .Apply(pinned, Contexts.Fresh())
            .Value.Next;
        unpinned.Library.TryLocate(Bold, out var back).ShouldBeTrue();
        back.ShouldBe(new ShortcutLocation(new ListRef.InProfile(Word), 1));
        unpinned.Library.TryGetShortcut(Bold, out var returned).ShouldBeTrue();
        returned.PinnedFrom.ShouldBeNull();
    }

    [Theory]
    [Trait("Req", "EDI-015")]
    [InlineData("chrome", "chrome")]
    [InlineData("ghost", "general")]
    public void Unpinning_without_its_profile_goes_to_the_shown_profile_or_to_General(
        string shown,
        string expected
    )
    {
        var pinned = Apply(new PinToAlwaysVisible(Bold)).Next;
        var withoutWord = new DeleteProfile(Word).Apply(pinned, Contexts.Fresh()).Value.Next;

        var unpinned = new UnpinFromAlwaysVisible(Bold, new ProfileId(shown))
            .Apply(withoutWord, Contexts.Fresh())
            .Value.Next;

        unpinned.Library.TryLocate(Bold, out var location).ShouldBeTrue();
        location.List.ShouldBe(new ListRef.InProfile(new ProfileId(expected)));
        Fail(new UnpinFromAlwaysVisible(new ShortcutId("save"), Word))
            .Code.ShouldBe("command.always.not_there");
    }

    [Fact]
    [Trait("Req", "REG-04")]
    [Trait("Req", "FRE-005")]
    public void Deleting_a_pinned_shortcut_leaves_its_pin_to_come_back_with_undo()
    {
        var pinned = Document with { Frequents = Document.Frequents.WithPin(Bold) };

        var change = new DeleteShortcut(Bold).Apply(pinned, Contexts.Fresh()).Value;

        change.Next.Library.TryLocate(Bold, out _).ShouldBeFalse();
        change.Next.Frequents.Pins.ShouldBe([Bold]);
        change.Events.ShouldBe([new ShortcutRemoved(Bold)]);
        change.Undo.ShouldBe(new UndoIntent.Record(L.Deleted.Key, null));
        change.Next.RestoreSlices(pinned, SliceDiff.Touched(pinned, change.Next)).ShouldBe(pinned);
    }

    [Fact]
    [Trait("Req", "DAT-004")]
    public void Creating_a_profile_gives_new_ids_to_it_and_to_its_shortcuts()
    {
        var template = Profile(
            "word",
            "Word 2",
            "excel.exe",
            Tap("bold", "Negrita", KeyIds.Ctrl, KeyIds.B)
        );

        var change = Apply(new CreateProfile(template, ListPosition.End));

        var created = change.Events.OfType<ProfileCreated>().Single().Id;
        change.Next.Library.TryGetProfile(created, out var profile).ShouldBeTrue();
        profile.Id.ShouldNotBe(Word);
        profile.Shortcuts.Single().Id.ShouldNotBe(Bold);
        change.Next.Validate().ShouldBeEmpty();
        Fail(
            new CreateProfile(
                template with
                {
                    Binding = new AppBinding.Processes([new ProcessName("winword.exe")]),
                },
                ListPosition.End
            )
        )
            .Code.ShouldBe("library.process.bound");
    }

    [Fact]
    [Trait("Req", "ATJ-004")]
    [Trait("Req", "REG-07")]
    public void Editing_a_profile_is_undoable_and_refuses_an_empty_name()
    {
        Document.Library.TryGetProfile(Word, out var word).ShouldBeTrue();

        Apply(new EditProfile(word with { Injection = InjectionMode.ScanCode }))
            .Undo.ShouldBe(new UndoIntent.Record(L.Saved.Key, "word"));
        Fail(new EditProfile(word with { Name = LocalizedText.Same("", LangCode.Es) }))
            .Code.ShouldBe("library.profile.name_empty");
    }

    [Fact]
    [Trait("Req", "PER-008")]
    [Trait("Req", "REG-04")]
    public void Deleting_a_profile_corrects_the_last_profile_and_never_deletes_General()
    {
        var withLast = Document with { Settings = Document.Settings with { LastProfile = Word } };

        var change = new DeleteProfile(Word).Apply(withLast, Contexts.Fresh()).Value;

        change.Next.Settings.LastProfile.ShouldBe(ProfileId.General);
        change.Events.ShouldBe([new ProfileRemoved(Word)]);
        change.Next.Validate().ShouldBeEmpty();
        Fail(new DeleteProfile(ProfileId.General)).Code.ShouldBe("library.general.protected");
    }

    [Fact]
    [Trait("Req", "ATJ-007")]
    public void Binding_a_taken_process_needs_the_take_over_and_reports_where_it_came_from()
    {
        var chrome = new ProfileId("chrome");

        Fail(new BindProcess(chrome, new ProcessName("winword.exe"), TakeOver: false))
            .Code.ShouldBe("library.process.bound");
        var change = Apply(new BindProcess(chrome, new ProcessName("winword.exe"), TakeOver: true));

        change.Events.ShouldBe([new ProcessBound(chrome, new ProcessName("winword.exe"), Word)]);
        change.Undo.ShouldBe(new UndoIntent.Record(L.LinkedTo.Key, null));
        Apply(new UnbindProcess(chrome, new ProcessName("chrome.exe")))
            .Undo.ShouldBe(new UndoIntent.Record(L.LinkRemoved.Key, null));
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    [Trait("Req", "REG-07")]
    public void A_behaviour_setting_is_an_undo_step_and_a_presentation_setting_is_not()
    {
        var behaviour = Apply(new SetSetting(SettingPaths.ReleaseOnAppSwitch, false));
        behaviour.Next.Settings.KeySafety.ReleaseOnAppSwitch.ShouldBeFalse();
        behaviour.Undo.ShouldBe(
            new UndoIntent.Record(L.SafeSwitch.Key, "setting:" + SettingPaths.ReleaseOnAppSwitch)
        );

        var presentation = Apply(new SetSetting(SettingPaths.Theme, ThemeChoice.Dark));
        presentation.Next.Settings.Theme.ShouldBe(ThemeChoice.Dark);
        presentation.Undo.ShouldBe(new UndoIntent.Transparent());
    }

    [Fact]
    public void A_setting_needs_a_known_path_its_type_its_range_and_an_existing_last_profile()
    {
        Fail(new SetSetting("nope", 1)).Code.ShouldBe("command.setting.unknown");
        Fail(new SetSetting(SettingPaths.Columns, "3")).Code.ShouldBe("command.setting.wrong_type");
        Fail(new SetSetting(SettingPaths.Columns, 5)).Code.ShouldBe("command.setting.out_of_range");
        Fail(new SetSetting(SettingPaths.LastProfile, new ProfileId("ghost")))
            .Code.ShouldBe("command.setting.out_of_range");
        Apply(new SetSetting(SettingPaths.LastProfile, Word))
            .Next.Settings.LastProfile.ShouldBe(Word);
        Apply(new SetSetting(SettingPaths.Columns, 3)).Next.ShouldBeSameAs(Document);
    }

    [Fact]
    public void The_panel_position_is_remembered_per_monitor_outside_the_history()
    {
        var first = Apply(new SetPanelPosition(new MonitorPosition(@"\\.\DISPLAY1", 10, 20)));
        var moved = new SetPanelPosition(new MonitorPosition(@"\\.\DISPLAY1", 30, 40))
            .Apply(first.Next, Contexts.Fresh())
            .Value;

        first.Undo.ShouldBe(new UndoIntent.Transparent());
        moved.Next.Settings.PanelPositions.ShouldBe([new MonitorPosition(@"\\.\DISPLAY1", 30, 40)]);
        Fail(new SetPanelPosition(new MonitorPosition("", 1, 1)))
            .Code.ShouldBe("command.position.monitor_empty");
    }

    [Fact]
    [Trait("Req", "FRE-002")]
    public void Usage_is_counted_outside_the_history_and_only_for_existing_shortcuts()
    {
        var change = Apply(new RecordUsage(Bold));

        change.Undo.ShouldBe(new UndoIntent.Transparent());
        change.Next.Frequents.Usage.Entries[Bold].ShouldBe([DomainGen.Now]);
        SliceDiff.Touched(Document, change.Next).ShouldBe(DocumentSlices.FrequentsUsage);
        Fail(new RecordUsage(new ShortcutId("gone"))).Code.ShouldBe("command.shortcut.not_found");
    }

    [Fact]
    [Trait("Req", "FRE-001")]
    [Trait("Req", "REG-04")]
    public void Pinning_unpinning_and_hiding_in_Frequents_are_value_edits_with_undo()
    {
        var pinned = Apply(new PinToFrequents(Bold));
        pinned.Next.Frequents.Pins.ShouldBe([Bold]);
        pinned.Undo.ShouldBe(new UndoIntent.Record(L.CtxPinT.Key, null));

        var hidden = new HideFromFrequents(Bold).Apply(pinned.Next, Contexts.Fresh()).Value;
        hidden.Next.Frequents.Pins.ShouldBeEmpty();
        hidden.Next.Frequents.Hidden.ShouldBe([Bold]);
        hidden.Undo.ShouldBe(new UndoIntent.Record(L.CtxHideT.Key, null));
        new HideFromFrequents(Bold).ShouldNotBeAssignableTo<IDestructiveCommand>();
        Apply(new UnpinFromFrequents(new ShortcutId("gone"))).Next.ShouldBeSameAs(Document);
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    [Trait("Req", "DAT-006")]
    public void Resetting_Frequents_backs_up_first_and_raises_the_epoch()
    {
        var used = Document with
        {
            Frequents = new FrequentsState([Bold], [], 4, DomainGen.Usage([(Bold, 1)])),
        };

        var change = new ResetFrequents().Apply(used, Contexts.Fresh()).Value;

        change.Next.Frequents.ShouldBe(
            FrequentsState.Empty with
            {
                UsageEpoch = 5,
                Usage = change.Next.Frequents.Usage,
            }
        );
        change.Backup.ShouldBe(new BackupRequirement.BeforeApply(BackupKind.PreResetFrequents));
        change.Undo.ShouldBe(new UndoIntent.Record(L.ResetFreqT.Key, null));
        SliceDiff
            .Touched(used, change.Next)
            .ShouldBe(DocumentSlices.FrequentsCuration | DocumentSlices.FrequentsUsage);
    }

    [Fact]
    [Trait("Req", "REP-005")]
    public void Marking_a_repetition_as_fine_adds_its_key_once()
    {
        CanonicalChord
            .TryFrom(KeyChord.FromKeys(KeyIds.Ctrl, KeyIds.C), out var key)
            .ShouldBeTrue();

        var change = Apply(new MarkDuplicateAccepted(key));
        change.Next.Duplicates.Ignored.ShouldBe([key]);
        change.Undo.ShouldBe(new UndoIntent.Record(L.DupKept.Key, null));
        new MarkDuplicateAccepted(key)
            .Apply(change.Next, Contexts.Fresh())
            .Value.Next.ShouldBeSameAs(change.Next);
    }

    [Fact]
    [Trait("Req", "REP-005")]
    [Trait("Req", "REG-04")]
    public void Only_a_repeated_appearance_is_deleted_from_the_card()
    {
        var repeated = Document with
        {
            Library = Document
                .Library.AddShortcut(
                    new ListRef.InProfile(Word),
                    Tap("bold2", "Otra", KeyIds.N, KeyIds.Ctrl),
                    ListPosition.End
                )
                .Value,
        };

        Fail(new DeleteDuplicate(new ShortcutId("save")))
            .Code.ShouldBe("command.duplicate.not_repeated");
        var change = new DeleteDuplicate(new ShortcutId("bold2"))
            .Apply(repeated, Contexts.Fresh())
            .Value;
        change.Next.Library.TryLocate(new ShortcutId("bold2"), out _).ShouldBeFalse();
        DuplicateIndex
            .Build(change.Next.Library, change.Next.Duplicates)
            .RepeatedCombinationCount.ShouldBe(0);
    }

    [Fact]
    [Trait("Req", "EDI-013")]
    [Trait("Req", "REG-04")]
    public void Deleting_a_macro_step_keeps_the_others_in_order()
    {
        var macro = Shortcut(
            "m",
            "Macro",
            new MacroAction([
                new WaitStep(TimeSpan.FromMilliseconds(100)),
                new WaitStep(TimeSpan.FromMilliseconds(200)),
                new WaitStep(TimeSpan.FromMilliseconds(300)),
            ])
        );
        var document = Document with
        {
            Library = Document.Library.AddShortcut(Always, macro, ListPosition.End).Value,
        };

        var change = new DeleteMacroStep(macro.Id, 1).Apply(document, Contexts.Fresh()).Value;

        change.Next.Library.TryGetShortcut(macro.Id, out var edited).ShouldBeTrue();
        ((MacroAction)edited.Action)
            .Steps.Cast<WaitStep>()
            .Select(w => w.Duration.TotalMilliseconds)
            .ShouldBe([100.0, 300.0]);
        new DeleteMacroStep(macro.Id, 3)
            .Apply(document, Contexts.Fresh())
            .Failure.Code.ShouldBe("command.macro.step_not_found");
        Fail(new DeleteMacroStep(Bold, 0)).Code.ShouldBe("command.macro.not_macro");
    }

    [Fact]
    [Trait("Req", "COP-002")]
    [Trait("Req", "DAT-006")]
    public void Replacing_on_import_backs_up_first_and_corrects_the_last_profile()
    {
        var withLast = Document with { Settings = Document.Settings with { LastProfile = Word } };
        var imported = CommandFactory.MinimalLibrary();

        var change = new ReplaceOnImport(imported).Apply(withLast, Contexts.Fresh()).Value;

        change.Next.Library.ShouldBeSameAs(imported);
        change.Next.Settings.LastProfile.ShouldBe(ProfileId.General);
        change.Backup.ShouldBe(new BackupRequirement.BeforeApply(BackupKind.PreImportReplace));
        change.Events.ShouldBe([new LibraryReplaced()]);
    }

    [Fact]
    [Trait("Req", "COP-002")]
    [Trait("Req", "DAT-006")]
    public void Merging_an_import_takes_the_planned_library_with_undo_and_a_backup_before()
    {
        var merged = Document.Library;

        var change = new MergeOnImport(merged).Apply(Document, Contexts.Fresh()).Value;
        var removing = new MergeOnImport(CommandFactory.MinimalLibrary()).Apply(
            Document,
            Contexts.Fresh()
        );

        removing.IsFailure.ShouldBeTrue("a merge never removes what the user has");

        change.Next.Library.ShouldBeSameAs(merged);
        change.Next.Settings.ShouldBe(Document.Settings);
        change.Undo.ShouldBeOfType<UndoIntent.Record>().Label.ShouldBe(Clicalo.Domain.Messages.L.ImpMerged.Key);
        change.Backup.ShouldBe(new BackupRequirement.BeforeApply(BackupKind.PreImportReplace));
        change.Events.ShouldBe([new LibraryReplaced()]);
    }

    [Fact]
    [Trait("Req", "COP-004")]
    [Trait("Req", "REG-08")]
    public void Restoring_a_backup_replaces_everything_but_the_revision_and_refuses_a_broken_one()
    {
        var current = Document with { Revision = 40 };
        var backup = UserDocument.Create(
            CommandFactory.MinimalLibrary(),
            SettingsSchema.Defaults with
            {
                Theme = ThemeChoice.Light,
            }
        ) with
        {
            Revision = 3,
        };

        var change = new RestoreBackup(backup).Apply(current, Contexts.Fresh()).Value;

        change.Next.ShouldBe(backup with { Revision = 40 });
        change.Backup.ShouldBe(new BackupRequirement.BeforeApply(BackupKind.PreRestore));
        change.Undo.ShouldBe(new UndoIntent.Record(L.RestoredT.Key, null));
        new RestoreBackup(backup with { Settings = backup.Settings with { Columns = 12 } })
            .Apply(current, Contexts.Fresh())
            .Failure.Code.ShouldBe("command.backup.invalid");
    }

    [Fact]
    [Trait("Req", "BIE-009")]
    public void Finishing_the_welcome_is_a_single_step_outside_the_history()
    {
        var change = Apply(new FinishOnboarding());

        change.Next.Onboarding.Completed.ShouldBeTrue();
        change.Undo.ShouldBe(new UndoIntent.Transparent());
        new FinishOnboarding()
            .Apply(change.Next, Contexts.Fresh())
            .Value.Next.ShouldBeSameAs(change.Next);
    }

    private static DocumentChange Apply(IDocumentCommand command)
    {
        var result = command.Apply(Document, Contexts.Fresh());
        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Failure.Code : string.Empty);
        return result.Value;
    }

    private static Failure Fail(IDocumentCommand command)
    {
        var result = command.Apply(Document, Contexts.Fresh());
        result.IsFailure.ShouldBeTrue(command.GetType().Name);
        return result.Failure;
    }

    private static Shortcut TemplateNamed(string name) => Tap("_", name, KeyIds.Ctrl, KeyIds.V);
}
