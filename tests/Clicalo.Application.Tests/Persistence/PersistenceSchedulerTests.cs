using Clicalo.Application.Persistence;
using Clicalo.Domain.Document;

namespace Clicalo.Application.Tests.Persistence;

/// <summary>Autosave by slice (blueprint §6.4, §6.8, REG-07, DAT-002, FRE-002, COP-003).</summary>
[Trait("Req", "REG-07")]
[Trait("Req", "DAT-002")]
public sealed class PersistenceSchedulerTests
{
    private static readonly TimeSpan Ms = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task Ten_changes_in_one_second_write_the_document_once_with_the_last_version()
    {
        await using var rig = new SchedulerRig();

        for (var i = 0; i < 10; i++)
        {
            await rig.ChangeAsync(DocumentSlices.Library);
            await rig.AdvanceAsync(100 * Ms);
        }

        await rig.AdvanceAsync(TimeSpan.FromSeconds(3));

        rig.Documents.Saves.Count.ShouldBe(1);
        rig.Documents.Saves[0].Document.ShouldBe(rig.Current);
        (rig.Documents.Saves[0].At - rig.Start).ShouldBe(1400 * Ms);
    }

    [Fact]
    public async Task Changes_that_never_pause_are_still_written_within_two_seconds()
    {
        await using var rig = new SchedulerRig();

        for (var i = 0; i < 10; i++)
        {
            await rig.ChangeAsync(DocumentSlices.Settings);
            await rig.AdvanceAsync(400 * Ms);
        }

        rig.Documents.SaveTimes.Length.ShouldBeGreaterThanOrEqualTo(2);
        (rig.Documents.SaveTimes[0] - rig.Start).ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    [Trait("Req", "FRE-002")]
    public async Task A_thousand_executions_never_write_the_document_nor_take_a_backup()
    {
        await using var rig = new SchedulerRig();

        for (var i = 1; i <= 1000; i++)
        {
            await rig.ChangeAsync(DocumentSlices.FrequentsUsage, UsageVersion(rig, marks: i));
            await rig.AdvanceAsync(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
        }

        await rig.AdvanceAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));

        rig.Documents.Attempts.ShouldBe(0);
        rig.Backups.Created.ShouldBeEmpty();
        rig.Usage.Saves.Count.ShouldBeInRange(1, (1060 / 30) + 1);
        rig.Usage.Saves[^1].Usage.Entries.Values.Single().Count.ShouldBe(1000);
    }

    [Fact]
    [Trait("Req", "FRE-002")]
    [Trait("Req", "COP-003")]
    [Trait("Req", "NFR-020")]
    public async Task Eight_hours_of_heavy_use_leave_the_document_and_the_backups_alone_until_a_real_edit()
    {
        await using var rig = new SchedulerRig();
        var every = TimeSpan.FromHours(8) / 10_000;

        for (var i = 1; i <= 10_000; i++)
        {
            await rig.ChangeAsync(
                DocumentSlices.FrequentsUsage,
                UsageVersion(rig, marks: 1 + (i % 50))
            );
            await rig.AdvanceAsync(every, every);
        }

        rig.Documents.Attempts.ShouldBe(0);
        rig.Backups.Created.ShouldBeEmpty();
        rig.Usage.Saves.Count.ShouldBeLessThanOrEqualTo(
            (int)(TimeSpan.FromHours(8) / TimeSpan.FromSeconds(30))
        );
        var gaps = rig.Usage.SaveTimes().Zip(rig.Usage.SaveTimes().Skip(1), (a, b) => b - a);
        gaps.ShouldAllBe(gap => gap >= TimeSpan.FromSeconds(30));

        var edited = rig.Elapsed;
        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(1));

        rig.Documents.Saves.Count.ShouldBe(1);
        rig.Backups.Created.Count.ShouldBe(1);
        rig.Backups.Created[0].Kind.ShouldBe(BackupKind.Auto);
        (rig.Backups.Created[0].At - rig.Start - edited).ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    [Trait("Req", "COP-003")]
    public async Task The_automatic_backup_comes_30_s_after_the_last_significant_change_and_only_once()
    {
        await using var rig = new SchedulerRig();

        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        await rig.ChangeAsync(DocumentSlices.Onboarding);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
        await rig.ChangeAsync(DocumentSlices.Duplicates);
        await rig.AdvanceAsync(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));

