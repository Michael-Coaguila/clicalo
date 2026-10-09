using Clicalo.Application.Ports;
using Clicalo.Domain.Catalog;
using Clicalo.Domain.Errors;
using Clicalo.Domain.Execution;
using Clicalo.Domain.Messages;

namespace Clicalo.App.Composition;

/// <summary>
/// The Shell executor of a start with <c>--no-input</c>: nothing reaches outside Clícalo, so apps, web pages and
/// system commands (<c>Platform.Windows/Launch</c> and <c>SystemCommands</c>, blueprint §3.2) never start. Every request
/// is answered at once as failed, with the notice «This action is not available yet», so the engine never waits for a
/// result that will not come.
/// </summary>
internal sealed class DeferredShellExecutor : IShellExecutor
{
    /// <summary>The failure every launch gets without input.</summary>
    public static Failure Unavailable { get; } =
        new(
            "shell.unavailable",
            L.ActionUnavailable,
            FailureSeverity.Info,
            FailureRecovery.None,
            FailureAnnouncement.Polite
        );

    /// <inheritdoc />
    public void Launch(EffectId effect, LaunchRequest request, IEngineInbox replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        _ = replyTo.Post(new EngineEvent.LaunchFailed(effect, Unavailable));
    }

    /// <inheritdoc />
    public void Run(EffectId effect, SystemCommandId command, IEngineInbox replyTo)
    {
        ArgumentNullException.ThrowIfNull(replyTo);
        _ = replyTo.Post(new EngineEvent.SystemCommandCompleted(effect, Succeeded: false));
    }
}
