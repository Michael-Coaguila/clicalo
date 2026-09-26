using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;

namespace Clicalo.Application.Store;

/// <summary>The failures of <see cref="DocumentStore"/> itself; command failures pass through unchanged.</summary>
internal static class StoreFailures
{
    public const string UnconfirmedCode = "store.destructive.unconfirmed";
    public const string ConfirmationMismatchCode = "store.confirmation.mismatch";
    public const string NothingToUndoCode = "store.undo.empty";

    /// <summary>A destructive command dispatched without its token (REG-04; CLC0010 misses it through a base type).</summary>
    public static Failure Unconfirmed() => Warning(UnconfirmedCode, L.DelConfirm);

    /// <summary>A token confirmed for another operation.</summary>
    public static Failure ConfirmationMismatch() => Warning(ConfirmationMismatchCode, L.DelConfirm);

    /// <summary>Undo with an empty history.</summary>
    public static Failure NothingToUndo() =>
        new(
            NothingToUndoCode,
            L.Undo,
            FailureSeverity.Info,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );

    private static Failure Warning(string code, Message message) =>
        new(
            code,
            message,
            FailureSeverity.Warning,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );
}
