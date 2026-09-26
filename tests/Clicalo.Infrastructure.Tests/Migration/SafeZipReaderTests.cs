using System.Globalization;
using System.IO.Compression;
using System.Text;
using Clicalo.Infrastructure.Migration;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>The limits and path rules of the untrusted zip reader (blueprint §6.6, MIG-009, LOG-006).</summary>
[Trait("Req", "MIG-009")]
public sealed class SafeZipReaderTests
{
    private static readonly SafeZipReader Reader = new(SafeZipLimits.Default);

    [Fact]
    public void An_honest_zip_gives_its_json_entries_exactly_in_order()
    {
        var zip = Zips.Create(
            ("profiles.json", "{\"profiles\":{}}"),
            ("backups/profiles.backup.es.json", "{}")
        );

        var entries = Read(zip).Value;

        entries
            .Select(static e => e.Name)
            .ShouldBe(["profiles.json", "backups/profiles.backup.es.json"]);
        Encoding.UTF8.GetString(entries[0].Content.Span).ShouldBe("{\"profiles\":{}}");
    }

    [Fact]
    public void Stored_entries_are_read_too()
    {
        var zip = Zips.Create(
            [("profiles.json", Encoding.UTF8.GetBytes("{}"))],
            CompressionLevel.NoCompression
        );

        Read(zip).Value.ShouldHaveSingleItem().Content.Length.ShouldBe(2);
    }

    [Fact]
    public void Nested_zips_other_files_and_folders_are_never_opened()
    {
        var inner = Zips.Create(("profiles.json", "{}"));
        var zip = Zips.Create([
            ("inner.zip", inner),
            ("readme.txt", Encoding.UTF8.GetBytes("hello")),
            ("folder/", []),
            ("profiles.json", Encoding.UTF8.GetBytes("{}")),
        ]);

        Read(zip).Value.Select(static e => e.Name).ShouldBe(["profiles.json"]);
    }

    [Theory]
    [InlineData("../profiles.json")]
    [InlineData("backups/../../profiles.json")]
    [InlineData("backups\\..\\..\\profiles.json")]
    [InlineData("/profiles.json")]
    [InlineData("\\profiles.json")]
    [InlineData("C:/profiles.json")]
    [InlineData("C:\\profiles.json")]
    [InlineData("profiles.json:hidden")]
    [InlineData("readme\u0001.txt")]
    public void One_unsafe_name_refuses_the_whole_archive(string name)
    {
        var zip = Zips.Create(("profiles.json", "{}"), (name, "x"));

        Code(Read(zip)).ShouldBe(V1ImportFailures.ZipUnsafePathCode);
    }

    [Fact]
    public void A_compression_bomb_is_stopped_while_it_expands()
    {
        var zip = Zips.Create([("profiles.json", Zips.Zeros(20 * 1024 * 1024))]);
        zip.Length.ShouldBeLessThan(100_000);

        Code(Read(zip)).ShouldBe(V1ImportFailures.ZipRatioCode);
    }

    [Fact]
    public void A_json_entry_over_its_size_limit_is_refused()
    {
        var reader = new SafeZipReader(new SafeZipLimits(1_000_000, 10, 100, 50_000));
        var zip = Zips.Create([("profiles.json", Zips.Noise(60_000, seed: 1))]);

        Code(reader.ReadJsonEntries(new MemoryStream(zip), TestContext.Current.CancellationToken))
            .ShouldBe(V1ImportFailures.ZipEntryTooLargeCode);
    }

    [Fact]
    public void The_uncompressed_total_is_limited_across_entries()
    {
        var reader = new SafeZipReader(new SafeZipLimits(100_000, 10, 100, 80_000));
        var zip = Zips.Create([
            ("a.json", Zips.Noise(60_000, seed: 2)),
            ("b.json", Zips.Noise(60_000, seed: 3)),
        ]);
        zip.Length.ShouldBeLessThan(100_000);

        Code(reader.ReadJsonEntries(new MemoryStream(zip), TestContext.Current.CancellationToken))
            .ShouldBe(V1ImportFailures.ZipTooLargeCode);
    }

