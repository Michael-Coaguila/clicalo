using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Application.Ports;
using Clicalo.Domain.Document;
using Clicalo.Domain.Privacy;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// <c>clicalo.json</c> end to end (blueprint §6.5): load outcomes, read-only cases, the write protocol and what a rewrite
/// keeps. The tests that read a document back through the Domain skip until the domain package is integrated.
/// </summary>
[Trait("Req", "DAT-001")]
[Trait("Req", "DAT-003")]
[Trait("Req", "REG-08")]
public sealed class DocumentRepositoryTests : IDisposable
{
    private readonly TempFolder _folder = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    public void Dispose() => _folder.Dispose();

    private DataLocations Data => _folder.Locations;

    [Fact]
    public async Task Nothing_on_disk_is_a_first_run_that_may_be_saved()
    {
        var load = await Repository().LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FirstRun);
        load.IsReadOnly.ShouldBeFalse();
        load.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public async Task A_future_major_is_read_only_and_never_written()
    {
        Put(Data.Document, FutureMajor());
        var before = File.ReadAllBytes(Data.Document);
        var repository = Repository();

        var load = await repository.LoadAsync(Token);
        var save = await repository.SaveAsync(TestDocuments.Document(), Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.FutureMajorReadOnly);
        load.IsReadOnly.ShouldBeTrue();
        save.Failure.Code.ShouldBe("persist.readonly");
        File.ReadAllBytes(Data.Document).ShouldBe(before);
        repository.AcceptDefaultDocument();
        repository.IsReadOnly.ShouldBeTrue();
    }

