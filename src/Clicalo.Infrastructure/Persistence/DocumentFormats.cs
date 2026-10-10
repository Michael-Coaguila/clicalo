namespace Clicalo.Infrastructure.Persistence;

/// <summary>The persisted formats of this version (blueprint §6.5, ADR-0007).</summary>
public static class DocumentFormats
{
    /// <summary>The <c>format</c> of <c>clicalo.json</c> and of every backup.</summary>
    public const string Document = "clicalo.document";

    /// <summary>The <c>format</c> of <c>usage.json</c>.</summary>
    public const string Usage = "clicalo.usage";

    /// <summary>The <c>type</c> of a shared profile, <c>clicalo-perfil-&lt;id&gt;.json</c> (DAT-007).</summary>
    public const string ProfileShare = "profile-share";

    /// <summary>
    /// The document schema this version writes: 1.1, the additive settings and welcome answers of M6 (ADR-0028). It
    /// reads any 1.x: a 1.0 document takes the defaults of the new members, and a later minor keeps its unknown ones.
    /// </summary>
    public static SchemaVersion DocumentSchema { get; } = new(1, 1);

    /// <summary>The usage schema this version writes and the greatest it reads: 1.0.</summary>
    public static SchemaVersion UsageSchema { get; } = new(1, 0);

    /// <summary>The shared profile schema this version writes and the greatest it reads: 1.0.</summary>
    public static SchemaVersion ProfileShareSchema { get; } = new(1, 0);

    /// <summary>The version written in <c>writtenBy</c>: this assembly's informational version, without build metadata.</summary>
    public static string AppVersion { get; } = ReadAppVersion();

    private static string ReadAppVersion()
    {
        var version =
            typeof(DocumentFormats)
                .Assembly.GetCustomAttributes(
                    typeof(System.Reflection.AssemblyInformationalVersionAttribute),
                    inherit: false
                )
                .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
                .FirstOrDefault()
                ?.InformationalVersion
            ?? "0.0.0";
        var plus = version.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? version : version[..plus];
    }
}
