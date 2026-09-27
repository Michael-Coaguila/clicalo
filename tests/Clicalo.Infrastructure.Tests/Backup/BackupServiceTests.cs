using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Frequents;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Tests.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Backup;

/// <summary>Backups: complete documents with the usage of the moment, retained per kind (blueprint §6.8).</summary>
[Trait("Req", "COP-003")]
[Trait("Req", "REG-08")]
public sealed class BackupServiceTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly BackupService _service;

    public BackupServiceTests() =>
        _service = new BackupService(
            _folder.Locations,
            new AtomicFile(_time, NullLogger<AtomicFile>.Instance),
            _time,
            NullLogger<BackupService>.Instance
        );

    public void Dispose()
    {
        _service.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Disposing_twice_is_harmless_and_a_later_snapshot_is_ignored()
    {
        _service.Dispose();

        Should.NotThrow(_service.Dispose);
        _service.SnapshotNow(TestDocuments.Document(), BackupKind.PreRestore);
        await _service.FlushSnapshotsAsync(Token);
        Directory.Exists(_folder.Locations.Backups).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "COP-004")]
    public async Task A_backup_is_a_complete_document_with_its_own_counts()
    {
        var info = (
            await _service.CreateAsync(TestDocuments.Document(), BackupKind.Manual, Token)
        ).Value;

        info.Kind.ShouldBe(BackupKind.Manual);
        info.Profiles.ShouldBe(2);
        info.Shortcuts.ShouldBe(5);
        info.Id.Value.ShouldBe("manual/clicalo.20260105T090000Z.000001.json");
        var file = Path.Combine(
            _folder.Locations.Backups,
            "manual",
            "clicalo.20260105T090000Z.000001.json"
        );
        EnvelopeCodec
            .Read(File.ReadAllBytes(file), DocumentFormats.Document, DocumentFormats.DocumentSchema)
            .ShouldBeOfType<EnvelopeReadResult.Readable>()
            .HashMatches.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    public async Task A_backup_carries_the_usage_of_that_moment()
    {
        var document = TestDocuments.Document() with
        {
            Frequents = new FrequentsState([], [], 3, UsageRepositoryTests.Usage(("copy", 2))),
        };

        var info = (
            await _service.CreateAsync(document, BackupKind.PreResetFrequents, Token)
        ).Value;

        var payload = JsonNode.Parse(File.ReadAllText(PathOf(info)))!["payload"]!;
        payload["usage"]!["copy"]!.AsArray().Count.ShouldBe(2);
        payload["frequents"]!["usageEpoch"]!.GetValue<long>().ShouldBe(3);
    }

    [Fact]
    [Trait("Req", "LOG-003")]
    public async Task Texts_are_encrypted_in_backups_too()
    {
        var info = (
            await _service.CreateAsync(TestDocuments.Document(), BackupKind.Auto, Token)
        ).Value;

        var text = File.ReadAllText(PathOf(info));
        text.ShouldNotContain("Saludos");
        text.ShouldContain("dpapi.v1");
    }

    [Fact]
    [Trait("Req", "EC-COP-01")]
    public async Task Rotation_keeps_12_automatic_10_of_each_kind_before_an_operation_and_every_manual_one()
    {
        for (var i = 0; i < 15; i++)
        {
            await _service.CreateAsync(TestDocuments.Document(i), BackupKind.Auto, Token);
            await _service.CreateAsync(TestDocuments.Document(i), BackupKind.PreUpdate, Token);
            _time.Advance(TimeSpan.FromSeconds(31));
        }

        for (var i = 0; i < 3; i++)
        {
            await _service.CreateAsync(TestDocuments.Document(i), BackupKind.Manual, Token);
        }

        var list = await _service.ListAsync(Token);

        list.Count(b => b.Kind == BackupKind.Auto).ShouldBe(12);
        list.Count(b => b.Kind == BackupKind.PreUpdate).ShouldBe(10);
        list.Count(b => b.Kind == BackupKind.Manual).ShouldBe(3);
        list.Where(b => b.Kind == BackupKind.Auto)
            .Min(b => b.CreatedAt)
            .ShouldBe(TestTime.Epoch.AddSeconds(3 * 31));
    }

    [Fact]
    public async Task The_list_is_newest_first_and_skips_unreadable_files()
    {
        await _service.CreateAsync(TestDocuments.Document(1), BackupKind.Auto, Token);
        await _service.CreateAsync(TestDocuments.Document(2), BackupKind.Manual, Token);
        var broken = Path.Combine(
            _folder.Locations.Backups,
            "auto",
            "clicalo.20260105T090000Z.000099.json"
        );
        File.WriteAllText(broken, "{ broken");

        var list = await _service.ListAsync(Token);

        list.Select(b => b.Kind).ShouldBe([BackupKind.Manual, BackupKind.Auto]);
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public async Task A_snapshot_is_only_queued_and_written_in_the_background()
    {
        _service.SnapshotNow(TestDocuments.Document(), BackupKind.PreRestore);
        _service.SnapshotNow(TestDocuments.Document(2), BackupKind.PreImportReplace);
        await _service.FlushSnapshotsAsync(Token);

        (await _service.ListAsync(Token))
            .Select(b => b.Kind)
            .ShouldBe([BackupKind.PreImportReplace, BackupKind.PreRestore]);
    }

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task The_v1_original_is_kept_byte_for_byte_and_never_overwritten()
    {
        byte[] original = [0xEF, 0xBB, 0xBF, (byte)'{', (byte)'}'];

        var first = (await _service.KeepV1OriginalAsync(original, Token)).Value;
        var second = (await _service.KeepV1OriginalAsync(original.AsMemory(3), Token)).Value;

        first.Id.Value.ShouldBe("v1-original-20260105T090000Z.json");
        second.Id.Value.ShouldBe("v1-original-20260105T090000Z-1.json");
        File.ReadAllBytes(Path.Combine(_folder.Locations.Backups, first.Id.Value))
            .ShouldBe(original);
        (await _service.ListAsync(Token)).ShouldBeEmpty();
    }

    [Theory]
    [Trait("Req", "LOG-006")]
    [InlineData("../clicalo.json")]
    [InlineData("auto/clicalo.20260105T090000Z.000777.json")]
    public async Task Reading_an_unknown_backup_fails_without_touching_other_files(string id) =>
        (await _service.ReadAsync(new BackupId(id), Token)).Failure.Code.ShouldBe(
            "backup.not_found"
        );

    [Fact]
    [Trait("Req", "COP-005")]
    public async Task A_backup_of_a_newer_major_is_not_applied()
    {
        var info = (
            await _service.CreateAsync(TestDocuments.Document(), BackupKind.Manual, Token)
        ).Value;
        var path = PathOf(info);
        File.WriteAllText(
            path,
            File.ReadAllText(path)
                .Replace("\"major\": 1", "\"major\": 2", StringComparison.Ordinal),
            new UTF8Encoding(false)
        );

        (await _service.ReadAsync(info.Id, Token)).Failure.Code.ShouldBe("import.schema_newer");
    }

    [Fact]
    public async Task An_unreadable_backup_is_a_failure_not_an_exception()
    {
        var info = (
            await _service.CreateAsync(TestDocuments.Document(), BackupKind.Manual, Token)
        ).Value;
        File.WriteAllText(PathOf(info), "{ broken");

        (await _service.ReadAsync(info.Id, Token)).Failure.Code.ShouldBe("backup.unreadable");
    }

    [Fact]
    [Trait("Req", "COP-004")]
    public async Task A_backup_reads_back_as_the_same_document_usage_included()
    {
        var document = TestDocuments.Document() with
        {
            Frequents = new FrequentsState([], [], 3, UsageRepositoryTests.Usage(("copy", 2))),
        };
        var info = (await _service.CreateAsync(document, BackupKind.Manual, Token)).Value;

        var read = (await _service.ReadAsync(info.Id, Token)).Value;

        read.Library.ShouldBe(document.Library);
        read.Settings.ShouldBe(document.Settings);
        read.Frequents.Usage.Entries.Count.ShouldBe(1);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string PathOf(BackupInfo info) =>
        Path.Combine(
            _folder.Locations.Backups,
            info.Id.Value.Replace('/', Path.DirectorySeparatorChar)
        );
}
