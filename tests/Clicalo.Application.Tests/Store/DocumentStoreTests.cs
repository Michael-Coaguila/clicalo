using Clicalo.Application.Store;
using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Keys;
using Clicalo.Domain.Library;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;
using Clicalo.Domain.Settings;
using Clicalo.Domain.Tests.Generators;
using Clicalo.Domain.Timing;
using static Clicalo.Application.Tests.Store.StoreSamples;

namespace Clicalo.Application.Tests.Store;

/// <summary>
/// The single writer of the document (blueprint §6.4): revisions, events, undo by slices (DAT-006, REG-07), grouping
/// of the edits of one shortcut (EDI-021), drafts without a trace (ATJ-011) and destructive commands only with two
/// taps (REG-04).
/// </summary>
public sealed class DocumentStoreTests
{
    [Fact]
    [Trait("Req", "REG-07")]
    public void A_change_raises_the_revision_and_is_published_with_its_slices_and_events()
    {
        var harness = new StoreHarness(Document());

        var result = harness.Dispatch(
            new CreateShortcut(
                new ListRef.AlwaysVisible(),
                Tap("_", "Pegar", KeyIds.Ctrl, KeyIds.V),
                ListPosition.End
            )
        );

        result.Value.ShouldBeSameAs(harness.Store.Current);
        harness.Store.Current.Revision.ShouldBe(1);
        var change = harness.Changes.ShouldHaveSingleItem();
        change.Origin.ShouldBe(ChangeOrigin.Command);
        change.Slices.ShouldBe(DocumentSlices.Library);
        change.Events.ShouldHaveSingleItem().ShouldBeOfType<ShortcutCreated>();
        change.After.ShouldBeSameAs(harness.Store.Current);
        harness.Store.CanUndo.ShouldBeTrue();
        harness.Store.UndoLabel.ShouldBe(L.NewCreated.Key);
    }

    [Fact]
    public void A_change_that_touches_nothing_is_not_published_and_a_failure_changes_nothing()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        harness.Dispatch(new SetSetting(SettingPaths.Columns, 3)).Value.ShouldBeSameAs(initial);
        harness
            .Dispatch(new EditShortcut(initial.Named(Bold, "Negrita")))
            .Value.ShouldBeSameAs(initial);
        harness
            .Dispatch(new SetSetting(SettingPaths.Columns, 9))
            .Failure.Code.ShouldBe("command.setting.out_of_range");

