using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>An unreadable document is moved aside, never deleted or overwritten (DAT-003, REG-08).</summary>
[Trait("Req", "DAT-003")]
[Trait("Req", "REG-08")]
public sealed class QuarantineStoreTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    [Fact]
    public void Moves_the_file_with_its_date_and_keeps_every_byte()
    {
        var store = new QuarantineStore(_folder.Locations, TestTime.CreateProvider());
        Directory.CreateDirectory(_folder.Locations.Root);
        File.WriteAllBytes(_folder.Locations.Document, [1, 2, 3]);

        var moved = store.Move(_folder.Locations.Document).Value;

        File.Exists(_folder.Locations.Document).ShouldBeFalse();
        Path.GetFileName(moved).ShouldBe("clicalo.20260105T090000Z.json.corrupt");
        File.ReadAllBytes(moved).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public void Never_overwrites_an_earlier_quarantined_file()
    {
        var store = new QuarantineStore(_folder.Locations, TestTime.CreateProvider());
        Directory.CreateDirectory(_folder.Locations.Root);
        var moved = new List<string>();
        for (byte i = 0; i < 3; i++)
        {
            File.WriteAllBytes(_folder.Locations.Document, [i]);
            moved.Add(store.Move(_folder.Locations.Document).Value);
        }

        moved
            .Select(Path.GetFileName)
            .ShouldBe([
                "clicalo.20260105T090000Z.json.corrupt",
                "clicalo.20260105T090000Z-1.json.corrupt",
                "clicalo.20260105T090000Z-2.json.corrupt",
            ]);
        moved.Select(File.ReadAllBytes).Select(b => b[0]).ShouldBe([(byte)0, (byte)1, (byte)2]);
    }

    [Fact]
    public void A_file_that_cannot_be_moved_stays_where_it_was()
    {
        var store = new QuarantineStore(_folder.Locations, TestTime.CreateProvider());
        Directory.CreateDirectory(_folder.Locations.Root);
        File.WriteAllBytes(_folder.Locations.Document, [7]);
        using var holder = new FileStream(
            _folder.Locations.Document,
            FileMode.Open,
            FileAccess.Read,
            FileShare.None
        );

        var result = store.Move(_folder.Locations.Document);

        result.IsFailure.ShouldBeTrue();
        result.Failure.Code.ShouldBe("persist.quarantine.failed");
        holder.Dispose();
        File.ReadAllBytes(_folder.Locations.Document).ShouldBe([7]);
    }
}
