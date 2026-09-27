using Clicalo.App.Lifecycle;
using Clicalo.Platform.Windows.SentinelHost;

namespace Clicalo.App.Composition;

/// <summary>The guardian of a sending start: the engine package's <see cref="SentinelSupervisor"/>.</summary>
/// <param name="supervisor">Sentinel's supervisor.</param>
internal sealed class SupervisedGuardian(SentinelSupervisor supervisor) : IGuardian
{
    /// <inheritdoc />
    public bool IsRunning => supervisor.ProcessId is not null;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        Task.Run(supervisor.Start, cancellationToken);
}
