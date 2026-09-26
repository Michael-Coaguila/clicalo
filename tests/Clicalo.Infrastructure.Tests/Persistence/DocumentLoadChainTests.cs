using System.Text;
using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// The file part of the load chain (blueprint §6.5): quarantine, <c>.prev</c>, <c>.tmp</c>, the emergency copy and the
/// read-only cases, over bytes (the Domain decode is covered by <c>DocumentRepositoryTests</c>).
/// </summary>
[Trait("Req", "DAT-003")]
[Trait("Req", "REG-08")]
public sealed class DocumentLoadChainTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private DataLocations Data => _folder.Locations;

    [Fact]
    public async Task A_valid_file_is_loaded_as_it_is()
    {
        Put(Data.Document, Marker.Bytes(5, seq: 5));
        Put(Data.DocumentPrevious, Marker.Bytes(4, seq: 4));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Main);
        result.Decoded.ShouldBe(new Marker(5));
        result.MainUsable.ShouldBeTrue();
        result.HashMatches.ShouldBeTrue();
        result.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unreadable_file_is_quarantined_and_prev_is_used()
    {
        Put(Data.Document, Encoding.UTF8.GetBytes("{\"format\":\"clicalo.docu"));
        Put(Data.DocumentPrevious, Marker.Bytes(4, seq: 4));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Previous);
        result.Decoded.ShouldBe(new Marker(4));
        result.Quarantined.Length.ShouldBe(1);
        File.Exists(Data.Document).ShouldBeFalse();
        File.ReadAllText(result.Quarantined[0]).ShouldBe("{\"format\":\"clicalo.docu");
    }

    [Fact]
    public async Task A_readable_but_invalid_file_is_quarantined_too()
    {
        Put(Data.Document, Marker.Invalid(seq: 9));
        Put(Data.DocumentPrevious, Marker.Bytes(8, seq: 8));

        var result = await Marker.LoadAsync(Data);

        result.Decoded.ShouldBe(new Marker(8));
        result.Quarantined.Length.ShouldBe(1);
        result.HighestSeq.ShouldBe(9);
    }

    [Fact]
    public async Task When_replace_stopped_half_way_the_newest_of_tmp_and_prev_is_used()
    {
        Put(Data.DocumentPrevious, Marker.Bytes(4, seq: 4));
        Put(Data.Document + ".tmp", Marker.Bytes(5, seq: 5));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Temporary);
        result.Decoded.ShouldBe(new Marker(5));
        result.MainMissing.ShouldBeTrue();
        result.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_complete_newer_tmp_left_by_a_crash_before_replacing_wins_over_the_file()
    {
        Put(Data.Document, Marker.Bytes(4, seq: 4));
        Put(Data.Document + ".tmp", Marker.Bytes(5, seq: 5));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Temporary);
        result.Decoded.ShouldBe(new Marker(5));
        result.MainUsable.ShouldBeTrue();
    }

    [Fact]
    public async Task A_half_written_tmp_is_ignored_and_never_quarantined()
    {
        var bytes = Marker.Bytes(5, seq: 5);
        Put(Data.Document, Marker.Bytes(4, seq: 4));
        Put(Data.Document + ".tmp", bytes[..(bytes.Length / 2)]);

        var result = await Marker.LoadAsync(Data);

        result.Decoded.ShouldBe(new Marker(4));
        result.Quarantined.ShouldBeEmpty();
        File.Exists(Data.Document + ".tmp").ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "DAT-002")]
    public async Task A_newer_emergency_copy_is_used()
    {
        Put(Data.Document, Marker.Bytes(4, seq: 4));
        Put(Data.PendingDocument, Marker.Bytes(5, seq: 5));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Pending);
        result.Decoded.ShouldBe(new Marker(5));
    }

    [Fact]
    public async Task An_older_emergency_copy_is_ignored()
    {
        Put(Data.Document, Marker.Bytes(6, seq: 6));
        Put(Data.PendingDocument, Marker.Bytes(5, seq: 5));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.Main);
        result.Decoded.ShouldBe(new Marker(6));
    }

    [Fact]
    public async Task A_future_major_stops_the_chain_and_touches_nothing()
    {
        var future = Encoding
            .UTF8.GetString(Marker.Bytes(1, seq: 1))
            .Replace("\"major\": 1", "\"major\": 2", StringComparison.Ordinal);
        Put(Data.Document, Encoding.UTF8.GetBytes(future));

        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.FutureMajor);
        result.Future.ShouldBe(new SchemaVersion(2, 0));
        File.ReadAllText(Data.Document).ShouldBe(future);
    }

    [Fact]
    public async Task A_file_locked_through_every_retry_is_reported_and_never_quarantined()
    {
        Put(Data.Document, Marker.Bytes(4, seq: 4));
        Put(Data.DocumentPrevious, Marker.Bytes(3, seq: 3));
        var locked = new LockedReads(Data.Document);
        var time = Clicalo.TestKit.Time.TestTime.CreateProvider();

        var result = await FakeClock.RunAsync(time, Marker.LoadAsync(Data, locked, time));

        result.MainLocked.ShouldBeTrue();
        result.Decoded.ShouldBe(new Marker(3));
        result.Quarantined.ShouldBeEmpty();
        File.Exists(Data.Document).ShouldBeTrue();
        locked.Attempts.ShouldBe(6);
    }

    [Fact]
    public async Task Nothing_on_disk_is_a_first_run()
    {
        var result = await Marker.LoadAsync(Data);

        result.Source.ShouldBe(LoadSource.None);
        result.SomethingExisted.ShouldBeFalse();
        result.MainMissing.ShouldBeTrue();
    }

    private static void Put(string path, byte[] bytes)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes);
    }

    /// <summary>The disk, with one file that another process keeps locked.</summary>
    private sealed class LockedReads(string locked) : IAtomicFileSystem
    {
        private readonly IAtomicFileSystem _disk = AtomicFile.Disk;

        public int Attempts { get; private set; }

        public bool Exists(string path) => _disk.Exists(path);

        public byte[]? ReadAllBytesOrNull(string path)
        {
            if (string.Equals(path, locked, StringComparison.OrdinalIgnoreCase))
            {
                Attempts++;
                throw new IOException("locked", unchecked((int)0x80070020));
            }

            return _disk.ReadAllBytesOrNull(path);
        }

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
