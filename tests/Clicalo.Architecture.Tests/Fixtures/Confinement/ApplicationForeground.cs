using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ports;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Foreground;

/// <summary>Fixture: the orchestrator, allowed to use the foreground control.</summary>
public sealed class ForegroundOrchestrator(IForegroundControl control)
{
    public bool Acquire(nint window) => control.TrySetForeground(window);
}

/// <summary>Fixture: a port declared outside Application.Ports.</summary>
public interface IMisplacedPort
{
    void Run();
}
