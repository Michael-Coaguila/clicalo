namespace Clicalo.Infrastructure.Migration;

/// <summary>The v1 files Import accepts (MIG-009).</summary>
public enum V1SourceKind
{
    /// <summary>A <c>profiles.json</c>.</summary>
    ProfilesJson,

    /// <summary><c>profiles.backup.es.json</c> (with <c>_nota</c>) or <c>profiles.backup.en.json</c>.</summary>
    LanguageBackup,

    /// <summary>The <c>.zip</c> backup that contains a <c>profiles.json</c>.</summary>
    Zip,
}
