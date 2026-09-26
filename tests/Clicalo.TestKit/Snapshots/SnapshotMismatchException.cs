namespace Clicalo.TestKit.Snapshots;

/// <summary>A snapshot differs from its verified file, or has none yet. The message explains how to review it.</summary>
public sealed class SnapshotMismatchException : Exception
{
    public SnapshotMismatchException() { }

    public SnapshotMismatchException(string message)
        : base(message) { }

    public SnapshotMismatchException(string message, Exception innerException)
        : base(message, innerException) { }

    /// <summary>Builds the standard failure message around a format-specific <paramref name="detail"/>.</summary>
    public static SnapshotMismatchException For(
        SnapshotLocation location,
        string verifiedPath,
        string receivedPath,
        string detail
    ) =>
        new(
            (
                File.Exists(verifiedPath)
                    ? "Snapshot '" + location.BaseName + "' does not match its verified file."
                    : "Snapshot '" + location.BaseName + "' has no verified file yet."
            )
                + Environment.NewLine
                + "  verified: "
                + verifiedPath
                + Environment.NewLine
                + "  received: "
                + receivedPath
                + Environment.NewLine
                + detail
                + Environment.NewLine
                + "Review the received file; to accept it, run the test again with "
                + SnapshotSettings.AcceptVariable
                + "=1 (or rename it to .verified)."
        );
}