        harness.Store.Current.ShouldBeSameAs(initial);
        harness.Changes.ShouldBeEmpty();
        harness.Store.CanUndo.ShouldBeFalse();
        harness.Store.UndoLabel.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "REG-04")]
    public void A_destructive_command_needs_the_token_of_its_own_operation()
    {
        var harness = new StoreHarness(Document());
        IDocumentCommand disguised = new DeleteShortcut(Bold);

        harness.Store.Dispatch(disguised).Failure.Code.ShouldBe("store.destructive.unconfirmed");
        var otherToken = harness.TokenFor(new DeleteProfile(Word));
        harness
            .Store.Dispatch(new DeleteShortcut(Bold), otherToken)
            .Failure.Code.ShouldBe("store.confirmation.mismatch");
        harness.Changes.ShouldBeEmpty();

        harness
            .Store.Dispatch(new DeleteShortcut(Bold), harness.TokenFor(new DeleteShortcut(Bold)))
            .IsSuccess.ShouldBeTrue();
        harness.Store.Current.Library.TryLocate(Bold, out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "FRE-005")]
    [Trait("Req", "REG-04")]
    public void Undoing_a_deletion_restores_the_shortcut_among_its_pins_and_keeps_later_usage()
    {
        var harness = new StoreHarness(Document());
        harness.Dispatch(new PinToFrequents(Save));
        harness.Dispatch(new PinToFrequents(Bold));
        harness.Dispatch(new DeleteShortcut(Bold));
        harness.Dispatch(new RecordUsage(Save));

        harness.Store.UndoLabel.ShouldBe(L.Deleted.Key);
        harness.Store.Undo().IsSuccess.ShouldBeTrue();

        var current = harness.Store.Current;
        current.Library.TryLocate(Bold, out var location).ShouldBeTrue();
        location.ShouldBe(new ShortcutLocation(new ListRef.InProfile(Word), 0));
        current.Frequents.Pins.ShouldBe([Save, Bold]);
        current.Frequents.Usage.Entries[Save].Count.ShouldBe(1);
        harness.Changes[^1].Origin.ShouldBe(ChangeOrigin.Undo);
        harness.Changes[^1].Slices.ShouldBe(DocumentSlices.Library);
        current.Revision.ShouldBe(5);
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    [Trait("Req", "DAT-006")]
    public void Undoing_a_reset_of_Frequents_restores_usage_pins_and_hidden_and_the_backup_came_first()
    {
        var harness = new StoreHarness(Document());
        harness.Dispatch(new PinToFrequents(Save));
        harness.Dispatch(new HideFromFrequents(Copy));
        harness.Dispatch(new RecordUsage(Bold));
        var before = harness.Store.Current;

        harness.Dispatch(new ResetFrequents()).IsSuccess.ShouldBeTrue();
        harness.Store.Current.Frequents.UsageEpoch.ShouldBe(1);
        harness
            .Backups.Snapshots.ShouldHaveSingleItem()
            .ShouldBe((before, BackupKind.PreResetFrequents));

        harness.Store.Undo().IsSuccess.ShouldBeTrue();
        harness.Store.Current.Frequents.ShouldBe(before.Frequents);
    }

    [Fact]
    [Trait("Req", "EDI-021")]
    public void The_edits_of_one_shortcut_form_one_step_until_the_editor_moves_on()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        harness.Dispatch(new EditShortcut(initial.Named(Bold, "N")));
        harness.Dispatch(new EditShortcut(initial.Named(Bold, "Ne")));
        harness.Dispatch(new MoveShortcut(Bold, new ListRef.InProfile(Word), ListPosition.End));
        harness.Store.SealCoalescing();
        harness.Dispatch(new EditShortcut(harness.Store.Current.Named(Bold, "Negrita!")));

        harness.UndoAll().ShouldBe(2);
        harness.Store.Current.Library.ShouldBe(initial.Library);
    }

    [Fact]
    [Trait("Req", "EDI-021")]
    public void Edits_of_another_shortcut_start_a_new_step()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        harness.Dispatch(new EditShortcut(initial.Named(Bold, "A")));
        harness.Dispatch(new EditShortcut(initial.Named(Save, "B")));
        harness.Dispatch(new EditShortcut(harness.Store.Current.Named(Bold, "C")));

        harness.UndoAll().ShouldBe(3);
    }

    [Fact]
    [Trait("Req", "ATJ-011")]
    public void A_draft_created_emptied_and_discarded_leaves_no_trace()
    {
        var harness = new StoreHarness(Document());
        harness.Dispatch(new PinToFrequents(Save));

        var created = harness
            .Dispatch(
                new CreateShortcut(
                    new ListRef.InProfile(Word),
                    Tap("_", "P", KeyIds.P),
                    ListPosition.End
                )
            )
            .Value;
        var id = harness.Changes[^1].Events.OfType<ShortcutCreated>().Single().Id;
        var blank = created.Named(id, string.Empty) with
        {
            Action = new TapAction(KeyChord.Empty, []),
        };
        harness.Dispatch(new EditShortcut(blank)).IsSuccess.ShouldBeTrue();
        harness.Dispatch(new DiscardDraft(id)).IsSuccess.ShouldBeTrue();

        harness.Store.UndoLabel.ShouldBe(L.CtxPinT.Key);
        harness.UndoAll().ShouldBe(1);
        harness.Store.Current.Library.TryLocate(id, out _).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "PER-008")]
    public void Undoing_a_new_profile_chosen_as_last_profile_leaves_General_as_last_profile()
    {
        var harness = new StoreHarness(Document());
        harness.Dispatch(
            new CreateProfile(Document().Library.General with { Shortcuts = [] }, ListPosition.End)
        );
        var created = harness.Changes[^1].Events.OfType<ProfileCreated>().Single().Id;
        harness
            .Dispatch(new SetSetting(SettingPaths.LastProfile, created))
            .IsSuccess.ShouldBeTrue();

        harness.Store.Undo().IsSuccess.ShouldBeTrue();

        harness.Store.Current.Library.TryGetProfile(created, out _).ShouldBeFalse();
        harness.Store.Current.Settings.LastProfile.ShouldBe(ProfileId.General);
        harness.Store.Current.Validate().ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void The_history_keeps_the_last_twenty_steps()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        for (var i = 0; i < 25; i++)
        {
            harness.Dispatch(new SetSetting(SettingPaths.ReleaseOnAppSwitch, i % 2 == 0));
            harness.Store.SealCoalescing();
        }

        harness.UndoAll().ShouldBe(Timings.Persistence.UndoDepth);
        harness.Store.Undo().Failure.Code.ShouldBe("store.undo.empty");
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void Presentation_is_not_undone_and_undo_never_rewinds_it()
    {
        var harness = new StoreHarness(Document());

        harness.Dispatch(new SetSetting(SettingPaths.AutoSuggestProfiles, false));
        harness.Dispatch(new SetSetting(SettingPaths.Theme, ThemeChoice.Light));
        harness.Dispatch(new SetPanelPosition(new MonitorPosition(@"\\.\DISPLAY1", 5, 5)));

        harness.UndoAll().ShouldBe(1);
        var settings = harness.Store.Current.Settings;
        settings.AutoSuggestProfiles.ShouldBeTrue();
        settings.Theme.ShouldBe(ThemeChoice.Light);
        settings.PanelPositions.ShouldHaveSingleItem();
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void Several_changes_of_one_behaviour_setting_are_one_step()
    {
        var harness = new StoreHarness(Document());

        harness.Dispatch(new SetSetting(SettingPaths.TouchHitSlop, 20));
        harness.Dispatch(new SetSetting(SettingPaths.TouchHitSlop, 22));
        harness.Dispatch(new SetSetting(SettingPaths.TouchHitSlop, 24));

        harness.UndoAll().ShouldBe(1);
        harness.Store.Current.Settings.Touch.HitSlopPx.ShouldBe(
            SettingsSchema.Defaults.Touch.HitSlopPx
        );
    }

    [Fact]
    public void Usage_counted_from_many_threads_is_never_lost()
    {
        var harness = new StoreHarness(Document());

        Parallel.For(
            0,
            200,
            _ => harness.Store.Dispatch(new RecordUsage(Copy)).IsSuccess.ShouldBeTrue()
        );

        harness.Store.Current.Revision.ShouldBe(200);
        harness.Store.Current.Frequents.Usage.Entries[Copy].Count.ShouldBe(200);
        harness.Store.CanUndo.ShouldBeFalse();
    }

    [Fact]
    public void A_listener_may_read_and_dispatch_while_it_is_notified()
    {
        var harness = new StoreHarness(Document());
        var nested = false;
        harness.Store.Changed += (_, change) =>
        {
            change.After.ShouldBeSameAs(harness.Store.Current);
            if (!nested)
            {
                nested = true;
                harness.Store.Dispatch(new RecordUsage(Copy)).IsSuccess.ShouldBeTrue();
            }
        };

        harness.Dispatch(new PinToFrequents(Copy)).IsSuccess.ShouldBeTrue();

        harness.Store.Current.Revision.ShouldBe(2);
    }

    [Fact]
    public void The_commands_see_the_clock_and_the_languages_of_the_document()
    {
        var harness = new StoreHarness(Document());
        harness.Time.Advance(TimeSpan.FromMinutes(5));

        harness.Dispatch(new RecordUsage(Copy));

        harness.Store.Current.Frequents.Usage.Entries[Copy].ShouldBe([DomainGen.Now.AddMinutes(5)]);
    }
}
