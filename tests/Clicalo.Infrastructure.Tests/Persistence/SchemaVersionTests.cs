using Clicalo.Infrastructure.Persistence;

namespace Clicalo.Infrastructure.Tests.Persistence;

/// <summary>The document is a major.minor format (blueprint §6.5, ADR-0007).</summary>
[Trait("Req", "DAT-001")]
public sealed class SchemaVersionTests
{
    [Fact]
    public void This_version_writes_document_and_usage_schema_1_0()
    {
        DocumentFormats.DocumentSchema.ShouldBe(new SchemaVersion(1, 0));
        DocumentFormats.UsageSchema.ShouldBe(new SchemaVersion(1, 0));
        DocumentFormats.DocumentSchema.ToString().ShouldBe("1.0");
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