    [Fact]
    public async Task Nothing_usable_is_a_default_in_memory_not_written_until_accepted()
    {
        Put(Data.Document, Encoding.UTF8.GetBytes("{ truncated"));
        var repository = Repository();

        var load = await repository.LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.DefaultInMemory);
        load.IsReadOnly.ShouldBeTrue();
        load.Quarantined.Length.ShouldBe(1);
        (await repository.SaveAsync(TestDocuments.Document(), Token)).Failure.Code.ShouldBe(
            "persist.readonly"
        );
        File.Exists(Data.Document).ShouldBeFalse();
        repository.AcceptDefaultDocument();
        repository.IsReadOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task A_document_that_could_not_be_read_because_it_was_locked_is_never_overwritten()
    {
        Put(Data.Document, Encoding.UTF8.GetBytes("{}"));
        var repository = Repository(files: new AlwaysLocked(Data.Document));

        var load = await FakeClock.RunAsync(_time, repository.LoadAsync(Token));
        repository.AcceptDefaultDocument();

        load.IsReadOnly.ShouldBeTrue();
        load.Quarantined.ShouldBeEmpty();
        repository.IsReadOnly.ShouldBeTrue();
        File.ReadAllText(Data.Document).ShouldBe("{}");
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    public async Task A_saved_document_loads_back_equal_and_valid()
    {
        var document = TestDocuments.Document();
        var repository = Repository();
        _ = await repository.LoadAsync(Token);

        var receipt = (await repository.SaveAsync(document, Token)).Value;
        var load = await Repository().LoadAsync(Token);

        receipt.Seq.ShouldBe(1);
        load.Outcome.ShouldBe(DocumentLoadOutcome.Loaded);
        load.Seq.ShouldBe(1);
        load.Document.ShouldBe(document);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "LOG-003")]
    public async Task Texts_never_reach_the_file_in_clear()
    {
        var repository = Repository();
        _ = await repository.LoadAsync(Token);

        await repository.SaveAsync(TestDocuments.Document(), Token);

        File.ReadAllText(Data.Document).ShouldNotContain("Saludos");
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "ACT-005")]
    public async Task Members_of_a_later_minor_survive_load_and_save()
    {
        await SaveAsync(TestDocuments.Document());
        Edit(payload =>
        {
            payload["futureRoot"] = 1;
            payload["settings"]!["futureSetting"] = "x";
            payload["profiles"]![0]!["futureProfile"] = true;
            payload["profiles"]![0]!["shortcuts"]![0]!["futureShortcut"] = new JsonArray(1, 2);
        });
        var repository = Repository();
        var load = await repository.LoadAsync(Token);

        await repository.SaveAsync(
            load.Document with
            {
                Onboarding = new OnboardingState(false),
            },
            Token
        );

        var text = File.ReadAllText(Data.Document);
        foreach (
            var member in new[] { "futureRoot", "futureSetting", "futureProfile", "futureShortcut" }
        )
        {
            text.ShouldContain(member);
        }
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "COP-005")]
    public async Task A_text_encrypted_elsewhere_is_unavailable_and_its_blob_survives_a_rewrite()
    {
        await SaveAsync(TestDocuments.Document());
        Edit(payload =>
            payload["profiles"]![0]!["shortcuts"]![1]!["action"]!["text"]!["blob"] =
                Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8])
        );
        var repository = Repository();
        var load = await repository.LoadAsync(Token);
        var text = load
            .Document.Library.Profiles[0]
            .Shortcuts[1]
            .Action.ShouldBeOfType<Domain.Library.TextAction>();

        text.Text.ShouldBe(SecretText.Unavailable);
        await repository.SaveAsync(load.Document, Token);
        File.ReadAllText(Data.Document)
            .ShouldContain(Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]));
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    public async Task A_valid_document_edited_by_hand_is_accepted_and_reported()
    {
        await SaveAsync(TestDocuments.Document());
        Edit(payload => payload["onboarding"]!["completed"] = false, breakHash: true);

        var load = await Repository().LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.EditedExternally);
        load.Document.Onboarding.Completed.ShouldBeFalse();
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    public async Task An_unreadable_document_is_quarantined_and_prev_is_used()
    {
        await SaveAsync(TestDocuments.Document(1));
        await SaveAsync(TestDocuments.Document(2));
        Put(Data.Document, Encoding.UTF8.GetBytes("{\"format\":"));

        var load = await Repository().LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.RecoveredFromPrevious);
        load.Document.ShouldBe(TestDocuments.Document(1));
        load.Quarantined.Length.ShouldBe(1);
        load.IsReadOnly.ShouldBeFalse();
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    public async Task With_prev_gone_too_the_newest_valid_backup_is_used()
    {
        var writer = new AtomicFile(_time, NullLogger<AtomicFile>.Instance);
        var backups = new BackupService(Data, writer, _time, NullLogger<BackupService>.Instance);
        await backups.CreateAsync(TestDocuments.Document(1), BackupKind.Auto, Token);
        await backups.CreateAsync(TestDocuments.Document(2), BackupKind.Auto, Token);
        Put(Data.Document, Encoding.UTF8.GetBytes("garbage"));

        var load = await Repository().LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.RecoveredFromBackup);
        load.Document.Library.ShouldBe(TestDocuments.Document(2).Library);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "DAT-004")]
    public async Task A_repairable_document_is_repaired_and_its_original_is_kept_as_pre_repair()
    {
        await SaveAsync(TestDocuments.Document());
        Edit(payload => payload["profiles"]![1]!["shortcuts"]![0]!["id"] = "copy");

        var load = await Repository().LoadAsync(Token);

        load.Outcome.ShouldBe(DocumentLoadOutcome.Repaired);
        Directory.GetFiles(Path.Combine(Data.Backups, "pre-repair")).Length.ShouldBe(1);
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    [Trait("Req", "DAT-002")]
    public async Task A_persistent_lock_keeps_an_emergency_copy_that_the_next_save_removes()
    {
        await SaveAsync(TestDocuments.Document(1));
        var repository = Repository();
        _ = await repository.LoadAsync(Token);
        var holder = new FileStream(Data.Document, FileMode.Open, FileAccess.Read, FileShare.None);

        var failed = await FakeClock.RunAsync(
            _time,
            repository.SaveAsync(TestDocuments.Document(2), Token)
        );

        failed.Failure.Code.ShouldBe("persist.io.locked");
        File.Exists(Data.PendingDocument).ShouldBeTrue();
        holder.Dispose();
        (await repository.SaveAsync(TestDocuments.Document(3), Token)).IsSuccess.ShouldBeTrue();
        File.Exists(Data.PendingDocument).ShouldBeFalse();
    }

    [Fact(SkipExceptions = [typeof(NotImplementedException)])]
    public async Task After_a_crash_with_an_unsaved_emergency_copy_the_copy_is_loaded()
    {
        await SaveAsync(TestDocuments.Document(1));
        var repository = Repository();
        _ = await repository.LoadAsync(Token);
        using (new FileStream(Data.Document, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            _ = await FakeClock.RunAsync(
                _time,
                repository.SaveAsync(TestDocuments.Document(2), Token)
            );
        }

        var load = await Repository().LoadAsync(Token);

        load.Document.ShouldBe(TestDocuments.Document(2));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static byte[] FutureMajor() =>
        Encoding.UTF8.GetBytes(
            Encoding
                .UTF8.GetString(Marker.Bytes(1, seq: 1))
                .Replace("\"major\": 1", "\"major\": 2", StringComparison.Ordinal)
        );

    private static void Put(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    private async Task SaveAsync(UserDocument document)
    {
        var repository = Repository();
        _ = await repository.LoadAsync(Token);
        (await repository.SaveAsync(document, Token)).IsSuccess.ShouldBeTrue();
    }

    /// <summary>Edits the payload of <c>clicalo.json</c> by hand, fixing its hash unless told to break it.</summary>
    private void Edit(Action<JsonObject> edit, bool breakHash = false)
    {
        var root = JsonNode.Parse(File.ReadAllText(Data.Document))!.AsObject();
        var payload = root["payload"]!.AsObject();
        edit(payload);
        var envelope = new DocumentEnvelope(
            DocumentFormats.Document,
            DocumentFormats.DocumentSchema,
            "2.0.0",
            root["seq"]!.GetValue<long>(),
            TestTime.Epoch,
            string.Empty,
            (JsonObject)payload.DeepClone()
        );
        var bytes = EnvelopeCodec.Write(envelope);
        if (breakHash)
        {
            var text = Encoding
                .UTF8.GetString(bytes)
                .Replace(
                    "\"payloadSha256\": \"",
                    "\"payloadSha256\": \"0",
                    StringComparison.Ordinal
                );
            bytes = Encoding.UTF8.GetBytes(text);
        }

        File.WriteAllBytes(Data.Document, bytes);
    }

    private DocumentRepository Repository(IAtomicFileSystem? files = null)
    {
        var writer = new AtomicFile(_time, NullLogger<AtomicFile>.Instance);
        return new DocumentRepository(
            Data,
            writer,
            new QuarantineStore(Data, _time),
            new BackupService(Data, writer, _time, NullLogger<BackupService>.Instance),
            _time,
            NullLogger<DocumentRepository>.Instance,
            new DocumentCodec(),
            files ?? AtomicFile.Disk,
            () => TestDocuments.Document(0)
        );
    }

    /// <summary>The disk, with the document held by another process.</summary>
    private sealed class AlwaysLocked(string locked) : IAtomicFileSystem
    {
        private readonly IAtomicFileSystem _disk = AtomicFile.Disk;

        public bool Exists(string path) => _disk.Exists(path);

        public byte[]? ReadAllBytesOrNull(string path) =>
            string.Equals(path, locked, StringComparison.OrdinalIgnoreCase)
                ? throw new IOException("locked", unchecked((int)0x80070021))
                : _disk.ReadAllBytesOrNull(path);

        public void CreateDirectory(string path) => _disk.CreateDirectory(path);

        public void WriteThrough(string path, ReadOnlySpan<byte> content) =>
            _disk.WriteThrough(path, content);

        public void Replace(string target, string replacement, string backup) =>
            _disk.Replace(target, replacement, backup);

        public void Move(string source, string target) => _disk.Move(source, target);

        public void Delete(string path) => _disk.Delete(path);

        public IReadOnlyList<string> Files(string directory, string pattern) =>
            _disk.Files(directory, pattern);
    }
}
