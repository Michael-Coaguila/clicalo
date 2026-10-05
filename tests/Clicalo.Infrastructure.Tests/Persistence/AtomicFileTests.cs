using Clicalo.Domain.Errors;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>The atomic write protocol against a real folder (blueprint §6.5, DAT-002, S11).</summary>
[Trait("Req", "DAT-002")]
public sealed class AtomicFileTests : IDisposable
{
    private readonly BoundedTestToken _bounded = new();
    private readonly TempFolder _folder = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _time =
        TestTime.CreateProvider();

    public void Dispose()
    {
        _bounded.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task The_first_write_creates_the_file_and_leaves_nothing_else()
    {
        var path = _folder.PathOf("data", "clicalo.json");

        var receipt = (await Writer().WriteAsync(path, "one"u8.ToArray(), Token)).Value;

        receipt.ReplacedExisting.ShouldBeFalse();
        receipt.Attempts.ShouldBe(1);
        File.ReadAllText(path).ShouldBe("one");
        _folder.Files().ShouldBe(["data/clicalo.json"]);
    }

    [Fact]
    public async Task A_second_write_replaces_the_file_and_keeps_the_old_one_as_prev()
    {
        var path = _folder.PathOf("clicalo.json");
        await Writer().WriteAsync(path, "one"u8.ToArray(), Token);

        var receipt = (await Writer().WriteAsync(path, "two"u8.ToArray(), Token)).Value;

        receipt.ReplacedExisting.ShouldBeTrue();
        File.ReadAllText(path).ShouldBe("two");
        File.ReadAllText(path + ".prev").ShouldBe("one");
        _folder.Files().ShouldBe(["clicalo.json", "clicalo.json.prev"]);
    }

    [Fact]
    public async Task A_short_lock_by_another_process_is_retried_and_the_write_succeeds()
    {
        var path = _folder.PathOf("clicalo.json");
        await Writer().WriteAsync(path, "old"u8.ToArray(), Token);
        var holder = Lock(path);

        var receipt = await FakeClock.RunAsync(
            _time,
            Writer().WriteAsync(path, "new"u8.ToArray(), Token),
            elapsed =>
            {
                if (elapsed >= TimeSpan.FromMilliseconds(120))
                {
                    holder.Dispose();
                }
            }
        );

        holder.Dispose();
        receipt.IsSuccess.ShouldBeTrue();
        receipt.Value.Attempts.ShouldBeInRange(2, 6);
        File.ReadAllText(path).ShouldBe("new");
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task A_lock_through_every_retry_fails_visibly_after_the_backoff_and_keeps_the_old_file()
    {
        var path = _folder.PathOf("clicalo.json");
        await Writer().WriteAsync(path, "old"u8.ToArray(), Token);
        var started = _time.GetUtcNow();
        using var holder = Lock(path);

        var result = await FakeClock.RunAsync(
            _time,
            Writer().WriteAsync(path, "new"u8.ToArray(), Token)
        );

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("persist.io.locked");
        result.Failure.Recovery.ShouldBe(FailureRecovery.Retry);
        result.Failure.Severity.ShouldBe(FailureSeverity.Critical);
        (_time.GetUtcNow() - started).ShouldBeInRange(
            TimeSpan.FromMilliseconds(1550),
            TimeSpan.FromMilliseconds(1560)
        );
        holder.Dispose();
        File.ReadAllText(path).ShouldBe("old");
    }

    [Fact]
    public async Task A_full_disk_is_not_retried()
    {
        var disk = new ThrowingFileSystem(new IOException("full", unchecked((int)0x80070070)));

        var result = await new AtomicFile(_time, NullLogger<AtomicFile>.Instance, disk).WriteAsync(
            _folder.PathOf("clicalo.json"),
            "x"u8.ToArray(),
            Token
        );

        result.Failure.Code.ShouldBe("persist.io.full");
        disk.Writes.ShouldBe(1);
    }

    [Fact]
    public async Task Cancelling_during_the_retries_stops_them_and_never_leaves_a_half_file()
    {
        var path = _folder.PathOf("clicalo.json");
        await Writer().WriteAsync(path, "old"u8.ToArray(), Token);
        using var holder = Lock(path);
        using var cancel = new CancellationTokenSource();

        var write = Writer().WriteAsync(path, "new"u8.ToArray(), cancel.Token);
        await cancel.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(() => write);
        holder.Dispose();
        File.ReadAllText(path).ShouldBe("old");
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task The_target_is_never_opened_for_writing_only_its_tmp_which_then_takes_its_place()
    {
        var disk = new RecordingFileSystem();
        var writer = new AtomicFile(_time, NullLogger<AtomicFile>.Instance, disk);
        var path = _folder.PathOf("clicalo.json");

        (await writer.WriteAsync(path, "one"u8.ToArray(), Token)).IsSuccess.ShouldBeTrue();
        (await writer.WriteAsync(path, "two"u8.ToArray(), Token)).IsSuccess.ShouldBeTrue();

        // A crash while writing must leave the old version whole: only the temporary file is ever opened (§6.5).
        disk.Steps.ShouldBe([
            "write " + path + ".tmp",
            "move " + path + ".tmp -> " + path,
            "write " + path + ".tmp",
            "replace " + path + " with " + path + ".tmp keeping " + path + ".prev",
        ]);
        File.ReadAllText(path).ShouldBe("two");
    }

    private CancellationToken Token => _bounded.Token;

    private AtomicFile Writer() => new(_time, NullLogger<AtomicFile>.Instance);

    /// <summary>What an indexer or a monitor does: a handle that shares nothing.</summary>
    private static FileStream Lock(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    /// <summary>The disk, recording every step that writes or renames.</summary>
    private sealed class RecordingFileSystem : IAtomicFileSystem
    {
        private readonly IAtomicFileSystem _disk = AtomicFile.Disk;

        public List<string> Steps { get; } = [];

        public bool Exists(string path) => _disk.Exists(path);

        public byte[]? ReadAllBytesOrNull(string path) => _disk.ReadAllBytesOrNull(path);

        public void CreateDirectory(string path) => _disk.CreateDirectory(path);

        public void WriteThrough(string path, ReadOnlySpan<byte> content)
        {
            Steps.Add("write " + path);

            // Fails at once, not after the retries: opening the target itself is never a transient error.
            path.ShouldEndWith(
                ".tmp",
                customMessage: "only the temporary file is opened for writing"
            );
            _disk.WriteThrough(path, content);
        }

        public void Replace(string target, string replacement, string backup)
        {
            Steps.Add("replace " + target + " with " + replacement + " keeping " + backup);
            _disk.Replace(target, replacement, backup);
        }

        public void Move(string source, string target)
        {
            Steps.Add("move " + source + " -> " + target);
            _disk.Move(source, target);
        }

        public void Delete(string path) => _disk.Delete(path);

        public IReadOnlyList<string> Files(string directory, string pattern) =>
            _disk.Files(directory, pattern);
    }

    /// <summary>File operations whose write always fails with one error.</summary>
    private sealed class ThrowingFileSystem(Exception error) : IAtomicFileSystem
    {
        public int Writes { get; private set; }

        public bool Exists(string path) => false;

        public byte[]? ReadAllBytesOrNull(string path) => null;

        public void CreateDirectory(string path) { }

        public void WriteThrough(string path, ReadOnlySpan<byte> content)
        {
            Writes++;
            throw error;
        }

        public void Replace(string target, string replacement, string backup) { }

        public void Move(string source, string target) { }

        public void Delete(string path) { }

        public IReadOnlyList<string> Files(string directory, string pattern) => [];
    }
}
