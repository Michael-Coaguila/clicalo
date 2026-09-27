using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// The expected failures of persistence, backups and imports (blueprint §6.5, §6.8, DAT-002, COP-005, LOG-006). The
/// code is stable and is what the log and the tests see; the user sees the message.
/// </summary>
internal static class PersistenceFailures
{
    /// <summary>Another process kept the file locked through every retry (S11).</summary>
    public const string LockedCode = "persist.io.locked";

    /// <summary>Access was denied through every retry.</summary>
    public const string DeniedCode = "persist.io.denied";

    /// <summary>The disk is full.</summary>
    public const string DiskFullCode = "persist.io.full";

    /// <summary>Any other I/O error.</summary>
    public const string IoCode = "persist.io.failed";

    /// <summary>The document is read-only: a future major, a file that could not be read, or a default not yet accepted.</summary>
    public const string ReadOnlyCode = "persist.readonly";

    /// <summary>The serialized document did not read back identical and valid: it was not written (§6.5, step 2).</summary>
    public const string InvalidCode = "persist.invalid";

    /// <summary>A file could not be moved to quarantine (it stays where it was, untouched).</summary>
    public const string QuarantineCode = "persist.quarantine.failed";

    /// <summary>A backup is unreadable or invalid.</summary>
    public const string BackupUnreadableCode = "backup.unreadable";

    /// <summary>A backup id that does not name a backup.</summary>
    public const string BackupNotFoundCode = "backup.not_found";

    /// <summary>A backup or import of a newer major: never applied (COP-005).</summary>
    public const string SchemaNewerCode = "import.schema_newer";

    /// <summary>An import larger than <c>Timings.Import.ShareMaxBytes</c> (LOG-006).</summary>
    public const string ImportTooLargeCode = "import.too_large";

    /// <summary>An import that is not valid JSON of the expected format (LOG-006).</summary>
    public const string ImportUnreadableCode = "import.unreadable";

    /// <summary>An import over the limits of profiles, shortcuts or depth, or semantically invalid (LOG-006).</summary>
    public const string ImportInvalidCode = "import.invalid";

    /// <summary>The failure of an I/O error that survived the retries.</summary>
    /// <param name="kind">What the error meant.</param>
    public static Failure Io(IoFailureKind kind) =>
        kind switch
        {
            IoFailureKind.Locked => Critical(LockedCode, L.SaveFailT, FailureRecovery.Retry),
            IoFailureKind.Denied => Critical(DeniedCode, L.SaveFailT, FailureRecovery.Retry),
            IoFailureKind.DiskFull => Critical(DiskFullCode, L.SaveFailT, FailureRecovery.Retry),
            _ => Critical(IoCode, L.SaveFailT, FailureRecovery.Retry),
        };

    /// <summary>Saving is disabled.</summary>
    public static Failure ReadOnly() =>
        new(
            ReadOnlyCode,
            L.SaveReadOnly,
            FailureSeverity.Warning,
            FailureRecovery.RestoreBackup,
            FailureAnnouncement.Polite
        );

    /// <summary>The document failed its read-back validation and was not written.</summary>
    public static Failure Invalid() => Critical(InvalidCode, L.SaveFailT, FailureRecovery.None);

    /// <summary>A file could not be quarantined.</summary>
    public static Failure Quarantine() =>
        Critical(QuarantineCode, L.DataUnreadable, FailureRecovery.Retry);

    /// <summary>A warning with <paramref name="code"/> and no automatic recovery.</summary>
    /// <param name="code">One of the codes of this class.</param>
    public static Failure Warning(string code) =>
        new(
            code,
            MessageOf(code),
            FailureSeverity.Warning,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );

    private static Message MessageOf(string code) =>
        code switch
        {
            SchemaNewerCode => L.SchemaNewer,
            ImportTooLargeCode => L.ImportTooLarge,
            ImportUnreadableCode or ImportInvalidCode => L.ImportInvalid,
            BackupUnreadableCode or BackupNotFoundCode => L.BackupDamaged,
            _ => L.SaveFailT,
        };

    private static Failure Critical(string code, Message message, FailureRecovery recovery) =>
        new(code, message, FailureSeverity.Critical, recovery, FailureAnnouncement.Assertive);
}
