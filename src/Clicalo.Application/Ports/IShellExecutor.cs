using Clicalo.Domain.Catalog;
using Clicalo.Domain.Execution;

namespace Clicalo.Application.Ports;

/// <summary>
/// Runs launches and system commands on the Shell thread (blueprint §3.2, §7.3), so a slow <c>ShellExecute</c> or WMI
/// call never delays the engine's priority lane. Never through an interpreter (LOG-008); never elevated (EJE-011). The
/// result comes back to the given inbox as <see cref="EngineEvent.LaunchCompleted"/>,
/// <see cref="EngineEvent.LaunchFailed"/> or <see cref="EngineEvent.SystemCommandCompleted"/>.
/// </summary>
public interface IShellExecutor
{
    /// <summary>Queues a launch; it is dropped if the generation is old when it runs.</summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="effect">The effect id.</param>
    /// <param name="request">What to start.</param>
    /// <param name="replyTo">Where the result goes.</param>
    void Launch(
        EngineGeneration generation,
        EffectId effect,
        LaunchRequest request,
        IEngineInbox replyTo
    );

    /// <summary>Queues a system command.</summary>
    /// <param name="generation">The caller's generation.</param>
    /// <param name="effect">The effect id.</param>
    /// <param name="command">The command.</param>
    /// <param name="replyTo">Where the result goes.</param>
    void Run(
        EngineGeneration generation,
        EffectId effect,
        SystemCommandId command,
        IEngineInbox replyTo
    );
}