    [Fact]
    public void An_archive_larger_than_the_total_limit_is_refused_before_it_is_parsed()
    {
        var reader = new SafeZipReader(new SafeZipLimits(1_000, 10, 100, 1_000));
        var zip = Zips.Create([("a.json", Zips.Noise(5_000, seed: 4))]);

        Code(reader.ReadJsonEntries(new MemoryStream(zip), TestContext.Current.CancellationToken))
            .ShouldBe(V1ImportFailures.ZipTooLargeCode);
    }

    [Fact]
    public void More_entries_than_allowed_are_refused()
    {
        var zip = Zips.Create(
            Enumerable
                .Range(0, 1001)
                .Select(static i =>
                    (string.Create(CultureInfo.InvariantCulture, $"e{i}.txt"), Array.Empty<byte>())
                )
        );

        Code(Read(zip)).ShouldBe(V1ImportFailures.ZipTooManyEntriesCode);
    }

    [Theory]
    [InlineData((ushort)1001)]
    [InlineData(ushort.MaxValue)]
    public void A_declared_entry_count_over_the_limit_is_refused_before_the_directory_is_loaded(
        ushort declared
    )
    {
        // The directory still holds one entry: only the check before loading it can tell «too many entries» from
        // damage (0xFFFF is also the ZIP64 marker, which a v1 backup never needs).
        var zip = Zips.DeclareEntryCount(Zips.Create(("profiles.json", "{}")), declared);

        Code(Read(zip)).ShouldBe(V1ImportFailures.ZipTooManyEntriesCode);
    }

    [Fact]
    public void Headers_that_understate_the_size_do_not_let_a_bomb_through()
    {
        var zip = Zips.LieAboutUncompressedSize(
            Zips.Create([("profiles.json", Zips.Zeros(20 * 1024 * 1024))]),
            10
        );

        // The decompressor stops at the declared 10 bytes; the checksum shows the file is not what the header says.
        Code(Read(zip)).ShouldBe(V1ImportFailures.ZipDamagedCode);
    }

    [Fact]
    public void The_checksum_is_the_standard_crc_32() =>
        Crc32.Append(0, "123456789"u8).ShouldBe(0xCBF43926u);

    [Fact]
    public void Headers_that_overstate_the_compressed_size_do_not_hide_the_ratio()
    {
        var honest = Zips.Create([("profiles.json", Zips.Zeros(20 * 1024 * 1024))]);
        var zip = Zips.LieAboutCompressedSize(honest, (uint)honest.Length);

        Read(zip).IsFailure.ShouldBeTrue();
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 1, 2, 3, 4, 5 })]
    public void Something_that_is_not_a_zip_is_damaged(byte[] bytes) =>
        Code(Read(bytes)).ShouldBe(V1ImportFailures.ZipDamagedCode);

    [Fact]
    public void A_truncated_zip_is_damaged()
    {
        var zip = Zips.Create(("profiles.json", "{\"profiles\":{}}"));

        Code(Read(zip[..(zip.Length / 2)])).ShouldBe(V1ImportFailures.ZipDamagedCode);
    }

    [Fact]
    public void A_cancelled_read_stops()
    {
        var zip = Zips.Create(("profiles.json", "{}"));

        Should.Throw<OperationCanceledException>(() =>
            Reader.ReadJsonEntries(new MemoryStream(zip), new CancellationToken(canceled: true))
        );
    }

    [Theory]
    [InlineData("profiles.json", true)]
    [InlineData("a/b/c.json", true)]
    [InlineData("./profiles.json", true)]
    [InlineData("..profiles.json", true)]
    [InlineData("", false)]
    [InlineData("..", false)]
    [InlineData("a/../b", false)]
    public void Names_are_checked_segment_by_segment(string name, bool safe) =>
        SafeZipReader.IsSafeName(name).ShouldBe(safe);

    private static Domain.Errors.Result<System.Collections.Immutable.ImmutableArray<SafeZipEntry>> Read(
        byte[] zip
    ) => Reader.ReadJsonEntries(new MemoryStream(zip), TestContext.Current.CancellationToken);

    private static string Code<T>(Domain.Errors.Result<T> result) => result.Failure.Code;
}
