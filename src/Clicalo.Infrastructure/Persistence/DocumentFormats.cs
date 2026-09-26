namespace Clicalo.Infrastructure.Persistence;

/// <summary>The persisted formats of this version (blueprint §6.5, ADR-0007).</summary>
public static class DocumentFormats
{
    /// <summary>The <c>format</c> of <c>clicalo.json</c> and of every backup.</summary>
    public const string Document = "clicalo.document";

    /// <summary>The <c>format</c> of <c>usage.json</c>.</summary>
    public const string Usage = "clicalo.usage";

    /// <summary>The document schema this version writes and the greatest it reads: 1.0.</summary>
    public static SchemaVersion DocumentSchema { get; } = new(1, 0);

    /// <summary>The usage schema this version writes and the greatest it reads: 1.0.</summary>
    public static SchemaVersion UsageSchema { get; } = new(1, 0);
}
