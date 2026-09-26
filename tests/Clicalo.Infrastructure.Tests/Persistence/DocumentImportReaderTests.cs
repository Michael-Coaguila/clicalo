using System.Text;
using System.Text.Json.Nodes;
using Clicalo.Infrastructure.Persistence;
using Clicalo.TestKit;
using Clicalo.TestKit.Time;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>Reading a document to import (COP-002): untrusted, limited and summarized before choosing (LOG-006).</summary>
[Trait("Req", "COP-002")]
[Trait("Req", "LOG-006")]
public sealed class DocumentImportReaderTests
{
    [Fact]
    public void A_file_over_the_size_limit_is_refused_before_parsing() =>
        DocumentImportReader
            .Read(new byte[(5 * 1024 * 1024) + 1])
            .Failure.Code.ShouldBe("import.too_large");

    [Theory]
    [InlineData("")]
    [InlineData("{\"type\":\"profile-share\"}")]
    [InlineData("{\"format\":\"clicalo.usage\"}")]
    public void A_file_that_is_not_a_document_is_refused(string text) =>
        DocumentImportReader
            .Read(Encoding.UTF8.GetBytes(text))
            .Failure.Code.ShouldBe("import.unreadable");

    [Fact]
    [Trait("Req", "COP-005")]
    public void A_document_of_a_newer_major_is_refused_with_a_warning()
    {
        var future = Encoding
            .UTF8.GetString(Marker.Bytes(1, seq: 1))
            .Replace("\"major\": 1", "\"major\": 2", StringComparison.Ordinal);

        var result = DocumentImportReader.Read(Encoding.UTF8.GetBytes(future));

        result.Failure.Code.ShouldBe("import.schema_newer");
        result.Failure.Severity.ShouldBe(Domain.Errors.FailureSeverity.Warning);
    }

    [Fact]
    public void A_document_with_more_profiles_than_allowed_is_refused_before_decoding()
    {
        var profiles = new JsonArray();
        for (var i = 0; i < 201; i++)
        {
            profiles.Add(
                new JsonObject
                {
                    ["id"] = "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                }
            );
        }

        var bytes = EnvelopeCodec.Write(
            new DocumentEnvelope(
                DocumentFormats.Document,
                DocumentFormats.DocumentSchema,
                "2.0.0",
                1,
                TestTime.Epoch,
                string.Empty,
                new JsonObject { ["profiles"] = profiles }
            )
        );

        DocumentImportReader.Read(bytes).Failure.Code.ShouldBe("import.invalid");
    }

    [Fact]
    [Trait("Req", "COP-005")]
    public void A_valid_document_is_summarized_with_its_counts_version_and_warnings()
    {
        var bytes = File.ReadAllBytes(
            RepoPaths.Combine(
                "tests",
                "Clicalo.Infrastructure.Tests",
                "Fixtures",
                "schema",
                "1.0",
                "document.json"
            )
        );

        var imported = DocumentImportReader.Read(bytes).Value;

        imported.Profiles.ShouldBe(2);
        imported.Shortcuts.ShouldBe(10);
        imported.Schema.ShouldBe(new SchemaVersion(1, 0));
        imported.WrittenBy.ShouldBe("2.0.0");
        imported.UnavailableTexts.ShouldBe(1);
        imported.Repairs.ShouldBeEmpty();
    }
}
