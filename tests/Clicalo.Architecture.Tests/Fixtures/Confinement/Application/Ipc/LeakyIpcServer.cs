using Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Engine;

namespace Clicalo.Architecture.Tests.Fixtures.Confinement.Application.Ipc;

/// <summary>Fixture: an IPC server that reaches the engine, which invariant D13 forbids.</summary>
public sealed class LeakyIpcServer(EngineHost engine)
{
    public void Handle() => engine.Send();
}
