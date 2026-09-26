namespace Clicalo.Infrastructure.Persistence;

/// <summary>
/// Classifies the I/O errors of the write protocol (blueprint §6.5): sharing and lock violations, the three
/// <c>ReplaceFileW</c> failures, a transient <c>ACCESS_DENIED</c> and a file that vanished are retried at
/// <c>Timings.Persistence.WriteRetryBackoff</c>; a full disk and anything else fail at once.
/// </summary>
internal static class TransientIo
{
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorLockViolation = 33;
    private const int ErrorHandleDiskFull = 39;
    private const int ErrorFileExists = 80;
    private const int ErrorDiskFull = 112;
    private const int ErrorAlreadyExists = 183;
    private const int ErrorUnableToRemoveReplaced = 1175;
    private const int ErrorUnableToMoveReplacement = 1176;
    private const int ErrorUnableToMoveReplacement2 = 1177;
    private const int ErrorUserMappedFile = 1224;

    /// <summary>What <paramref name="exception"/> means.</summary>
    /// <param name="exception">An <see cref="IOException"/> or an <see cref="UnauthorizedAccessException"/>.</param>
    public static IoFailureKind Classify(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception is UnauthorizedAccessException)
        {
            return IoFailureKind.Denied;
        }

        if (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return IoFailureKind.Missing;
        }

        return (exception.HResult & 0xFFFF) switch
        {
            ErrorSharingViolation
            or ErrorLockViolation
            or ErrorUnableToRemoveReplaced
            or ErrorUnableToMoveReplacement
            or ErrorUnableToMoveReplacement2
            or ErrorUserMappedFile => IoFailureKind.Locked,
            ErrorAccessDenied => IoFailureKind.Denied,
            ErrorFileNotFound or ErrorPathNotFound or ErrorFileExists or ErrorAlreadyExists =>
                IoFailureKind.Missing,
            ErrorDiskFull or ErrorHandleDiskFull => IoFailureKind.DiskFull,
            _ => IoFailureKind.Other,
        };
    }

    /// <summary>Whether a failure of this kind is worth retrying.</summary>
    /// <param name="kind">The failure.</param>
    public static bool IsTransient(IoFailureKind kind) =>
        kind is IoFailureKind.Locked or IoFailureKind.Denied or IoFailureKind.Missing;

    /// <summary>Whether <paramref name="exception"/> is an I/O error this protocol handles (not a defect).</summary>
    /// <param name="exception">Any exception.</param>
    public static bool IsIo(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;
}
