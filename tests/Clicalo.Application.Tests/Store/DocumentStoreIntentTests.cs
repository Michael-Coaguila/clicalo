using Clicalo.Domain.Commands;
using Clicalo.Domain.Document;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Settings;
using static Clicalo.Application.Tests.Store.StoreSamples;

namespace Clicalo.Application.Tests.Store;

/// <summary>How the store honours each undo intent and backup requirement of a change (blueprint §6.4).</summary>
public sealed class DocumentStoreIntentTests
{
    [Fact]
    [Trait("Req", "DAT-006")]
    public void A_barrier_empties_the_history()
    {
        var harness = new StoreHarness(Document());
        harness.Dispatch(new PinToFrequents(Bold));
        harness.Dispatch(new PinToFrequents(Save));

        harness.Dispatch(
            new ScriptedCommand(document => new DocumentChange(
                document with
                {
                    Onboarding = new OnboardingState(true),
                },
                [],
                new UndoIntent.Barrier(),
                new BackupRequirement.None()
            ))
        );

        harness.Store.CanUndo.ShouldBeFalse();
        harness.Store.Current.Onboarding.Completed.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public void A_backup_is_taken_of_the_document_before_the_change()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        harness.Dispatch(
            new ScriptedCommand(document => new DocumentChange(
                document with
                {
                    Settings = document.Settings with { AutoSuggestProfiles = false },
                },
                [],
                new UndoIntent.Record(L.Saved.Key, null),
                new BackupRequirement.BeforeApply(BackupKind.Manual)
            ))
        );

        harness.Backups.Snapshots.ShouldHaveSingleItem().ShouldBe((initial, BackupKind.Manual));
        harness.Store.Current.Settings.AutoSuggestProfiles.ShouldBeFalse();
    }

    [Fact]
    public void A_command_that_throws_is_a_defect_and_changes_nothing()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        Should.Throw<InvalidOperationException>(() =>
            harness.Dispatch(
                new ScriptedCommand(_ => throw new InvalidOperationException("defect"))
            )
        );

        harness.Store.Current.ShouldBeSameAs(initial);
        harness.Changes.ShouldBeEmpty();
        harness
            .Dispatch(new SetSetting(SettingPaths.Theme, ThemeChoice.Dark))
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "REG-07")]
    public void A_transparent_change_keeps_the_open_step_open()
    {
        var initial = Document();
        var harness = new StoreHarness(initial);

        harness.Dispatch(new EditShortcut(initial.Named(Bold, "A")));
        harness.Dispatch(new RecordUsage(Bold));
        harness.Dispatch(new EditShortcut(harness.Store.Current.Named(Bold, "B")));

        harness.UndoAll().ShouldBe(1);
        harness.Store.Current.Library.ShouldBe(initial.Library);
        harness.Store.Current.Frequents.Usage.Entries.ShouldContainKey(Bold);
    }
}