        rig.Backups.Created.Count.ShouldBe(1);
        (rig.Backups.Created[0].At - rig.Start).ShouldBe(TimeSpan.FromSeconds(50));
        rig.Backups.Created[0].Document.ShouldBe(rig.Current);
    }

    [Fact]
    [Trait("Req", "COP-003")]
    public async Task No_automatic_backup_when_the_user_turned_it_off()
    {
        await using var rig = new SchedulerRig();

        await rig.ChangeAsync(
            DocumentSlices.Settings,
            PersistenceDocuments.Document(1, autoBackup: false)
        );
        await rig.AdvanceAsync(TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1));

        rig.Backups.Created.ShouldBeEmpty();
        rig.Documents.Saves.Count.ShouldBe(1);
    }

    [Fact]
    public async Task A_change_of_the_revision_only_writes_nothing()
    {
        await using var rig = new SchedulerRig();

        await rig.ChangeAsync(DocumentSlices.None);
        await rig.AdvanceAsync(TimeSpan.FromMinutes(6), TimeSpan.FromSeconds(1));

        rig.Documents.Attempts.ShouldBe(0);
        rig.Usage.Saves.ShouldBeEmpty();
        rig.Statuses.ShouldBeEmpty();
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    public async Task A_new_usage_epoch_rewrites_usage_even_without_the_usage_slice()
    {
        await using var rig = new SchedulerRig();

        await rig.ChangeAsync(
            DocumentSlices.FrequentsCuration,
            PersistenceDocuments.Document(1, epoch: 1)
        );
        await rig.AdvanceAsync(TimeSpan.FromSeconds(31), TimeSpan.FromSeconds(1));

        rig.Usage.Saves.Single().Epoch.ShouldBe(1);
        rig.Documents.Saves.Count.ShouldBe(1);
    }

    [Fact]
    public async Task The_status_goes_pending_then_saved()
    {
        await using var rig = new SchedulerRig();

        await rig.ChangeAsync(DocumentSlices.Library);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Pending);
        await rig.AdvanceAsync(600 * Ms);

        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        rig.Statuses.ShouldBe([SaveStatus.Pending, SaveStatus.Saved]);
    }

    [Theory]
    [Trait("Req", "NFR-006")]
    [InlineData(500)]
    [InlineData(2000)]
    public async Task S11_a_lock_shorter_than_3_s_never_shows_an_error(int lockMs)
    {
        await using var rig = new SchedulerRig();
        rig.Documents.LockedUntil = rig.Start + ((500 + lockMs) * Ms);

        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(10));

        rig.Documents.Saves.Count.ShouldBe(1);
        rig.Statuses.ShouldNotContain(SaveStatus.Failing);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    [Trait("Req", "NFR-006")]
    public async Task S11_a_lock_longer_than_3_s_is_visible_3_s_after_the_first_attempt_and_retried_every_30_s()
    {
        await using var rig = new SchedulerRig();
        rig.Documents.LockedUntil = rig.Start + TimeSpan.FromSeconds(40);

        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceToAsync(3490 * Ms);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Retrying);
        await rig.AdvanceToAsync(3500 * Ms);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Failing);

        await rig.AdvanceToAsync(TimeSpan.FromSeconds(33));
        var attemptsBeforeRetry = rig.Documents.Attempts;
        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceToAsync(TimeSpan.FromSeconds(33.5));
        rig.Documents.Attempts.ShouldBe(
            attemptsBeforeRetry,
            "while failing, changes wait for the retry"
        );
        await rig.AdvanceToAsync(TimeSpan.FromSeconds(80));

        rig.Documents.Saves.Count.ShouldBe(1);
        rig.Documents.Saves[0].Document.ShouldBe(rig.Current);
        (rig.Documents.Saves[0].At - rig.Start).ShouldBeGreaterThanOrEqualTo(
            TimeSpan.FromSeconds(40)
        );
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        rig.Statuses.ShouldBe([
            SaveStatus.Pending,
            SaveStatus.Retrying,
            SaveStatus.Failing,
            SaveStatus.Saved,
        ]);
    }

    [Fact]
    [Trait("Req", "NFR-006")]
    public async Task A_full_disk_never_spins_it_is_visible_after_3_s_and_retried_every_30_s()
    {
        await using var rig = new SchedulerRig();
        rig.Documents.Refuse = FakeDocumentRepository.DiskFull;

        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceToAsync(2900 * Ms);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Retrying);
        await rig.AdvanceToAsync(3600 * Ms);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Failing);
        await rig.AdvanceToAsync(TimeSpan.FromSeconds(62), TimeSpan.FromSeconds(1));

        rig.Documents.Attempts.ShouldBe(3, "the first one, then at 30 s and 60 s");
        rig.Documents.Refuse = null;
        await rig.AdvanceToAsync(TimeSpan.FromSeconds(91), TimeSpan.FromSeconds(1));
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        rig.Documents.Saves.Single().Document.ShouldBe(rig.Current);
    }

    [Fact]
    public async Task A_refused_save_is_visible_at_once_and_retried_with_the_next_change()
    {
        await using var rig = new SchedulerRig();
        rig.Documents.Refuse = FakeDocumentRepository.ReadOnly;

        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(1));

        rig.Scheduler.Status.ShouldBe(SaveStatus.Failing);
        rig.Documents.Attempts.ShouldBe(1);
        rig.Documents.Refuse = null;
        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.AdvanceAsync(TimeSpan.FromSeconds(1));
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
        rig.Documents.Saves.Single().Document.ShouldBe(rig.Current);
    }

    [Fact]
    public async Task Flushing_writes_both_files_now()
    {
        await using var rig = new SchedulerRig();
        await rig.ChangeAsync(DocumentSlices.Library);
        await rig.ChangeAsync(DocumentSlices.FrequentsUsage, UsageVersion(rig, marks: 3));

        await rig.Scheduler.FlushAsync(TestContext.Current.CancellationToken);

        rig.Documents.Saves.Single().At.ShouldBe(rig.Start);
        rig.Usage.Saves.Single().Usage.Entries.Values.Single().Count.ShouldBe(3);
        rig.Scheduler.Status.ShouldBe(SaveStatus.Saved);
    }

    [Fact]
    public async Task Stopping_the_loop_flushes_what_is_pending()
    {
        var rig = new SchedulerRig();
        await rig.ChangeAsync(DocumentSlices.Library);

        await rig.DisposeAsync();

        rig.Documents.Saves.Single().Document.ShouldBe(rig.Current);
    }

    private static Domain.Document.UserDocument UsageVersion(SchedulerRig rig, int marks) =>
        rig.Current with
        {
            Frequents = rig.Current.Frequents with { Usage = PersistenceDocuments.Usage(marks) },
        };
}
