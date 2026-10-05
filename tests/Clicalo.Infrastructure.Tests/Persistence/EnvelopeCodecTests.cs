using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Infrastructure.Persistence;
using CsCheck;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>The envelope 1.0 of every persisted file (blueprint §6.5, ADR-0007, ADR-0018).</summary>
[Trait("Req", "DAT-001")]
public sealed class EnvelopeCodecTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 25, 10, 31, 2, TimeSpan.Zero);

    [Fact]
    public void Writes_the_envelope_fields_in_order_as_utf8_without_bom_and_with_lf()
    {
        var bytes = EnvelopeCodec.Write(Envelope(new JsonObject { ["n"] = 1 }, seq: 1842));
        var text = Encoding.UTF8.GetString(bytes);

        bytes.AsSpan().StartsWith((byte[])[0xEF, 0xBB, 0xBF]).ShouldBeFalse();
        text.ShouldNotContain('\r');
        text.ShouldEndWith("}\n");
        var root = JsonNode.Parse(text)!.AsObject();
        root.Select(p => p.Key)
            .ShouldBe([
                "format",
                "schema",
                "writtenBy",
                "seq",
                "writtenAtUtc",
                "payloadSha256",
                "payload",
            ]);
        root["format"]!.GetValue<string>().ShouldBe(DocumentFormats.Document);
        root["schema"]!["major"]!.GetValue<int>().ShouldBe(1);
        root["schema"]!["minor"]!.GetValue<int>().ShouldBe(0);
        root["seq"]!.GetValue<long>().ShouldBe(1842);
        root["writtenAtUtc"]!.GetValue<string>().ShouldBe("2026-09-25T10:31:02.000Z");
        root["payloadSha256"]!.GetValue<string>().ShouldMatch("^[0-9a-f]{64}$");
    }

    [Fact]
    public void Reads_back_what_it_writes_with_a_matching_hash()
    {
        var payload = new JsonObject
        {
            ["name"] = "Ñandú «ok»",
            ["n"] = 0.92,
            ["list"] = new JsonArray(1, 2),
        };
        var bytes = EnvelopeCodec.Write(Envelope(payload, seq: 7));

        var read = EnvelopeCodec
            .Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema)
            .ShouldBeOfType<EnvelopeReadResult.Readable>();

        read.HashMatches.ShouldBeTrue();
        read.Envelope.Seq.ShouldBe(7);
        read.Envelope.WrittenAtUtc.ShouldBe(At);
        JsonNode.DeepEquals(read.Envelope.Payload, payload).ShouldBeTrue();
        Encoding.UTF8.GetString(bytes).ShouldContain("Ñandú «ok»");
    }

    [Fact]
    public void Writing_is_deterministic()
    {
        var first = EnvelopeCodec.Write(Envelope(new JsonObject { ["a"] = "b" }, seq: 3));
        var second = EnvelopeCodec.Write(Envelope(new JsonObject { ["a"] = "b" }, seq: 3));

        first.ShouldBe(second);
    }

    [Fact]
    public void A_bom_is_tolerated()
    {
        var bytes = EnvelopeCodec.Write(Envelope(new JsonObject { ["n"] = 1 }, seq: 1));

        EnvelopeCodec
            .Read(
                [0xEF, 0xBB, 0xBF, .. bytes],
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            )
            .ShouldBeOfType<EnvelopeReadResult.Readable>()
            .HashMatches.ShouldBeTrue();
    }

    [Fact]
    [Trait("Req", "DAT-003")]
    public void An_edited_payload_is_readable_but_its_hash_does_not_match()
    {
        var text = Encoding.UTF8.GetString(
            EnvelopeCodec.Write(Envelope(new JsonObject { ["n"] = 1 }, seq: 1))
        );
        var edited = Encoding.UTF8.GetBytes(
            text.Replace("\"n\": 1", "\"n\": 2", StringComparison.Ordinal)
        );

        EnvelopeCodec
            .Read(edited, DocumentFormats.Document, DocumentFormats.DocumentSchema)
            .ShouldBeOfType<EnvelopeReadResult.Readable>()
            .HashMatches.ShouldBeFalse();
    }

    [Fact]
    [Trait("Req", "REG-08")]
    [Trait("Req", "DAT-003")]
    public void A_future_major_is_recognized_and_not_unreadable()
    {
        var bytes = EnvelopeCodec.Write(
            Envelope(new JsonObject { ["n"] = 1 }, seq: 1) with
            {
                Schema = new SchemaVersion(2, 0),
            }
        );

        EnvelopeCodec
            .Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema)
            .ShouldBeOfType<EnvelopeReadResult.FutureMajor>()
            .Found.ShouldBe(new SchemaVersion(2, 0));
    }

    [Fact]
    [Trait("Req", "ACT-005")]
    public void A_later_minor_is_read_and_its_unknown_fields_survive_a_rewrite()
    {
        var payload = new JsonObject
        {
            ["n"] = 1,
            ["addedInMinor1"] = new JsonObject { ["x"] = true },
        };
        var bytes = EnvelopeCodec.Write(
            Envelope(payload, seq: 1) with
            {
                Schema = new SchemaVersion(1, 1),
            }
        );

        var read = EnvelopeCodec
            .Read(bytes, DocumentFormats.Document, DocumentFormats.DocumentSchema)
            .ShouldBeOfType<EnvelopeReadResult.Readable>();
        var rewritten = EnvelopeCodec.Write(read.Envelope with { Seq = 2 });

        Encoding.UTF8.GetString(rewritten).ShouldContain("addedInMinor1");
        read.Envelope.Schema.ShouldBe(new SchemaVersion(1, 1));
    }

    [Theory]
    [Trait("Req", "DAT-003")]
    [InlineData("", "json")]
    [InlineData("{\"format\":\"clicalo.document\"", "json")]
    [InlineData("[1,2]", "shape")]
    [InlineData("{\"format\":\"clicalo.usage\"}", "format")]
    [InlineData("{\"format\":\"clicalo.document\",\"schema\":1}", "schema")]
    [InlineData("{\"format\":\"clicalo.document\",\"schema\":{\"major\":1,\"minor\":0}}", "fields")]
    [InlineData(
        "{\"format\":\"clicalo.document\",\"schema\":{\"major\":1,\"minor\":0},\"writtenBy\":\"2\",\"seq\":1,\"writtenAtUtc\":\"2026-01-01T00:00:00Z\",\"payloadSha256\":\"x\",\"payload\":[]}",
        "payload"
    )]
    [InlineData("{\"format\":\"clicalo.document\",\"format\":\"clicalo.document\"}", "json")]
    public void Damaged_or_foreign_files_are_unreadable(string text, string reason) =>
        EnvelopeCodec
            .Read(
                Encoding.UTF8.GetBytes(text),
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            )
            .ShouldBeOfType<EnvelopeReadResult.Unreadable>()
            .Reason.ShouldBe(reason);

    [Fact]
    [Trait("Req", "DAT-003")]
    public void Every_truncation_of_a_valid_file_is_unreadable_never_a_different_document()
    {
        var bytes = EnvelopeCodec.Write(
            Envelope(new JsonObject { ["n"] = 12345, ["text"] = "abcdefghij" }, seq: 9)
        );

        for (var length = 0; length < bytes.Length - 1; length++)
        {
            EnvelopeCodec
                .Read(
                    bytes.AsSpan(0, length),
                    DocumentFormats.Document,
                    DocumentFormats.DocumentSchema
                )
                .ShouldBeOfType<EnvelopeReadResult.Unreadable>(
                    string.Create(CultureInfo.InvariantCulture, $"{length} of {bytes.Length} bytes")
                );
        }
    }

    [Theory]
    [Trait("Req", "DAT-003")]
    [InlineData(0xFF)]
    [InlineData(0xC3)]
    [InlineData(0x00)]
    [InlineData((int)'"')]
    public void A_damaged_byte_anywhere_never_throws_it_is_read_or_unreadable(int damage)
    {
        var bytes = EnvelopeCodec.Write(
            Envelope(new JsonObject { ["text"] = "Ñandú", ["n"] = 12 }, seq: 3)
        );

        for (var index = 0; index < bytes.Length; index++)
        {
            var damaged = bytes.ToArray();
            damaged[index] = (byte)damage;

            var read = EnvelopeCodec.Read(
                damaged,
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema
            );

            read.ShouldNotBeNull(
                string.Create(CultureInfo.InvariantCulture, $"byte {index} set to {damage}")
            );
        }
    }

    [Fact]
    public void Any_payload_round_trips_with_a_matching_hash() =>
        Gen.Dictionary(
                Gen.String[Gen.Char.AlphaNumeric, 1, 8],
                Gen.OneOf(
                    Gen.Int.Select(i => (JsonNode)JsonValue.Create(i)),
                    Gen.Double.Where(double.IsFinite).Select(d => (JsonNode)JsonValue.Create(d)),
                    Gen.String[Gen.Char['\u0000', '퟿'], 0, 20]
                        .Select(s => (JsonNode)JsonValue.Create(s)),
                    Gen.Bool.Select(b => (JsonNode)JsonValue.Create(b))
                )
            )
            .Sample(values =>
            {
                var payload = new JsonObject();
                foreach (var pair in values)
                {
                    payload[pair.Key] = pair.Value.DeepClone();
                }

                var read = EnvelopeCodec.Read(
                    EnvelopeCodec.Write(Envelope(payload, seq: 1)),
                    DocumentFormats.Document,
                    DocumentFormats.DocumentSchema
                );

                return read is EnvelopeReadResult.Readable { HashMatches: true } readable
                    && JsonNode.DeepEquals(readable.Envelope.Payload, payload);
            });

    private static DocumentEnvelope Envelope(JsonObject payload, long seq) =>
        new(
            DocumentFormats.Document,
            DocumentFormats.DocumentSchema,
            "2.0.0",
            seq,
            At,
            string.Empty,
            payload
        );
}
