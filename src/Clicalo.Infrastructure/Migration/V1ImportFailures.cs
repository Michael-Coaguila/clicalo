using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Infrastructure.Migration;

/// <summary>
/// The expected failures of reading a v1 file or an untrusted zip (EC-MIG-02, MIG-009, LOG-006). Nothing is written
/// and the welcome offers «Retry migration». The codes are stable and are all the log ever says; the text is a
/// placeholder until the migration keys of catalog §9 exist in <c>data/i18n</c> (the app package adds them).
/// </summary>
internal static class V1ImportFailures
{
    public const string UnreadableCode = "migration.v1.unreadable";
    public const string EmptyCode = "migration.v1.empty";
    public const string DamagedCode = "migration.v1.damaged";
    public const string NotV1Code = "migration.v1.not_v1";
    public const string NoProfilesCode = "migration.v1.no_profiles";
    public const string TooLargeCode = "migration.v1.too_large";
    public const string ZipDamagedCode = "migration.zip.damaged";
    public const string ZipTooLargeCode = "migration.zip.too_large";
    public const string ZipTooManyEntriesCode = "migration.zip.too_many_entries";
    public const string ZipRatioCode = "migration.zip.compression_ratio";
    public const string ZipEntryTooLargeCode = "migration.zip.entry_too_large";
    public const string ZipUnsafePathCode = "migration.zip.unsafe_path";
    public const string ZipWithoutProfilesCode = "migration.zip.no_profiles";

    /// <summary>The file could not be opened or read.</summary>
    public static Failure Unreadable { get; } = Create(UnreadableCode);

    /// <summary>The file is empty or only whitespace (EC-MIG-02).</summary>
    public static Failure Empty { get; } = Create(EmptyCode);

    /// <summary>The JSON is broken or truncated (EC-MIG-02).</summary>
    public static Failure Damaged { get; } = Create(DamagedCode);

    /// <summary>The JSON is not an object, so it is not a v1 file.</summary>
    public static Failure NotV1 { get; } = Create(NotV1Code);

    /// <summary>There is no <c>profiles</c> object, or it is empty (EC-MIG-02).</summary>
    public static Failure NoProfiles { get; } = Create(NoProfilesCode);

    /// <summary>The file, its profiles or its buttons pass the import limits (LOG-006).</summary>
    public static Failure TooLarge { get; } = Create(TooLargeCode);

    /// <summary>The zip cannot be read (MIG-009).</summary>
    public static Failure ZipDamaged { get; } = Create(ZipDamagedCode);

    /// <summary>The zip or its uncompressed content passes the total size limit (MIG-009).</summary>
    public static Failure ZipTooLarge { get; } = Create(ZipTooLargeCode);

    /// <summary>The zip has more entries than allowed (MIG-009).</summary>
    public static Failure ZipTooManyEntries { get; } = Create(ZipTooManyEntriesCode);

    /// <summary>An entry expands more than the ratio allows, counted while reading (MIG-009).</summary>
    public static Failure ZipRatio { get; } = Create(ZipRatioCode);

    /// <summary>A JSON entry passes its size limit (MIG-009).</summary>
    public static Failure ZipEntryTooLarge { get; } = Create(ZipEntryTooLargeCode);

    /// <summary>An entry name has <c>..</c>, an absolute path or a drive (MIG-009).</summary>
    public static Failure ZipUnsafePath { get; } = Create(ZipUnsafePathCode);

    /// <summary>The zip has no <c>profiles.json</c> (MIG-009).</summary>
    public static Failure ZipWithoutProfiles { get; } = Create(ZipWithoutProfilesCode);

    private static Failure Create(string code) =>
        new(
            code,
            L.Retry,
            FailureSeverity.Warning,
            FailureRecovery.Retry,
            FailureAnnouncement.Polite
        );
}
