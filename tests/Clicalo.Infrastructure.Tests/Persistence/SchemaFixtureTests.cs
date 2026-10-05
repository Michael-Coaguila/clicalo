using System.Text.Json;
using Clicalo.Domain.Library;
using Clicalo.Domain.Primitives;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.TestKit;
using Clicalo.TestKit.Time;
using Microsoft.Extensions.Logging.Abstractions;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// The immutable fixtures of schema 1.0 (blueprint §6.6: <c>Fixtures/schema/&lt;major.minor&gt;/</c>): every later version
/// must keep reading them. They cover every action type, every setting group, a text encrypted for another user and a
/// dangling pin.
/// </summary>
[Trait("Req", "DAT-001")]
[Trait("Req", "ACT-005")]
public sealed class SchemaFixtureTests
{
    private static readonly string Folder = RepoPaths.Combine(
        "tests",
        "Clicalo.Infrastructure.Tests",
        "Fixtures",
        "schema",
        "1.0"
    );

    [Theory]
    [InlineData("document.json", DocumentFormats.Document)]
    [InlineData("usage.json", DocumentFormats.Usage)]
    public void The_1_0_fixtures_read_with_a_matching_hash(string file, string format)
    {
        var read = EnvelopeCodec
            .Read(File.ReadAllBytes(Path.Combine(Folder, file)), format, new SchemaVersion(1, 0))
            .ShouldBeOfType<EnvelopeReadResult.Readable>();

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 0));
    }

    [Fact]
    public void The_1_0_document_payload_maps_to_the_dtos_without_unknown_members()
    {
        var payload = Payload();

        var dto = payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;

        dto.Extra.ShouldBeNull();
        dto.Settings!.Extra.ShouldBeNull();
        dto.Profiles!.Select(p => p.Id).ShouldBe(["general", "word"]);
        dto.Profiles!.SelectMany(p => p.Shortcuts!)
            .Select(s => s.Action!.Type)
            .ShouldBe(["tap", "text", "toggle", "mouse", "url", "app", "system", "macro"]);
        dto.Always!.Shortcuts!.Select(s => s.Action!.Type).ShouldBe(["tap", "hold"]);
    }

    [Fact]
    [Trait("Req", "FRE-002")]
    public async Task The_1_0_usage_fixture_loads_for_its_epoch()
    {
        using var folder = new TempFolder();
        Directory.CreateDirectory(folder.Locations.Root);
        File.Copy(Path.Combine(Folder, "usage.json"), folder.Locations.Usage);
        var time = TestTime.CreateProvider();

        var usage = await new UsageRepository(
            folder.Locations,
            new AtomicFile(time, NullLogger<AtomicFile>.Instance),
            time,
            NullLogger<UsageRepository>.Instance
        ).LoadAsync(3, TestContext.Current.CancellationToken);

        usage.Entries[new ShortcutId("copy")].Count.ShouldBe(2);
        usage
            .Entries[new ShortcutId("web")][0]
            .ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1767603720000));
    }

    [Fact]
    [Trait("Req", "COP-005")]
    [Trait("Req", "FRE-005")]
    public void The_1_0_document_decodes_into_a_valid_document()
    {
        var decoded = new DocumentCodec().Decode(Payload(), live: false).Value;

        var document = decoded.Document;
        decoded.Repairs.ShouldBeEmpty();
        decoded.UnavailableTexts.ShouldBe(1);
        document.Library.Profiles.Count.ShouldBe(2);
        document.Library.AlwaysVisible.Count.ShouldBe(2);
        document.Settings.Opacity.ShouldBe(0.92);
        document.Settings.LastProfile.ShouldBe(new ProfileId("word"));
        document.Frequents.Pins.Select(p => p.Value).ShouldBe(["copy", "deleted-long-ago"]);
        var word = document.Library.Profiles[1];
        word.Injection.ShouldBe(Domain.Keys.InjectionMode.ScanCode);
        word.Binding.ShouldBeOfType<AppBinding.Processes>()
            .Names.Single()
            .Value.ShouldBe("winword.exe");
        word.Shortcuts[0].Action.ShouldBeOfType<MacroAction>().Steps.Count.ShouldBe(4);
        document
            .Library.Profiles[0]
            .Shortcuts[1]
            .Action.ShouldBeOfType<TextAction>()
            .Text.IsAvailable.ShouldBeFalse();
    }

    private static System.Text.Json.Nodes.JsonObject Payload() =>
        EnvelopeCodec
            .Read(
                File.ReadAllBytes(Path.Combine(Folder, "document.json")),
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            )
            .ShouldBeOfType<EnvelopeReadResult.Readable>()
            .Envelope.Payload;
}
