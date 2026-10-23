using System.Text.Json;
using Clicalo.Domain.Settings;
using Clicalo.Infrastructure.Persistence;
using Clicalo.Infrastructure.Persistence.Dto;
using Clicalo.TestKit;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>
/// Schema 1.2 (ADR-0033): the AI provider is an optional member at the level of <c>settings</c>, so the immutable 1.2
/// fixture reads with Gemini, a 1.1 document reads with Anthropic as before, the choice survives a round trip where a 1.1
/// version keeps it, and an unknown value is Anthropic, never fatal (REG-08).
/// </summary>
[Trait("Req", "DAT-001")]
public sealed class SchemaMinor2Tests
{
    [Fact]
    public void The_1_2_fixture_reads_with_a_matching_hash_and_no_unknown_members()
    {
        var read = Read("1.2");

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 2));
        DocumentFormats.DocumentSchema.ShouldBe(new SchemaVersion(1, 2));
        var dto = read.Envelope.Payload.Deserialize(DocumentJsonContext.Default.PayloadDto)!;
        dto.Extra.ShouldBeNull();
        dto.Settings!.Extra.ShouldBeNull();
    }

    [Fact]
    [Trait("Req", "PLA-004")]
    public void The_1_2_fixture_decodes_the_provider_with_its_key_and_consent()
    {
        var decoded = new DocumentCodec().Decode(Read("1.2").Envelope.Payload, live: false).Value;

        decoded.Repairs.ShouldBeEmpty();
        var ai = decoded.Document.Settings.Ai;
        ai.Provider.ShouldBe(AiProvider.Gemini);
        ai.Consent.ShouldBeTrue();
        ai.ApiKeyRef.ShouldBe("Clicalo/ai/gemini");
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.1")]
    public void An_older_document_reads_with_anthropic_as_before(string version)
    {
        var decoded = new DocumentCodec().Decode(Read(version).Envelope.Payload, live: false).Value;

        decoded.Document.Settings.Ai.Provider.ShouldBe(AiProvider.Anthropic);
    }

    [Fact]
    public void The_provider_survives_a_round_trip_at_the_level_of_the_settings()
    {
        var document = TestDocuments.Document(1) with
        {
            Settings = TestDocuments.Settings with
            {
                Ai = TestDocuments.Settings.Ai with { Provider = AiProvider.Gemini },
            },
        };
        var codec = new DocumentCodec();

        var payload = codec.Encode(document, includeUsage: false);
        var decoded = codec.Decode(payload, live: false).Value;

        decoded.Document.Settings.ShouldBe(document.Settings);
        var settings = payload["settings"]!.AsObject();
        settings["aiProvider"]!.GetValue<string>().ShouldBe("gemini");
        settings["ai"]!
            .AsObject()
            .ContainsKey("provider")
            .ShouldBeFalse("ai has no extension data, so a 1.1 version would drop it there");
    }

    [Theory]
    [Trait("Req", "DAT-003")]
    [InlineData("openai")]
    [InlineData("")]
    public void An_unknown_provider_reads_as_anthropic(string value)
    {
        var payload = Read("1.2").Envelope.Payload;
        payload["settings"]!["aiProvider"] = value;

        var decoded = new DocumentCodec().Decode(payload, live: false).Value;

        decoded.Document.Settings.Ai.Provider.ShouldBe(AiProvider.Anthropic);
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
