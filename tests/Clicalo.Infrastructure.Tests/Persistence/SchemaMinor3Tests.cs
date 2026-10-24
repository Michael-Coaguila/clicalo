using System.Text.Json;
using System.Text.Json.Nodes;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Schema 1.3 (ADR-0034): the first-time guides done are an optional list of the settings, so the immutable 1.3
/// fixture reads with it, a 1.2 document reads with every guide still to show, the list survives a round trip and a
/// 1.2 version keeps it as an unknown member (REG-08).
/// </summary>
[Trait("Req", "DAT-001")]
[Trait("Req", "PES-015")]
public sealed class SchemaMinor3Tests
{
    [Fact]
    public void The_1_3_fixture_reads_with_a_matching_hash_and_no_unknown_members()
    {
        var read = Read("1.3");

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 3));
        var dto = read.Envelope.Payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;
        dto.Extra.ShouldBeNull();
        dto.Settings!.Extra.ShouldBeNull();
    }

    [Fact]
    public void The_1_3_fixture_decodes_the_guides_done_apart_from_the_tab_one()
    {
        var decoded = new DocumentCodec().Decode(Read("1.3").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        decoded.Document.Settings.GuidesDone.ShouldBe(["panel", "bubble", "shortcuts"]);
        decoded.Document.Settings.Dock.CoachDone.ShouldBeFalse();
    }

    [Fact]
    public void A_1_2_document_still_shows_every_guide()
    {
        var decoded = new DocumentCodec().Decode(Read("1.2").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        decoded.Document.Settings.GuidesDone.ShouldBeEmpty();
    }

    [Fact]
    public void The_guides_done_survive_a_round_trip()
    {
        var document = TestDocuments.Document(1);
        var codec = new DocumentCodec();

        var payload = codec.Encode(document, includeUsage: false);
        var decoded = codec.Decode(payload, live: false).Value;

        payload["settings"]!["guidesDone"]!
            .AsArray()
            .Select(static n => n!.GetValue<string>())
            .ShouldBe(["panel", "touch"]);
        decoded.Document.Settings.ShouldBe(document.Settings);
    }

    [Fact]
    public void A_damaged_list_is_repaired_never_fatal()
    {
        var payload = Read("1.3").Envelope.Payload;
        payload["settings"]!["guidesDone"] = new JsonArray("panel", "", "panel", "later");

        var decoded = new DocumentCodec().Decode(payload, live: false).Value;

        decoded.Document.Settings.GuidesDone.ShouldBe(["panel", "later"]);
    }

    [Fact]
    public void A_1_2_version_keeps_the_list_as_an_unknown_member_of_the_settings()
    {
        // What a 1.2 version reads: the settings without the member it does not know.
        var payload = Read("1.3").Envelope.Payload;
        var settings = payload["settings"]!.AsObject();
        var list = settings["guidesDone"]!.DeepClone();
        _ = settings.Remove("guidesDone");
        settings["laterMember"] = list;

        var dto = payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;

        dto.Settings!.Extra.ShouldNotBeNull();
        dto.Settings.Extra.ShouldContainKey("laterMember");
        var codec = new DocumentCodec();
        var decoded = codec.Decode(payload, live: true).Value;
        var rewritten = codec.Encode(decoded.Document, includeUsage: false);
        rewritten["settings"]!["laterMember"]!.AsArray().Count.ShouldBe(3);
    }

    private static EnvelopeReadResult.Readable Read(string version) =>
        EnvelopeCodec
            .Read(
                File.ReadAllBytes(
                    RepoPaths.Combine(
                        "tests",
                        "Clicalo.Infrastructure.Tests",
                        "Fixtures",
                        "schema",
                        version,
                        "document.json"
                    )
                ),
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            )
            .ShouldBeOfType<EnvelopeReadResult.Readable>();
}
