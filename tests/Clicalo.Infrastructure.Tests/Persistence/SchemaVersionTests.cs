using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>The document is a major.minor format (blueprint §6.5, ADR-0007).</summary>
[Trait("Req", "DAT-001")]
public sealed class SchemaVersionTests
{
    [Fact]
    public void This_version_writes_document_schema_1_3_and_usage_schema_1_0()
    {
        // ADR-0028, ADR-0033 and ADR-0034: the M6 settings and welcome answers, the AI provider and the done flags of
        // the first-use guides are additive minors.
        DocumentFormats.DocumentSchema.ShouldBe(new SchemaVersion(1, 3));
        DocumentFormats.UsageSchema.ShouldBe(new SchemaVersion(1, 0));
        DocumentFormats.DocumentSchema.ToString().ShouldBe("1.3");
    }

    [Fact]
    public void A_greater_major_is_newer_whatever_the_minor()
    {
        (new SchemaVersion(2, 0) > new SchemaVersion(1, 9)).ShouldBeTrue();
        (new SchemaVersion(1, 1) > new SchemaVersion(1, 0)).ShouldBeTrue();
        (new SchemaVersion(1, 0) <= new SchemaVersion(1, 0)).ShouldBeTrue();
    }

    [Fact]
    public void The_data_files_live_in_the_data_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "clicalo-layout");
        var locations = new DataLocations(root);

        locations.Document.ShouldBe(Path.Combine(root, "clicalo.json"));
        locations.DocumentPrevious.ShouldBe(Path.Combine(root, "clicalo.json.prev"));
        locations.Usage.ShouldBe(Path.Combine(root, "usage.json"));
        locations.Quarantine.ShouldBe(Path.Combine(root, "quarantine"));
    }
}
