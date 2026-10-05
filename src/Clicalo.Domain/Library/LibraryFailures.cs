using Clicalo.Domain.Errors;
using Clicalo.Domain.Messages;
using Clicalo.Domain.Primitives;

namespace Clicalo.Domain.Library;

/// <summary>
/// The expected failures of <see cref="ShortcutLibrary"/> operations. Codes are stable (logs and tests); the user sees
/// the message.
/// </summary>
internal static class LibraryFailures
{
    public const string ShortcutNotFoundCode = "library.shortcut.not_found";
    public const string ProfileNotFoundCode = "library.profile.not_found";
    public const string DuplicateIdCode = "library.id.duplicate";
    public const string EmptyIdCode = "library.id.empty";
    public const string GeneralProtectedCode = "library.general.protected";
    public const string GeneralUnboundCode = "library.general.unbound";
    public const string ProcessBoundCode = "library.process.bound";
    public const string ProcessEmptyCode = "library.process.empty";
    public const string ProfileNameEmptyCode = "library.profile.name_empty";
    public const string InvalidCode = "library.invalid";

    public static Failure ShortcutNotFound() => Warning(ShortcutNotFoundCode, L.ItemGone);

    public static Failure ProfileNotFound() => Warning(ProfileNotFoundCode, L.ItemGone);

    public static Failure DuplicateId() => Warning(DuplicateIdCode, L.Retry);

    public static Failure EmptyId() => Warning(EmptyIdCode, L.Retry);

    public static Failure GeneralProtected() => Warning(GeneralProtectedCode, L.GeneralFixed);

    public static Failure GeneralUnbound() => Warning(GeneralUnboundCode, L.GeneralFixed);

    public static Failure ProcessBound(ProcessName process) =>
        Warning(ProcessBoundCode, L.LinkedT(process.Value));

    public static Failure ProcessEmpty() => Warning(ProcessEmptyCode, L.UnlinkedT);

    public static Failure ProfileNameEmpty() => Warning(ProfileNameEmptyCode, L.ProfName);

    public static Failure Invalid(LibraryViolation violation) =>
        Warning(InvalidCode + "." + CodeOf(violation.Invariant), L.Retry);

    private static string CodeOf(LibraryInvariant invariant) =>
        invariant switch
        {
            LibraryInvariant.UniqueIds => "unique_ids",
            LibraryInvariant.SingleList => "single_list",
            LibraryInvariant.FixedListsExist => "fixed_lists",
            LibraryInvariant.GeneralUnbound => "general_unbound",
            LibraryInvariant.ProcessOwnedOnce => "process_owned_once",
            _ => "macro_steps",
        };

    private static Failure Warning(string code, Message message) =>
        new(
            code,
            message,
            FailureSeverity.Warning,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );
}
