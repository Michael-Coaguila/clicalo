using System.Text;
using Clicalo.Infrastructure.Migration;
using CsCheck;

namespace Clicalo.Infrastructure.Tests.Migration;

/// <summary>
/// Generated hostile zips (blueprint §6.6): ratio bombs, many entries, <c>..</c>, absolute and drive paths, and
/// headers that lie. Whatever the archive, the reader never throws, never returns an unsafe name and never returns
/// more than its limits.
/// </summary>
[Trait("Req", "MIG-009")]
public sealed class SafeZipReaderPropertyTests
{
    private static readonly SafeZipLimits Limits = new(64 * 1024, 8, 100, 16 * 1024);

    private static readonly Gen<string> SafeName = Gen.OneOfConst(
        "profiles.json",
        "profiles.backup.es.json",
        "a/b.json",
        "notes.txt",
        "inner.zip",
        "folder/"
    );

    private static readonly Gen<string> UnsafeName = Gen.OneOfConst(
        "../x.json",
        "a/../../x.json",
        "..\\x.json",
        "/x.json",
        "\\x.json",
        "C:/x.json",
        "D:\\x.json",
        "x.json:stream"
    );

    private static readonly Gen<GeneratedEntry> Entry = Gen.Select(
        Gen.Frequency((6, SafeName), (1, UnsafeName)),
        Gen.Int[0, 40_000],
        Gen.Bool,
        Gen.Int,
        static (name, size, zeros, seed) => new GeneratedEntry(name, size, zeros, seed)
    );

    private static readonly Gen<GeneratedZip> Archive = Gen.Select(
        Entry.Array[0, 12],
        Gen.Frequency(
            (4, Gen.Const(Lie.None)),
            (1, Gen.Const(Lie.Uncompressed)),
            (1, Gen.Const(Lie.Compressed))
        ),
        Gen.UInt,
        static (entries, lie, declared) => new GeneratedZip(entries, lie, declared)
    );

    [Fact]
    public void A_generated_zip_never_breaks_a_rule() =>
        Archive.Sample(
            generated =>
            {
                var bytes = generated.Build();
                var result = new SafeZipReader(Limits).ReadJsonEntries(
                    new MemoryStream(bytes),
                    CancellationToken.None
                );

                if (generated.Entries.Any(static e => !SafeZipReader.IsSafeName(e.Name)))
                {
                    result.IsFailure.ShouldBeTrue("an unsafe name must refuse the archive");
                }

                if (generated.Entries.Length > Limits.MaxEntries)
                {
                    result.IsFailure.ShouldBeTrue("too many entries");
                }

                if (!result.TryGetValue(out var entries))
                {
                    return;
                }

                entries.Length.ShouldBeLessThanOrEqualTo(Limits.MaxEntries);
                entries
                    .Sum(static e => (long)e.Content.Length)
                    .ShouldBeLessThanOrEqualTo(Limits.MaxTotalBytes);
                foreach (var entry in entries)
                {
                    SafeZipReader.IsSafeName(entry.Name).ShouldBeTrue();
                    entry.Name.ShouldEndWith(".json", Case.Insensitive);
                    entry.Content.Length.ShouldBeLessThanOrEqualTo((int)Limits.MaxJsonBytes);
                    entry.Content.Length.ShouldBeLessThanOrEqualTo(
                        (int)(Limits.MaxCompressionRatio * bytes.Length)
                    );
                }

                if (generated.Lie == Lie.None)
                {
                    // An honest archive that passes gives back exactly its JSON entries.
                    var expected = generated
                        .Entries.Where(static e =>
                            e.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                        )
                        .ToList();
                    entries
                        .Select(static e => e.Name)
                        .ShouldBe(expected.Select(static e => e.Name));
                    for (var i = 0; i < expected.Count; i++)
                    {
                        entries[i].Content.ToArray().ShouldBe(expected[i].Content());
                    }
                }
            },
            iter: 300,
            threads: 1
        );

    [Fact]
    public void Generated_bombs_are_always_refused() =>
        Gen.Int[2 * 1024 * 1024, 8 * 1024 * 1024]
            .Sample(
                size =>
                {
                    var zip = Zips.Create([("profiles.json", Zips.Zeros(size))]);

                    new SafeZipReader(SafeZipLimits.Default)
                        .ReadJsonEntries(new MemoryStream(zip), CancellationToken.None)
                        .IsFailure.ShouldBeTrue();
                },
                iter: 10,
                threads: 1
            );

    internal enum Lie
    {
        None,
        Uncompressed,
        Compressed,
    }

    internal sealed record GeneratedEntry(string Name, int Size, bool Zeros, int Seed)
    {
        public byte[] Content() =>
            Name.EndsWith('/') ? []
            : Zeros ? Zips.Zeros(Size)
            : Zips.Noise(Size, Seed);
    }

    internal sealed record GeneratedZip(GeneratedEntry[] Entries, Lie Lie, uint Declared)
    {
        public byte[] Build()
        {
            var zip = Zips.Create(Entries.Select(static e => (e.Name, e.Content())));
            return Lie switch
            {
                Lie.Uncompressed => Zips.LieAboutUncompressedSize(zip, Declared % 1024),
                Lie.Compressed => Zips.LieAboutCompressedSize(zip, Declared),
                _ => zip,
            };
        }

        public override string ToString() =>
            new StringBuilder()
                .AppendJoin(
                    ", ",
                    Entries.Select(static e => $"{e.Name}:{e.Size}{(e.Zeros ? "z" : "n")}")
                )
                .Append(" lie=")
                .Append(Lie)
                .ToString();
    }
}
