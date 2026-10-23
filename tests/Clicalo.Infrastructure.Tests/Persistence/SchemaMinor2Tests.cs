using System.Text.Json;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Schema 1.2 (ADR-0033): the done flag of the guide of the Full and Compact views is an optional member of the
/// settings, so the immutable 1.2 fixture reads with it, a 1.1 document reads with the guide still to show, the flag
/// survives a round trip and a 1.1 version keeps it as an unknown member (REG-08).
/// </summary>
[Trait("Req", "DAT-001")]
[Trait("Req", "PES-015")]
public sealed class SchemaMinor2Tests
{
    [Fact]
    public void The_1_2_fixture_reads_with_a_matching_hash_and_no_unknown_members()
    {
        var read = Read("1.2");

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 2));
        var dto = read.Envelope.Payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;
        dto.Extra.ShouldBeNull();
        dto.Settings!.Extra.ShouldBeNull();
    }

    [Fact]
    public void The_1_2_fixture_decodes_the_done_flag_of_the_panel_guide_apart_from_the_tab_one()
    {
        var decoded = new DocumentCodec().Decode(Read("1.2").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        decoded.Document.Settings.PanelCoachDone.ShouldBeTrue();
        decoded.Document.Settings.Dock.CoachDone.ShouldBeFalse();
    }

    [Fact]
    public void A_1_1_document_still_shows_the_panel_guide()
    {
        var decoded = new DocumentCodec().Decode(Read("1.1").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        decoded.Document.Settings.PanelCoachDone.ShouldBeFalse();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_done_flag_survives_a_round_trip(bool done)
    {
        var document = TestDocuments.Document(1) with
        {
            Settings = TestDocuments.Settings with { PanelCoachDone = done },
        };
        var codec = new DocumentCodec();

        var payload = codec.Encode(document, includeUsage: false);
        var decoded = codec.Decode(payload, live: false).Value;

        payload["settings"]!["panelCoachDone"]!.GetValue<bool>().ShouldBe(done);
        decoded.Document.Settings.ShouldBe(document.Settings);
    }

    [Fact]
    public void A_1_1_version_keeps_the_flag_as_an_unknown_member_of_the_settings()
    {
        // What a 1.1 version reads: the settings without the member it does not know.
        var payload = Read("1.2").Envelope.Payload;
        var settings = payload["settings"]!.AsObject();
        var flag = settings["panelCoachDone"]!.DeepClone();
        _ = settings.Remove("panelCoachDone");
        settings["laterMember"] = flag;

        var dto = payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;

        dto.Settings!.Extra.ShouldNotBeNull();
        dto.Settings.Extra.ShouldContainKey("laterMember");
        var codec = new DocumentCodec();
        var decoded = codec.Decode(payload, live: true).Value;
        var rewritten = codec.Encode(decoded.Document, includeUsage: false);
        rewritten["settings"]!["laterMember"]!.GetValue<bool>().ShouldBeTrue();
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
