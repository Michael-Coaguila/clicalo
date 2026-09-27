using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Domain.Frequents;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary><c>usage.json</c> (blueprint §6.5): the only frequent writes, kept out of the document.</summary>
[Trait("Req", "FRE-002")]
[Trait("Req", "DAT-002")]
public sealed class UsageRepositoryTests : IDisposable
{
    private readonly BoundedTestToken _bounded = new();
    private readonly TempFolder _folder = new();

    public void Dispose()
    {
        _bounded.Dispose();
        _folder.Dispose();
    }

    [Fact]
    public async Task Saves_and_loads_the_usage_of_its_epoch()
    {
        var usage = Usage(("copy", 3), ("paste", 1));

        (await Repository().SaveAsync(3, usage, Token)).Value.Seq.ShouldBe(1);
        var loaded = await Repository().LoadAsync(3, Token);

        loaded
            .Entries.Keys.Select(k => k.Value)
            .Order(StringComparer.Ordinal)
            .ShouldBe(["copy", "paste"]);
        loaded.Entries[new ShortcutId("copy")].ShouldBe(usage.Entries[new ShortcutId("copy")]);
    }

    [Fact]
    public async Task Writes_the_usage_envelope_with_keys_in_ordinal_order()
    {
        await Repository().SaveAsync(3, Usage(("zeta", 1), ("alpha", 1), ("Beta", 1)), Token);

        var root = JsonNode.Parse(File.ReadAllText(_folder.Locations.Usage))!;
        root["format"]!.GetValue<string>().ShouldBe("clicalo.usage");
        root["payload"]!["usageEpoch"]!.GetValue<long>().ShouldBe(3);
        root["payload"]!["usage"]!
            .AsObject()
            .Select(p => p.Key)
            .ShouldBe(["Beta", "alpha", "zeta"]);
    }

    [Fact]
    [Trait("Req", "FRE-004")]
    public async Task Usage_of_another_epoch_starts_empty()
    {
        await Repository().SaveAsync(3, Usage(("copy", 2)), Token);

        (await Repository().LoadAsync(4, Token)).ShouldBe(UsageHistory.Empty);
    }

    [Fact]
    public async Task A_missing_file_starts_empty()
    {
        (await Repository().LoadAsync(0, Token)).ShouldBe(UsageHistory.Empty);
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public async Task An_unreadable_file_starts_empty_from_prev_when_possible_and_is_never_quarantined()
    {
        await Repository().SaveAsync(3, Usage(("copy", 1)), Token);
        await Repository().SaveAsync(3, Usage(("copy", 2)), Token);
        File.WriteAllText(_folder.Locations.Usage, "{ broken");

        var loaded = await Repository().LoadAsync(3, Token);

        loaded.Entries[new ShortcutId("copy")].Count.ShouldBe(1);
        Directory.Exists(_folder.Locations.Quarantine).ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-08")]
    public async Task A_usage_file_of_a_future_major_is_never_overwritten()
    {
        await Repository().SaveAsync(3, Usage(("copy", 1)), Token);
        var future = File.ReadAllText(_folder.Locations.Usage)
            .Replace("\"major\": 1", "\"major\": 2", StringComparison.Ordinal);
        File.WriteAllText(_folder.Locations.Usage, future);
        var repository = Repository();

        (await repository.LoadAsync(3, Token)).ShouldBe(UsageHistory.Empty);
        var save = await repository.SaveAsync(3, Usage(("copy", 5)), Token);

        save.Failure.Code.ShouldBe("persist.readonly");
        File.ReadAllText(_folder.Locations.Usage).ShouldBe(future);
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public async Task Unknown_members_of_a_later_minor_survive_a_rewrite()
    {
        await Repository().SaveAsync(3, Usage(("copy", 1)), Token);
        var root = JsonNode.Parse(File.ReadAllText(_folder.Locations.Usage))!.AsObject();
        var payload = root["payload"]!.AsObject();
        payload["streaks"] = new JsonObject { ["copy"] = 4 };
        root["payloadSha256"] = JsonTextHash(payload);
        File.WriteAllText(_folder.Locations.Usage, root.ToJsonString(), new UTF8Encoding(false));
        var repository = Repository();

        _ = await repository.LoadAsync(3, Token);
        await repository.SaveAsync(3, Usage(("copy", 2)), Token);

        File.ReadAllText(_folder.Locations.Usage).ShouldContain("streaks");
    }

    [Fact]
    public async Task Each_save_raises_the_sequence_above_everything_on_disk()
    {
        await Repository().SaveAsync(3, Usage(("copy", 1)), Token);
        await Repository().SaveAsync(3, Usage(("copy", 1)), Token);
        var repository = Repository();
        _ = await repository.LoadAsync(3, Token);

        (await repository.SaveAsync(3, Usage(("copy", 1)), Token)).Value.Seq.ShouldBe(2);
    }

    internal static UsageHistory Usage(params (string Id, int Marks)[] entries) =>
        new(
            entries.ToImmutableDictionary(
                e => new ShortcutId(e.Id),
                e =>
                    ValueListBuilder.From(
                        Enumerable.Range(0, e.Marks).Select(i => TestTime.Epoch.AddMinutes(i))
                    )
            )
        );

    private static string JsonTextHash(JsonObject payload)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(
                payload.ToJsonString(
                    new System.Text.Json.JsonSerializerOptions
                    {
                        Encoder = System
                            .Text
                            .Encodings
                            .Web
                            .JavaScriptEncoder
                            .UnsafeRelaxedJsonEscaping,
                    }
                )
            )
        );
        return Convert.ToHexStringLower(hash);
    }

    private CancellationToken Token => _bounded.Token;

    private UsageRepository Repository()
    {
        var time = TestTime.CreateProvider();
        return new UsageRepository(
            _folder.Locations,
            new AtomicFile(time, NullLogger<AtomicFile>.Instance),
            time,
            NullLogger<UsageRepository>.Instance
        );
    }
}
