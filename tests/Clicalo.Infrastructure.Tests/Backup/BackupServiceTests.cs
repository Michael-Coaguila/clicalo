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
    private readonly BoundedTestToken _bounded = new();
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
        _bounded.Dispose();
        _folder.Dispose();
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
    public async Task A_snapshot_is_only_queued_until_the_persistence_consumer_writes_it_in_order()
    {
        _service.SnapshotNow(TestDocuments.Document(), BackupKind.PreRestore);
        _service.SnapshotNow(TestDocuments.Document(2), BackupKind.PreImportReplace);

        Directory.Exists(_folder.Locations.Backups).ShouldBeFalse("SnapshotNow never does I/O");
        (await _service.WriteSnapshotsAsync(Token)).Value.ShouldBe(2);
        (await _service.WriteSnapshotsAsync(Token)).Value.ShouldBe(0);

        (await _service.ListAsync(Token))
            .Select(b => b.Kind)
            .ShouldBe([BackupKind.PreImportReplace, BackupKind.PreRestore]);
    }

    [Fact]
    [Trait("Req", "DAT-006")]
    public async Task A_snapshot_that_cannot_be_written_stays_queued_and_is_written_by_the_next_call()
    {
        var files = new FailingWrites();
        var service = new BackupService(
            _folder.Locations,
            new AtomicFile(_time, NullLogger<AtomicFile>.Instance, files),
            _time,
            NullLogger<BackupService>.Instance,
            new DocumentCodec(),
            files
        );
        service.SnapshotNow(TestDocuments.Document(), BackupKind.PreRestore);
        service.SnapshotNow(TestDocuments.Document(2), BackupKind.PreImportReplace);
        files.Full = true;

        var failed = await service.WriteSnapshotsAsync(Token);

        failed.Failure.Code.ShouldBe("persist.io.full");
        Directory.Exists(_folder.Locations.Backups).ShouldBeFalse();
        files.Full = false;
        (await service.WriteSnapshotsAsync(Token)).Value.ShouldBe(2);
        (await service.ListAsync(Token))
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

    [Fact]
    [Trait("Req", "MIG-004")]
    public async Task The_same_v1_original_is_kept_once_and_a_zip_keeps_its_extension()
    {
        byte[] original = [(byte)'{', (byte)'}'];
        byte[] zip = [(byte)'P', (byte)'K', 3, 4, 0, 0];

        var first = (await _service.KeepV1OriginalAsync(original, Token)).Value;
        var again = (await _service.KeepV1OriginalAsync(original, Token)).Value;
        var archive = (await _service.KeepV1OriginalAsync(zip, Token)).Value;

        again.Id.ShouldBe(first.Id);
        archive.Id.Value.ShouldBe("v1-original-20260105T090000Z.zip");
        Directory.GetFiles(_folder.Locations.Backups, "v1-original-*").Length.ShouldBe(2);
        File.ReadAllBytes(Path.Combine(_folder.Locations.Backups, archive.Id.Value)).ShouldBe(zip);
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

    private CancellationToken Token => _bounded.Token;

    private string PathOf(BackupInfo info) =>
        Path.Combine(
            _folder.Locations.Backups,
            info.Id.Value.Replace('/', Path.DirectorySeparatorChar)
        );

    /// <summary>The disk, with every write failing like a full disk while <see cref="Full"/> is set.</summary>
    private sealed class FailingWrites : IAtomicFileSystem
    {
        private readonly IAtomicFileSystem _disk = AtomicFile.Disk;

        public bool Full { get; set; }

        public bool Exists(string path) => _disk.Exists(path);

        public byte[]? ReadAllBytesOrNull(string path) => _disk.ReadAllBytesOrNull(path);

        public void CreateDirectory(string path)
        {
            if (!Full)
            {
                _disk.CreateDirectory(path);
            }
        }

        public void WriteThrough(string path, ReadOnlySpan<byte> content)
        {
            if (Full)
            {
                throw new IOException("full", unchecked((int)0x80070070));
            }

            _disk.WriteThrough(path, content);
        }

        public void Replace(string target, string replacement, string backup) =>
            _disk.Replace(target, replacement, backup);

        public void Move(string source, string target) => _disk.Move(source, target);

        public void Delete(string path) => _disk.Delete(path);

        public IReadOnlyList<string> Files(string directory, string pattern) =>
            _disk.Files(directory, pattern);
    }
}
