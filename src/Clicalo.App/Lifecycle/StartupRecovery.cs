using Clicalo.Application.Ports;
using Clicalo.Application.Store;
using Clicalo.Domain.Messages;

namespace Clicalo.App.Lifecycle;

/// <summary>
/// What the start says and does when the document was not simply read (blueprint §6.5, SIS-004, DAT-003). Pure:
/// <list type="bullet">
/// <item>recovered from <c>.prev</c> or from the newest valid backup: [docRecovered];</item>
/// <item>nothing usable: the default document shows with [dataUnreadable], and it is only written once the person
/// accepts it by using it (<see cref="Accepts"/>): what could not be read was moved to <c>quarantine\</c>, never
/// overwritten;</item>
/// <item>a newer major: [saveReadOnly], and nothing is ever written.</item>
/// </list>
/// </summary>
internal static class StartupRecovery
{
    /// <summary>The notice of the start, or <see langword="null"/> when the document was read as usual.</summary>
    /// <param name="outcome">How the document was obtained.</param>
    /// <param name="awaitingAcceptance">The default document in memory waits for the person to accept it.</param>
    public static Message? NoticeFor(DocumentLoadOutcome outcome, bool awaitingAcceptance) =>
        outcome switch
        {
            DocumentLoadOutcome.RecoveredFromPrevious or DocumentLoadOutcome.RecoveredFromBackup =>
                L.DocRecovered,
            DocumentLoadOutcome.DefaultInMemory when awaitingAcceptance => L.DataUnreadable,
            DocumentLoadOutcome.FutureMajorReadOnly => L.SaveReadOnly,
            _ => null,
        };

    /// <summary>
    /// Whether <paramref name="change"/> accepts the default document: the person added, installed or edited something,
    /// or ended the welcome. A setting that changes on its own (the panel position, the last profile) or the usage of
    /// Frecuentes never does.
    /// </summary>
    /// <param name="change">A change of the document.</param>
    public static bool Accepts(DocumentChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        return !ReferenceEquals(change.Before.Library, change.After.Library)
            || change.Before.Onboarding != change.After.Onboarding;
    }
}
