using System.Collections.Immutable;
using System.Globalization;
using Clicalo.Application.Ports;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Backup;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// S11 Â· hostile persistence, crash half: the process dies at every point of the write protocol (the
/// <see cref="CrashingFileSystem"/> enumerates them) during a sequence of saves; after the Â«restartÂ» the document is the
/// last saved version or the one being saved, never lost, never reset to factory data and never quarantined, and saving
/// goes on normally (docs/testing/spikes/S11.md).
/// </summary>
[Trait("Req", "DAT-002")]
[Trait("Req", "REG-08")]
[Trait("Req", "NFR-006")]
public sealed class S11CrashTests
{
    private const int Saves = 4;

    private static readonly FakeTimeProvider Time = TestTime.CreateProvider();

    /// <summary>Every crash point of <see cref="Saves"/> saves of the document or of the usage.</summary>
    public static TheoryData<int> CrashPoints() => [.. Enumerable.Range(1, CountPoints())];

    [Theory]
    [MemberData(nameof(CrashPoints))]
    public async Task A_crash_at_any_point_of_a_save_never_loses_the_document(int crashAt)
    {
        using var folder = new TempFolder();
        var path = folder.Locations.Document;
        var saved = await SaveUntilCrashAsync(
            new AtomicFile(Time, NullLogger<AtomicFile>.Instance, new CrashingFileSystem(crashAt)),
            path,
            n => Marker.Bytes(n, seq: n)
        );

        var restart = await Marker.LoadAsync(folder.Locations);

        restart.Quarantined.ShouldBeEmpty("a crash never leaves an unreadable document behind");
        if (saved == 0)
        {
            (restart.Decoded?.N ?? 0).ShouldBeInRange(0, 1);
        }
        else
        {
            restart.Decoded.ShouldNotBeNull(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"crash at point {crashAt} after {saved} saves lost the document"
                )
            );
            restart.Decoded.N.ShouldBeInRange(saved, saved + 1);
        }

        var healthy = new AtomicFile(Time, NullLogger<AtomicFile>.Instance);
        for (var n = saved + 1; n <= Saves; n++)
        {
            (
                await healthy.WriteAsync(path, Marker.Bytes(n, seq: n), Token)
            ).IsSuccess.ShouldBeTrue();
        }

        (await Marker.LoadAsync(folder.Locations)).Decoded.ShouldBe(new Marker(Saves));
    }

    [Theory]
    [MemberData(nameof(CrashPoints))]
    [Trait("Req", "FRE-002")]
    public async Task A_crash_at_any_point_of_a_usage_save_keeps_the_usage(int crashAt)
    {
        using var folder = new TempFolder();
        var crashing = new UsageRepository(
            folder.Locations,
            new AtomicFile(Time, NullLogger<AtomicFile>.Instance, new CrashingFileSystem(crashAt)),
            Time,
            NullLogger<UsageRepository>.Instance
        );
        var saved = 0;
        try
        {
            for (var n = 1; n <= Saves; n++)
            {
                (await crashing.SaveAsync(3, Usage(n), Token)).IsSuccess.ShouldBeTrue();
                saved = n;
            }
        }
        catch (CrashingFileSystem.SimulatedCrash)
        {
            // The process died here.
        }

        var restart = await UsageRepository(folder).LoadAsync(3, Token);

        var marks = restart.Entries.GetValueOrDefault(new ShortcutId("copy")).Count;
        marks.ShouldBeInRange(saved == 0 ? 0 : saved, saved + 1);
        folder
            .Files()
            .ShouldNotContain(f => f.StartsWith("roaming/quarantine", StringComparison.Ordinal));
    }

    [Theory(SkipExceptions = [typeof(NotImplementedException)])]
    [MemberData(nameof(CrashPoints))]
    [Trait("Req", "DAT-003")]
    public async Task A_crash_at_any_point_of_a_document_save_never_resets_it_to_factory_data(
        int crashAt
    )
    {
        using var folder = new TempFolder();
        var crashing = Repository(
            folder,
            new AtomicFile(Time, NullLogger<AtomicFile>.Instance, new CrashingFileSystem(crashAt))
        );
        _ = await crashing.LoadAsync(Token);
        var saved = 0;
        try
        {
            for (var n = 1; n <= Saves; n++)
            {
                (
                    await crashing.SaveAsync(TestDocuments.Document(n), Token)
                ).IsSuccess.ShouldBeTrue();
                saved = n;
            }
        }
        catch (CrashingFileSystem.SimulatedCrash)
        {
            // The process died here.
        }

        var restart = await Repository(
                folder,
                new AtomicFile(Time, NullLogger<AtomicFile>.Instance)
            )
            .LoadAsync(Token);

        if (saved > 0)
        {
            restart.Outcome.ShouldNotBe(DocumentLoadOutcome.DefaultInMemory);
            restart.Outcome.ShouldNotBe(DocumentLoadOutcome.FirstRun);
            restart.IsReadOnly.ShouldBeFalse();
            var marker = restart.Document.Library.Profiles[0].Name.Values.Values.First();
            marker.ShouldBeOneOf("General " + saved, "General " + (saved + 1));
        }

        restart.Quarantined.ShouldBeEmpty();
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static int CountPoints()
    {
        using var folder = new TempFolder();
        var counting = new CrashingFileSystem(int.MaxValue);
        var writer = new AtomicFile(Time, NullLogger<AtomicFile>.Instance, counting);
        for (var n = 1; n <= Saves; n++)
        {
            writer
                .WriteAsync(
                    folder.Locations.Document,
                    Marker.Bytes(n, seq: n),
                    CancellationToken.None
                )
                .IsCompletedSuccessfully.ShouldBeTrue("a healthy write never waits");
        }

        return counting.Points;
    }

    private static async Task<int> SaveUntilCrashAsync(
        AtomicFile writer,
        string path,
        Func<int, byte[]> bytes
    )
    {
        var saved = 0;
        try
        {
            for (var n = 1; n <= Saves; n++)
            {
                (await writer.WriteAsync(path, bytes(n), Token)).IsSuccess.ShouldBeTrue();
                saved = n;
            }
        }
        catch (CrashingFileSystem.SimulatedCrash)
        {
            // The process died here.
        }

        return saved;
    }

    private static UsageHistory Usage(int marks) =>
        new(
            new Dictionary<ShortcutId, ValueList<DateTimeOffset>>
            {
                [new ShortcutId("copy")] = ValueListBuilder.From(
                    Enumerable.Range(0, marks).Select(i => TestTime.Epoch.AddMinutes(i))
                ),
            }.ToImmutableDictionary()
        );

    private static UsageRepository UsageRepository(TempFolder folder) =>
        new(
            folder.Locations,
            new AtomicFile(Time, NullLogger<AtomicFile>.Instance),
            Time,
            NullLogger<UsageRepository>.Instance
        );

    private static DocumentRepository Repository(TempFolder folder, IAtomicFileWriter writer) =>
        new(
            folder.Locations,
            writer,
            new QuarantineStore(folder.Locations, Time),
            new BackupService(folder.Locations, writer, Time, NullLogger<BackupService>.Instance),
            Time,
            NullLogger<DocumentRepository>.Instance
        );
}
